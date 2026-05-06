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
              out _, out var midCastFailReason)
            && IsMidCastCancelReason(midCastFailReason)) {
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
