using Fusion;
using UnityEngine;

/// <summary>
/// Authoritative spell casting, GCD, and per-spell cooldowns for one player.
/// Replaces the legacy <c>PlayerCombat</c> component.
/// <para>
/// Authority model (Fusion Shared Mode):
/// The player is their own State Authority. All networked properties below are
/// written only on the player's own machine. Other clients observe them to render
/// cast bars, cooldown timers, etc.
/// </para>
/// <para>
/// Time source: all timers are stored as Fusion tick integers so they survive
/// host migration, late-join, and multi-client editor testing.  Seconds are
/// derived from ticks via <c>Runner.DeltaTime</c> on the render side.
/// </para>
/// </summary>
[DefaultExecutionOrder(-100)]
public class NetworkCombatController : NetworkBehaviour {
  const float GcdSec = 1.0f;

  /// <summary>Squared move magnitude above this cancels a cast-time spell (state authority).</summary>
  const float MovementCancelSqr = 1e-6f;

  // ── CAST STATE ──────────────────────────────────────────────────────────────
  // Networked; written only on state authority. Clients observe for cast bar UI.

  /// <summary>Spell ID currently being cast; 0 = idle.</summary>
  [Networked] public byte      CurrentSpellId  { get; set; }

  /// <summary>Target being cast on (for validation at cast completion).</summary>
  [Networked] public NetworkId CastTarget      { get; set; }

  /// <summary>Tick when the current cast started (for progress bar).</summary>
  [Networked] public int       CastStartTick   { get; set; }

  /// <summary>Tick when the current cast resolves; if Tick >= CastEndTick the spell fires.</summary>
  [Networked] public int       CastEndTick     { get; set; }

  /// <summary>Last cast failure reason; shown in HUD for a few seconds.</summary>
  [Networked] public byte LastFailReason { get; set; }

  /// <summary>Tick at which <see cref="LastFailReason"/> was set; HUD hides after ~2 s.</summary>
  [Networked] public int LastFailTick { get; set; }

  // ── COOLDOWN / GCD ──────────────────────────────────────────────────────────
  // All tick-based; derived from Runner.TickRate at cast time. Observed by UI.

  /// <summary>Tick when the global cooldown expires.</summary>
  [Networked] public int GcdEndTick { get; set; }

  // Individual spell cooldowns (three spells; extend if more are added).
  [Networked] public int Cooldown1EndTick { get; set; }
  [Networked] public int Cooldown2EndTick { get; set; }
  [Networked] public int Cooldown3EndTick { get; set; }

  // ── PENDING MISSILE SLOT ─────────────────────────────────────────────────────
  // ONE-SLOT MODEL: only one in-flight missile at a time.
  // With a stationary target at max range (30 m / 20 m·s⁻¹ = 1.5 s) a second
  // Fireball cast (also 1.5 s) resolves exactly as the first arrives. However
  // if the target flees during flight the travel time EXCEEDS the cast time and
  // SchedulePendingImpact will silently overwrite the first missile (logged as a
  // warning). This is an acknowledged prototype limitation; see PROJECTILE_POLICY.md.
  // Upgrade path: extract to a sibling TargetedMissileSlot : NetworkBehaviour
  // with a small NetworkLinkedList when multi-missile support is needed.

  /// <summary>0 = no missile in flight.</summary>
  [Networked] public byte PendingImpactSpellId { get; set; }

  /// <summary>Target locked at missile release (<see cref="NetworkId"/>); resolved each tick.</summary>
  [Networked] public NetworkId PendingImpactTarget { get; set; }

  /// <summary>Simulation tick when the missile was released; retained for diagnostics.</summary>
  [Networked] public int PendingMissileReleaseTick { get; set; }

  /// <summary>True while a cast-time spell is in flight; <see cref="PlayerMovement"/> uses this to freeze movement.</summary>
  public bool IsCasting => CurrentSpellId != 0 && Runner != null && Runner.Tick < CastEndTick;

