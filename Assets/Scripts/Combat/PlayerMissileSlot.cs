using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Authoritative single-slot missile for one caster.
/// Owns all missile state: four replicated travel-description fields
/// (<see cref="PendingImpactSpellId"/>, <see cref="PendingImpactTarget"/>,
/// <see cref="PendingMissileReleaseTick"/>, <see cref="MissileOrigin"/>),
/// the authority-only virtual position, and per-tick advance/impact logic.
/// <para>
/// Authority contract: <see cref="FixedUpdateNetwork"/> only writes state on
/// <see cref="HasStateAuthority"/>. All clients observe the four replicated
/// fields to drive <see cref="CosmeticProjectileView"/> locally without requiring
/// a networked position.
/// </para>
/// <para>
/// Execution order: <c>[DefaultExecutionOrder(-200)]</c> ensures this resolves
/// missiles before <see cref="NetworkCombatController"/> (<c>-100</c>) processes
/// new player inputs on the same tick, preserving the resolve-before-schedule
/// invariant.
/// </para>
/// </summary>
[DefaultExecutionOrder(-200)]
public class PlayerMissileSlot : NetworkBehaviour {
  // ── REPLICATED TRAVEL STATE ──────────────────────────────────────────────────
  // These four fields fully describe an in-flight spell from any client's perspective.
  // Cosmetic views derive a local visual position from them; no networked position
  // field is needed and _missileVirtualPos must never be replicated.

  /// <summary>0 = no missile in flight.</summary>
  [Networked] public byte PendingImpactSpellId { get; set; }

  /// <summary>Target locked at missile release; resolved each tick.</summary>
  [Networked] public NetworkId PendingImpactTarget { get; set; }

  /// <summary>Simulation tick when the missile was released; retained for cosmetic timing.</summary>
  [Networked] public int PendingMissileReleaseTick { get; set; }

  /// <summary>
  /// Caster world position captured at the moment of <see cref="Schedule"/>.
  /// Replicated so cosmetic views can reconstruct a correct launch arc even if the
  /// caster moves after releasing the missile (fixes the lerp-origin drift in
  /// <see cref="CosmeticProjectileView"/>).
  /// </summary>
  [Networked] public Vector3 MissileOrigin { get; set; }

  // ── EVENTS ───────────────────────────────────────────────────────────────────
  // Fired synchronously from within FixedUpdateNetwork on state authority.
  // NetworkCombatController subscribes in Spawned and unsubscribes in Despawned.

  /// <summary>Fired on state authority when the missile reaches the target.</summary>
  public event Action<byte, NetworkId, Health> OnImpact;

  /// <summary>Fired on state authority when the missile is abandoned (target gone or dead).</summary>
  public event Action<CombatFeedbackReason> OnCancelled;

  // ── AUTHORITY-ONLY STATE ─────────────────────────────────────────────────────
  // Missile virtual position advances per-tick on state authority, homing toward the
  // target's current position. Not networked; cosmetic views do NOT read this field.
  // Reset by Clear(); re-initialized in Spawned() after authority transfer.
  Vector3 _missileVirtualPos;

  // ── FUSION LIFECYCLE ─────────────────────────────────────────────────────────

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

    TryResolvePendingImpact();
  }

  // ── PUBLIC API ───────────────────────────────────────────────────────────────

  /// <summary>
  /// Arms the missile slot. Captures the caster's world position as
  /// <see cref="MissileOrigin"/> so cosmetic views reconstruct a correct arc.
  /// If a missile is already in flight it is silently overwritten (one-slot limit;
  /// see <c>docs/PROJECTILE_POLICY.md</c>).
  /// </summary>
  public void Schedule(byte spellId, NetworkId targetId, Vector3 casterPos) {
    if (PendingImpactSpellId != 0) {
      ForbesLog.Net(
        $"Schedule: overwriting in-flight missile spellId={PendingImpactSpellId} — one-slot limit.", this);
    }

    PendingImpactSpellId      = spellId;
    PendingImpactTarget       = targetId;
    PendingMissileReleaseTick = Runner.Tick;
    MissileOrigin             = casterPos;
    _missileVirtualPos        = casterPos;
  }

  /// <summary>
  /// Clears the missile slot unconditionally. Called on caster death or spell abort.
  /// Does not fire <see cref="OnImpact"/> or <see cref="OnCancelled"/>.
  /// </summary>
  public void Clear() {
    PendingImpactSpellId      = 0;
    PendingImpactTarget       = default;
    PendingMissileReleaseTick = 0;
    MissileOrigin             = default;
    _missileVirtualPos        = default;
  }

  // ── PRIVATE LOGIC ────────────────────────────────────────────────────────────

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
      Clear();
      return;
    }

    if (!Runner.TryFindObject(PendingImpactTarget, out var targetObj)
        || targetObj == null
        || !targetObj.TryGetComponent(out Health impactHealth)) {
      ForbesLog.Net("Missile: target missing -> NoTarget", this);
      Clear();
      OnCancelled?.Invoke(CombatFeedbackReason.NoTarget);
      return;
    }

    if (impactHealth.IsDead) {
      ForbesLog.Net("Missile: target dead — impact cancelled", this);
      Clear();
      OnCancelled?.Invoke(CombatFeedbackReason.TargetDead);
      return;
    }

    Vector3 targetPos = targetObj.transform.position;
    float   speed     = spell.ProjectileSpeedMetersPerSecond;
    float   dt        = Runner.DeltaTime;

    _missileVirtualPos = SpellTravelLogic.AdvanceMissilePosition(_missileVirtualPos, targetPos, speed, dt);

    if (!SpellTravelLogic.HasMissileArrived(_missileVirtualPos, targetPos, speed, dt)) {
      return;
    }

    byte      arrivalSpellId = PendingImpactSpellId;
    NetworkId arrivalTarget  = PendingImpactTarget;
    Clear();
    OnImpact?.Invoke(arrivalSpellId, arrivalTarget, impactHealth);
  }
}
