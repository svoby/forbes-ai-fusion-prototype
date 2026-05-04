using System.Collections;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;
using UnityEngine.TestTools;

namespace Forbes.Tests.PlayMode {
  /// <summary>
  /// Fusion Single-player smokes for moving-target missile logic (<see cref="SpellRegistry"/> id 1,
  /// Fireball). The missile is a homing logical projectile that advances toward the target's
  /// current position each tick; it is not bound to a fixed impact tick.
  /// </summary>
  [TestFixture]
  public class NetworkCombatProjectileTravelSmokeTests {
    FusionSinglePlayerTestSession _session;
    NetworkObject                 _player;
    NetworkObject                 _dummy;

    [SetUp]
    public void SetUp() {
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
      _session = new FusionSinglePlayerTestSession();
      _player  = null;
      _dummy   = null;
    }

    [TearDown]
    public void TearDown() {
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    IEnumerator SpawnBoth(string context = "") {
      var runner = _session.Runner;
      Assert.IsNotNull(runner, $"{context}: runner null");
      yield return FusionPlayModeTestHelpers.WaitFrames(5);

      var playerPrefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.PlayerCharacterPrefabPath);
      var dummyPrefab  = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.TrainingDummyPrefabPath);
      Assert.IsNotNull(playerPrefab, $"{context}: playerPrefab null");
      Assert.IsNotNull(dummyPrefab,  $"{context}: dummyPrefab null");

      var spawnFlags = NetworkSpawnFlags.SharedModeStateAuthLocalPlayer;

      yield return FusionPlayModeTestHelpers.SpawnPlayerPrefabBlocking(
        runner, playerPrefab, new Vector3(-2f, 1f, 0f), Quaternion.identity, spawnFlags, o => _player = o);

      runner.SetPlayerObject(runner.LocalPlayer, _player);
      Assert.IsTrue(runner.TryGetPlayerObject(runner.LocalPlayer, out _));

      yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
        runner, dummyPrefab, new Vector3(6f, 0f, 6f), Quaternion.identity,
        PlayerRef.None, spawnFlags, o => _dummy = o);

      if (_dummy.TryGetComponent(out NetworkMobBrain mobBrain)) {
        FusionPlayModeTestHelpers.PinMobBrainNoCombat(mobBrain);
      }
      if (_dummy.TryGetComponent(out NetworkTransform mobNt)) {
        mobNt.DisableSharedModeInterpolation = true;
      }

      yield return FusionPlayModeTestHelpers.WaitFrames(3);

      if (_player.TryGetComponent(out Health playerHealth)) {
        playerHealth.AuthorityApplyStartingHealthIfUnset();
      }
      if (_dummy.TryGetComponent(out Health dummyHealth)) {
        dummyHealth.AuthorityApplyStartingHealthIfUnset();
      }