  /// <summary>0–1 cast progress for UI render; avoids NRE when <see cref="Runner"/> not yet resolved.</summary>
  public float CastProgress {
    get {
      if (Runner == null) {
        return 0f;
      }
      int total = CastEndTick - CastStartTick;
      return total <= 0 ? 0f : Mathf.Clamp01((float)(Runner.Tick - CastStartTick) / total);
    }
  }

  Health          _health;
  SpellImpactView _impactView;
  NetworkButtons  _prevButtons;

  // Non-networked; state authority only. Tracks the missile's logical position
  // each tick so it can home toward the target's current position. Reset to
  // default by ClearPendingImpact. Lost on authority transfer — acceptable for
  // prototype (a future fix can re-derive from PendingMissileReleaseTick on Spawned).
  Vector3 _missileVirtualPos;

  void Awake() {
    _health     = GetComponent<Health>();
    _impactView = GetComponent<SpellImpactView>();
  }

  public override void Spawned() {
    if (HasStateAuthority && PendingImpactSpellId != 0) {
      // Authority transferred while a missile was in flight. _missileVirtualPos is
      // non-networked, so the new authority starts with Vector3.zero. Re-init to the
      // caster's current position so the missile homes correctly from here onward.
      _missileVirtualPos = transform.position;
      ForbesLog.Net($"Spawned with missile in flight — re-init missileVirtualPos={_missileVirtualPos}", this);
    }
  }

  public override void FixedUpdateNetwork() {
    if (!HasStateAuthority) {
      return;
    }

    if (_health != null && _health.IsDead) {
      TryCancelCast(CastCancelReason.Death);
      return;
    }

    TryResolvePendingImpact();

    // Resolve a cast-time spell when the timer expires.
    if (CurrentSpellId != 0 && Runner.Tick >= CastEndTick) {
      ResolveCast();
      return;
    }

    if (!GetInput(out GameplayInput input)) {
      return;
    }

    if (IsCasting) {
      if (input.Move.sqrMagnitude > MovementCancelSqr) {
        TryCancelCast(CastCancelReason.Movement);
      } else if (input.Buttons.WasPressed(_prevButtons, (int)GameplayButtons.Jump)) {
        TryCancelCast(CastCancelReason.Jump);
      } else {
        var castSpell = SpellRegistry.Get(CurrentSpellId);
        if (castSpell.IsValid
            && !CombatValidator.TryValidate(
              Runner, transform, CastTarget, castSpell,
              Runner.Tick, gcdEndTick: 0, cooldownEndTick: 0,
              isAlreadyCasting: false,
              out _, out _)) {
          TryCancelCast(CastCancelReason.InvalidTarget);
        }
      }
    }

    // ── INPUT DISPATCH ───────────────────────────────────────────────────────
    if (input.Buttons.WasPressed(_prevButtons, (int)GameplayButtons.Spell1)) {
      TryCastOrInterrupt(1, input.TargetId);
    } else if (input.Buttons.WasPressed(_prevButtons, (int)GameplayButtons.Spell2)) {
      TryCastOrInterrupt(2, input.TargetId);
    } else if (input.Buttons.WasPressed(_prevButtons, (int)GameplayButtons.Spell3)) {
      TryCastOrInterrupt(3, input.TargetId);
    }

    _prevButtons = input.Buttons;
  }

  // ── CAST LIFECYCLE ───────────────────────────────────────────────────────────

  /// <summary>
  /// Authoritative cast interrupt. Only mutates networked cast fields on state authority.
  /// Does not refund GCD or alter spell cooldowns.
  /// </summary>
  public void TryCancelCast(CastCancelReason reason) {
    if (!HasStateAuthority || Runner == null) {
      return;
    }

    if (reason == CastCancelReason.None) {
      return;
    }

    if (reason == CastCancelReason.Death) {
      bool log = IsCasting;
      ClearCastState();
      // Death clears both cast state and any in-flight pending impact: the
      // projectile is abandoned and the target will not be damaged.
      ClearPendingImpact();
      if (log) {
        ForbesLog.Net($"Cast cancelled: {reason}", this);
      }
      return;
    }

    // Non-Death cancellations (Movement, Jump, NewSpell, InvalidTarget) clear
    // cast state only — not pending impact. Cancellation fires before
    // ResolveCast, so no pending impact has been scheduled yet; movement
    // cannot cancel a projectile already in flight.
    if (!IsCasting) {
      return;
    }

    ClearCastState();
    ForbesLog.Net($"Cast cancelled: {reason}", this);
  }

