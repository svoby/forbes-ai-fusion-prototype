using Fusion;
using UnityEngine;

public partial class NetworkCombatController {
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
      SetCombatFeedback(CombatFeedbackReason.NoTarget);
      ForbesLog.Net("Missile: target missing -> NoTarget", this);
      ClearPendingImpact();
      return;
    }

    if (impactHealth.IsDead) {
      SetCombatFeedback(CombatFeedbackReason.TargetDead);
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
}
