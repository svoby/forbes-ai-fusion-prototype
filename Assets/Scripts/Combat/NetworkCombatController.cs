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
public partial class NetworkCombatController : NetworkBehaviour {
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

  /// <summary>Latest player-facing combat warning; HUD shows for ~2 s.</summary>
  [Networked] public byte LastCombatFeedbackReason { get; set; }

  /// <summary>Tick at which <see cref="LastCombatFeedbackReason"/> was set.</summary>
  [Networked] public int LastCombatFeedbackTick { get; set; }

  // ── COOLDOWN / GCD ──────────────────────────────────────────────────────────
  // All tick-based; derived from Runner.TickRate at cast time. Observed by UI.

  /// <summary>Tick when the global cooldown expires.</summary>
  [Networked] public int GcdEndTick { get; set; }

  // Individual spell cooldowns (three spells; extend if more are added).
  [Networked] public int Cooldown1EndTick { get; set; }
  [Networked] public int Cooldown2EndTick { get; set; }
  [Networked] public int Cooldown3EndTick { get; set; }

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
  PlayerMissileSlot _missileSlot;
  NetworkButtons  _prevButtons;

  void Awake() {
    _health      = GetComponent<Health>();
    _impactView  = GetComponent<SpellImpactView>();
    _missileSlot = GetComponent<PlayerMissileSlot>();
  }

  public override void Spawned() {
    _missileSlot.OnImpact    += HandleMissileImpact;
    _missileSlot.OnCancelled += HandleMissileCancelled;
  }

  public override void Despawned(NetworkRunner runner, bool hasState) {
    if (_missileSlot != null) {
      _missileSlot.OnImpact    -= HandleMissileImpact;
      _missileSlot.OnCancelled -= HandleMissileCancelled;
    }
  }

  void HandleMissileImpact(byte spellId, NetworkId targetId, Health targetHealth) {
    var spell = SpellRegistry.Get(spellId);
    targetHealth.DealDamageRpc(spell.Damage);
    DispatchImpactVisual(spellId, targetId);
  }

  void HandleMissileCancelled(CombatFeedbackReason reason) {
    SetCombatFeedback(reason);
  }

  public override void FixedUpdateNetwork() {
    if (!HasStateAuthority) {
      return;
    }

    if (!TickCombatRuntime()) {
      return;
    }

    if (!GetInput(out GameplayInput input)) {
      return;
    }

    ProcessPlayerInput(input);
  }

  // Runtime spell work is intentionally separate from player input dispatch so
  // future AI/mob casters can request casts without becoming a second combat system.
  bool TickCombatRuntime() {
    if (_health != null && _health.IsDead) {
      TryCancelCast(CastCancelReason.Death);
      return false;
    }

    // Resolve a cast-time spell when the timer expires.
    if (CurrentSpellId != 0 && Runner.Tick >= CastEndTick) {
      ResolveCast();
      return false;
    }

    return true;
  }

  void ProcessPlayerInput(GameplayInput input) {
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
              out _, out var midCastFailReason)
            && IsMidCastCancelReason(midCastFailReason)) {
          TryCancelCast(CastCancelReason.InvalidTarget);
        }
      }
    }

    // ── INPUT DISPATCH ───────────────────────────────────────────────────────
    if (TryGetPressedPlayerSpell(input.Buttons, _prevButtons, out byte requestedSpellId)) {
      TryRequestCast(requestedSpellId, input.TargetId);
    }

    _prevButtons = input.Buttons;
  }

  // Player-specific button mapping. Combat runtime consumes spell IDs so AI/mob
  // casters can later call TryRequestCast directly without depending on input enums.
  static bool TryGetPressedPlayerSpell(NetworkButtons buttons, NetworkButtons previousButtons, out byte spellId) {
    if (buttons.WasPressed(previousButtons, (int)GameplayButtons.Spell1)) {
      spellId = 1;
      return true;
    }

    if (buttons.WasPressed(previousButtons, (int)GameplayButtons.Spell2)) {
      spellId = 2;
      return true;
    }

    if (buttons.WasPressed(previousButtons, (int)GameplayButtons.Spell3)) {
      spellId = 3;
      return true;
    }

    spellId = 0;
    return false;
  }

  /// <summary>
  /// Returns true when a mid-cast validation failure is severe enough to
  /// interrupt the cast immediately (e.g. target vanished or died).
  /// <para>
  /// <see cref="CombatFailReason.OutOfRange"/> is intentionally excluded: the
  /// target may re-enter range before the cast finishes. Range is re-checked
  /// at cast completion inside <see cref="ResolveCast"/>, which will emit an
  /// <see cref="CombatFeedbackReason.OutOfRange"/> HUD notification if the
  /// target is still too far away when the cast resolves.
  /// </para>
  /// </summary>
  internal static bool IsMidCastCancelReason(CombatFailReason reason) {
    return reason != CombatFailReason.None && reason != CombatFailReason.OutOfRange;
  }

}