  void TryCastOrInterrupt(byte spellId, NetworkId targetId) {
    if (IsCasting) {
      TryCancelCast(CastCancelReason.NewSpell);
    }
    TryStartCast(spellId, targetId);
  }

  // ---- Cast initiation ----

  void TryStartCast(byte spellId, NetworkId targetId) {
    var spell = SpellRegistry.Get(spellId);
    if (!spell.IsValid) {
      return;
    }

    int cooldownEnd = GetCooldownEndTick(spellId);

    if (!CombatValidator.TryValidate(
          Runner, transform, targetId, spell,
          Runner.Tick, GcdEndTick, cooldownEnd,
          isAlreadyCasting: CurrentSpellId != 0,
          out var targetHealth, out var failReason)) {
      SetFailReason(failReason);
      ForbesLog.Net($"Cast rejected: {failReason} spell={spell.Name}", this);
      return;
    }

    // GCD fires immediately on successful cast request.
    if (spell.TriggersGcd) {
      GcdEndTick = Runner.Tick + SecsToTicks(GcdSec);
    }

    int castTicks = SecsToTicks(spell.CastTimeSec);

    if (castTicks == 0) {
      SetCooldownEndTick(spellId, Runner.Tick + SecsToTicks(spell.CooldownSec));

      if (SpellTravelLogic.HasProjectile(spell)) {
        SchedulePendingImpact(spellId, targetId);
        ForbesLog.Net($"Instant cast (missile): {spell.Name} releaseTick={Runner.Tick}", this);
      } else {
        targetHealth.DealDamageRpc(spell.Damage);
        DispatchImpactVisual(spellId, targetId);
      }
    } else {
      // Cast-time spell: set networked state; damage fires in ResolveCast.
      CurrentSpellId = spellId;
      CastTarget     = targetId;
      CastStartTick  = Runner.Tick;
      CastEndTick    = Runner.Tick + castTicks;
      // Cooldown begins at cast start (matches WoW: you can't bypass CD by cancelling).
      SetCooldownEndTick(spellId, Runner.Tick + castTicks + SecsToTicks(spell.CooldownSec));
      ForbesLog.Net($"Cast started: {spell.Name} castTicks={castTicks}", this);
    }
  }

  // ---- Cast resolution ----

  void ResolveCast() {
    var spell = SpellRegistry.Get(CurrentSpellId);

    // Re-validate: target might have died or walked out of range during cast.
    if (!CombatValidator.TryValidate(
          Runner, transform, CastTarget, spell,
          Runner.Tick, gcdEndTick: 0, cooldownEndTick: 0,
          isAlreadyCasting: false,
          out var targetHealth, out var failReason)) {
      SetFailReason(failReason);
      ForbesLog.Net($"Cast resolved but invalid at completion: {failReason}", this);
      ClearCastState();
      return;
    }

    if (SpellTravelLogic.HasProjectile(spell)) {
      SchedulePendingImpact(CurrentSpellId, CastTarget);
      ForbesLog.Net($"Cast resolved (missile): {spell.Name} releaseTick={Runner.Tick}", this);
    } else {
      targetHealth.DealDamageRpc(spell.Damage);
      DispatchImpactVisual(CurrentSpellId, CastTarget);
    }

    ClearCastState();
  }

  // ---- Helpers ----

  void ClearCastState() {
    CurrentSpellId = 0;
    CastTarget     = default;
    CastStartTick  = 0;
    CastEndTick    = 0;
  }

  void ClearPendingImpact() {
    PendingImpactSpellId      = 0;
    PendingImpactTarget       = default;
    PendingMissileReleaseTick = 0;
    _missileVirtualPos        = default;
  }