      yield return FusionPlayModeTestHelpers.WaitFrames(5);
    }

    IEnumerator FireFireball() {
      _session.InputRelay.TargetNetworkId = _dummy.Id;
      _session.InputRelay.PendingPulse    = FusionPlayModeSpellPulse.Spell1;
      yield return FusionPlayModeTestHelpers.WaitUntil(
        () => _player.GetComponent<NetworkCombatController>().PendingImpactSpellId != 0,
        maxFrames: 1200,
        messageOnFail: "Fireball missile not scheduled within timeout");
    }

    // ── Tests ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Missile-in-flight: HP must not change until the missile arrives.
    /// We wait for a short window well below the expected travel time at initial
    /// distance (~10 m / 20 m·s⁻¹ ≈ 0.5 s → ~30 ticks at 60 Hz) and assert HP is
    /// still at the start value.
    /// </summary>
    [UnityTest]
    [Timeout(120000)]
    public IEnumerator ProjectileSpell_DamageNotAppliedDuringFlight() {
      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        yield return SpawnBoth(nameof(ProjectileSpell_DamageNotAppliedDuringFlight));

        var combat     = _player.GetComponent<NetworkCombatController>();
        var dummyHealth = _dummy.GetComponent<Health>();
        float startHp  = dummyHealth.NetworkedHealth;

        Assert.IsFalse(SpellTravelLogic.HasProjectile(SpellRegistry.Get(2)), "Spell 2 must be hitscan for this test to be meaningful.");
        Assert.IsTrue(SpellTravelLogic.HasProjectile(SpellRegistry.Get(1)),  "Spell 1 (Fireball) must be a projectile.");

        yield return FireFireball();

        // Wait well under the expected travel time so the missile is still in flight.
        // Derived from spell data so the assertion survives speed tuning:
        //   spawn positions are (-2,1,0) and (6,0,6) → dist ≈ 10.2 m
        //   at 20 m/s / 60 Hz → ~31 ticks; we wait at most travelTicks/4.
        var  spell1        = SpellRegistry.Get(1);
        var  runner        = _session.Runner;
        float spawnDist    = Vector3.Distance(new Vector3(-2f, 1f, 0f), new Vector3(6f, 0f, 6f));
        int  travelTicks   = SpellTravelLogic.ComputeTravelTicks(
          spawnDist, spell1.ProjectileSpeedMetersPerSecond, Mathf.RoundToInt(runner.TickRate));
        int  inFlightFrames = Mathf.Max(3, travelTicks / 4);
        yield return FusionPlayModeTestHelpers.WaitFrames(inFlightFrames);

        Assert.AreEqual(startHp, dummyHealth.NetworkedHealth, 0.35f,
          "Dummy HP must not change while missile is still in flight.");
        Assert.AreNotEqual(0, combat.PendingImpactSpellId,
          "Missile slot must still be occupied during flight.");

        Assert.Greater(_player.GetComponent<Health>().NetworkedHealth, 0.5f,
          "Caster should stay alive for this smoke.");
      }
    }

    /// <summary>
    /// Missile eventually resolves and applies the correct damage.
    /// </summary>
    [UnityTest]
    [Timeout(120000)]
    public IEnumerator ProjectileSpell_DamageAppliedAfterMissileArrives() {
      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        yield return SpawnBoth(nameof(ProjectileSpell_DamageAppliedAfterMissileArrives));

        var dummyHealth = _dummy.GetComponent<Health>();
        float startHp   = dummyHealth.NetworkedHealth;
        float fireballDmg = SpellRegistry.Get(1).Damage;

        yield return FireFireball();

        // Poll until HP drops by the expected damage amount.
        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => Mathf.Abs(dummyHealth.NetworkedHealth - (startHp - fireballDmg)) < 0.36f,
          maxFrames: 960,
          messageOnFail: $"post-impact hp={dummyHealth.NetworkedHealth} expected ~{startHp - fireballDmg}");

        Assert.AreEqual(0, _player.GetComponent<NetworkCombatController>().PendingImpactSpellId,
          "Missile slot must be cleared after impact.");
      }
    }

    /// <summary>
    /// Target dies while missile is in flight → missile is cancelled, no extra damage applied.
    /// </summary>
    [UnityTest]
    [Timeout(120000)]
    public IEnumerator ProjectileSpell_DeadTarget_NoDelayedDamage_ClearPendingSafely() {
      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        yield return SpawnBoth(nameof(ProjectileSpell_DeadTarget_NoDelayedDamage_ClearPendingSafely));

        var combat     = _player.GetComponent<NetworkCombatController>();
        var dummyHealth = _dummy.GetComponent<Health>();

        yield return FireFireball();

        // Kill the target while the missile is in flight.
        dummyHealth.DealDamageRpc(dummyHealth.NetworkedHealth + 100f);
        yield return FusionPlayModeTestHelpers.WaitUntil(() => dummyHealth.IsDead, maxFrames: 240,
          messageOnFail: "Dummy did not die from lethal damage RPC.");

        Assert.AreEqual(0f, dummyHealth.NetworkedHealth, 0.01f);

        // Wait well past normal travel time; missile must be discarded, not applied.
        yield return FusionPlayModeTestHelpers.WaitFrames(120);

        Assert.IsTrue(dummyHealth.IsDead,     "Target must stay dead — no missile resurrection.");
        Assert.AreEqual(0f, dummyHealth.NetworkedHealth, 0.01f,
          "Dead target must not receive delayed projectile damage.");
        Assert.AreEqual(0, combat.PendingImpactSpellId,
          "Missile slot must be cleared when target is dead.");
      }
    }

    /// <summary>
    /// Caster dies before the missile arrives → pending missile is cleared by
    /// TryCancelCast(Death) and the target is unharmed.
    /// </summary>
    [UnityTest]
    [Timeout(120000)]
    public IEnumerator ProjectileSpell_CasterDiesBeforeImpact_PendingImpactCleared_TargetUnharmed() {
      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        yield return SpawnBoth(nameof(ProjectileSpell_CasterDiesBeforeImpact_PendingImpactCleared_TargetUnharmed));

        var combat      = _player.GetComponent<NetworkCombatController>();
        var playerHealth = _player.GetComponent<Health>();
        var dummyHealth  = _dummy.GetComponent<Health>();
        float startDummyHp = dummyHealth.NetworkedHealth;

        yield return FireFireball();

        // Kill the caster while the missile is in flight.
        playerHealth.DealDamageRpc(playerHealth.NetworkedHealth + 100f);
        yield return FusionPlayModeTestHelpers.WaitUntil(() => playerHealth.IsDead, maxFrames: 240,
          messageOnFail: "Caster did not die after lethal DealDamageRpc.");

        // Wait well past normal travel time; cleared missile must not land.
        yield return FusionPlayModeTestHelpers.WaitFrames(120);

        Assert.AreEqual(0, combat.PendingImpactSpellId,
          "Pending missile must be cleared when the caster dies.");
        Assert.AreEqual(startDummyHp, dummyHealth.NetworkedHealth, 0.35f,
          "Target must be undamaged: in-flight missile abandoned on caster death.");
        Assert.IsFalse(dummyHealth.IsDead,
          "Target should be alive — no projectile damage was applied.");
      }
    }

    /// <summary>
    /// Target moves a moderate distance (5 m) after missile release — the missile
    /// homes, catches up, and damage is still applied. Validates the moving-target
    /// missile model: flight is extended by target movement but impact still lands.
    /// </summary>
    [UnityTest]
    [Timeout(120000)]
    public IEnumerator ProjectileSpell_TargetMovesModerately_MissileCatches_StillDamaged() {
      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        yield return SpawnBoth(nameof(ProjectileSpell_TargetMovesModerately_MissileCatches_StillDamaged));

        var dummyHealth = _dummy.GetComponent<Health>();
        float startHp   = dummyHealth.NetworkedHealth;
        float dmg       = SpellRegistry.Get(1).Damage;

        yield return FireFireball();

        // Move the target 5 m away after missile is launched. At 20 m/s missile
        // speed the missile will still catch a stationary target at ≤ 30 m range.
        Vector3 nudged = _dummy.transform.position + new Vector3(5f, 0f, 0f);
        FusionPlayModeTestHelpers.TeleportNetworkObjectForPlayModeSmokeTest(_dummy, nudged);

        // Give the missile enough time to home and arrive.
        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => Mathf.Abs(dummyHealth.NetworkedHealth - (startHp - dmg)) < 0.36f,
          maxFrames: 960,
          messageOnFail:
          $"After 5 m move, damage not applied. hp={dummyHealth.NetworkedHealth} expect~{startHp - dmg}");

        Assert.IsFalse(dummyHealth.IsDead, "Target must survive a Fireball hit.");
      }
    }

    /// <summary>
    /// Case 6: Moving while a cast-time spell is in progress must cancel the cast.
    /// Uses Heavy Blast (spell 3, castTimeSec=2.5 s) — a cast-time non-projectile
    /// spell — so there is no pending missile to confuse the assertion.
    /// </summary>
    [UnityTest]
    [Timeout(120000)]
    public IEnumerator CastTimedSpell_MovementDuringCast_CancelsCast_NoDamageApplied() {
      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        yield return SpawnBoth(nameof(CastTimedSpell_MovementDuringCast_CancelsCast_NoDamageApplied));

        var combat      = _player.GetComponent<NetworkCombatController>();
        var dummyHealth = _dummy.GetComponent<Health>();
        float startHp   = dummyHealth.NetworkedHealth;

        Assert.IsFalse(SpellTravelLogic.HasProjectile(SpellRegistry.Get(3)),
          "Heavy Blast (spell 3) must be a non-projectile cast-time spell for this test.");

        // Start Heavy Blast cast (2.5 s cast, no projectile, 8 s cooldown).
        _session.InputRelay.TargetNetworkId = _dummy.Id;
        _session.InputRelay.PendingPulse    = FusionPlayModeSpellPulse.Spell3;

        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => combat.CurrentSpellId == 3,
          maxFrames: 120,
          messageOnFail: "Heavy Blast cast did not start within timeout");

        // Inject sustained movement so the cast-cancel path fires.
        _session.InputRelay.StickyMove = new Vector2(1f, 0f);

        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => combat.CurrentSpellId == 0,
          maxFrames: 120,
          messageOnFail: "Cast was not cancelled by movement input within timeout");

        _session.InputRelay.StickyMove = Vector2.zero;

        // Wait well past the original 2.5 s cast window — no damage must arrive.
        yield return FusionPlayModeTestHelpers.WaitFrames(200);

        Assert.AreEqual(startHp, dummyHealth.NetworkedHealth, 0.35f,
          "No damage must be applied after a movement-cancelled cast-time spell.");
        Assert.AreEqual(0, combat.PendingImpactSpellId,
          "No missile must be in flight after a non-projectile cancelled cast.");
      }
    }

    /// <summary>
    /// Case 7: Moving after a projectile is already in flight must NOT cancel the missile.
    /// After ResolveCast + ClearCastState, IsCasting=false; the movement-cancel guard
    /// only fires when IsCasting is true, so the missile advances and impacts normally.
    /// </summary>
    [UnityTest]
    [Timeout(120000)]
    public IEnumerator ProjectileSpell_MovementAfterRelease_DoesNotCancelMissile() {
      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        yield return SpawnBoth(nameof(ProjectileSpell_MovementAfterRelease_DoesNotCancelMissile));

        var dummyHealth = _dummy.GetComponent<Health>();
        float startHp   = dummyHealth.NetworkedHealth;
        float dmg       = SpellRegistry.Get(1).Damage;

        // Fire Fireball and wait for the missile to be in flight.
        yield return FireFireball();

        Assert.AreNotEqual(0, _player.GetComponent<NetworkCombatController>().PendingImpactSpellId,
          "Missile must be in flight before movement is injected.");

        // Inject continuous movement AFTER missile is in flight.
        _session.InputRelay.StickyMove = new Vector2(1f, 0f);

        // Missile must still arrive and deal damage despite caster movement.
        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => Mathf.Abs(dummyHealth.NetworkedHealth - (startHp - dmg)) < 0.36f,
          maxFrames: 960,
          messageOnFail:
            $"Missile did not land after caster moved. hp={dummyHealth.NetworkedHealth} expected~{startHp - dmg}");

        _session.InputRelay.StickyMove = Vector2.zero;

        Assert.AreEqual(0, _player.GetComponent<NetworkCombatController>().PendingImpactSpellId,
          "Missile slot must be cleared after impact.");
        Assert.IsFalse(dummyHealth.IsDead, "Target must survive a single Fireball.");
      }
    }

    /// <summary>
    /// Target runs far away after missile is released but stays within the
    /// missile's reachable distance. Validates that the missile follows the target
    /// and impact is delayed but still resolves.
    /// </summary>
    [UnityTest]
    [Timeout(120000)]
    public IEnumerator ProjectileSpell_TargetRunsFarAway_MissileFollows_ImpactDelayedButLands() {
      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        yield return SpawnBoth(nameof(ProjectileSpell_TargetRunsFarAway_MissileFollows_ImpactDelayedButLands));

        var dummyHealth = _dummy.GetComponent<Health>();
        float startHp   = dummyHealth.NetworkedHealth;
        float dmg       = SpellRegistry.Get(1).Damage;

        yield return FireFireball();

        // Teleport to 20 m away — well within Fireball range (30 m) so the
        // missile can still home in and catch the stationary target.
        Vector3 farPos = _dummy.transform.position + new Vector3(20f, 0f, 0f);
        FusionPlayModeTestHelpers.TeleportNetworkObjectForPlayModeSmokeTest(_dummy, farPos);

        // Missile must still arrive — give it generous time. The missile starts at
        // the caster (~-2,1,0) and the target teleports to ~(26,0,6): total distance
        // ≈ 28.6 m at 20 m/s ≈ 1.43 s / ~86 ticks at 60 Hz. The 960-frame budget
        // (16 s) is well above that; target is stationary after the teleport.
        yield return FusionPlayModeTestHelpers.WaitUntil(
          () => Mathf.Abs(dummyHealth.NetworkedHealth - (startHp - dmg)) < 0.36f,
          maxFrames: 960,
          messageOnFail:
          $"After 20 m teleport, missile did not land. hp={dummyHealth.NetworkedHealth} expect~{startHp - dmg}");

        Assert.IsFalse(dummyHealth.IsDead, "Target must survive a single Fireball.");
      }
    }
  }
}