  void SchedulePendingImpact(byte spellId, NetworkId targetId) {
    if (PendingImpactSpellId != 0) {
      ForbesLog.Net($"SchedulePendingImpact: overwriting in-flight missile spellId={PendingImpactSpellId} — one-slot limit.", this);
    }
    PendingImpactSpellId      = spellId;
    PendingImpactTarget       = targetId;
    PendingMissileReleaseTick = Runner.Tick;
    _missileVirtualPos        = transform.position; // missile starts at caster position
  }

  // Per-tick missile advance. Runs every FixedUpdateNetwork while a missile is in
  // flight. The missile homes toward the target's current position — movement by
  // the target extends or shortens flight time. Validates only existence and
  // liveness at each tick; range / LoS are not re-checked after release.
  void TryResolvePendingImpact() {
    if (PendingImpactSpellId == 0) {
      return;
    }

    var spell = SpellRegistry.Get(PendingImpactSpellId);
    if (!spell.IsValid) {
      ClearPendingImpact();
      return;
    }

    if (!Runner.TryFindObject(PendingImpactTarget, out var targetObj)
        || targetObj == null
        || !targetObj.TryGetComponent(out Health impactHealth)) {
      SetFailReason(CombatFailReason.NoTarget);
      ForbesLog.Net("Missile: target missing -> NoTarget", this);
      ClearPendingImpact();
      return;
    }

    if (impactHealth.IsDead) {
      SetFailReason(CombatFailReason.TargetDead);
      ForbesLog.Net("Missile: target dead — impact cancelled", this);
      ClearPendingImpact();
      return;
    }

    Vector3 targetPos = targetObj.transform.position;
    float   speed     = spell.ProjectileSpeedMetersPerSecond;
    float   dt        = Runner.DeltaTime;

    _missileVirtualPos = SpellTravelLogic.AdvanceMissilePosition(_missileVirtualPos, targetPos, speed, dt);

    if (!SpellTravelLogic.HasMissileArrived(_missileVirtualPos, targetPos, speed, dt)) {
      return;
    }

    byte     arrivalSpellId = PendingImpactSpellId;
    NetworkId arrivalTarget  = PendingImpactTarget;
    ClearPendingImpact();
    impactHealth.DealDamageRpc(spell.Damage);
    DispatchImpactVisual(arrivalSpellId, arrivalTarget);
  }

  void SetFailReason(CombatFailReason reason) {
    LastFailReason = (byte)reason;
    LastFailTick   = Runner.Tick;
  }

  int GetCooldownEndTick(byte spellId) {
    return spellId switch {
      1 => Cooldown1EndTick,
      2 => Cooldown2EndTick,
      3 => Cooldown3EndTick,
      _ => 0,
    };
  }

  void SetCooldownEndTick(byte spellId, int tick) {
    switch (spellId) {
      case 1: Cooldown1EndTick = tick; break;
      case 2: Cooldown2EndTick = tick; break;
      case 3: Cooldown3EndTick = tick; break;
    }
  }

  /// <summary>Converts seconds to tick count using the runner's fixed tick rate.</summary>
  int SecsToTicks(float seconds) {
    return SecsToTicks(Runner.TickRate, seconds);
  }

  /// <summary>
  /// Pure tick-rounding helper exposed for EditMode tick-math tests. Must match
  /// the instance overload above exactly: gameplay timers depend on it.
  /// </summary>
  internal static int SecsToTicks(int tickRate, float seconds) {
    return Mathf.CeilToInt(seconds * tickRate);
  }

  // ── Cosmetic impact visual ────────────────────────────────────────────────────

  /// <summary>
  /// Sends a cosmetic impact RPC to all clients immediately after authoritative
  /// damage is dispatched. Called only on State Authority; never mutates gameplay
  /// state on any client.
  /// </summary>
  void DispatchImpactVisual(byte spellId, NetworkId targetId) {
    RpcOnSpellImpact(spellId, targetId);
  }

  /// <summary>
  /// Received on every client (including host) after a spell successfully damages
  /// its target. Delegates to <see cref="SpellImpactView"/> for the local visual;
  /// if the component is absent the call is a no-op.
  /// </summary>
  [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
  void RpcOnSpellImpact(byte spellId, NetworkId targetId) {
    _impactView?.OnSpellImpact(spellId, targetId);
  }
}
