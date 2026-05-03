using System.Collections;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;
using UnityEngine.TestTools;

namespace Forbes.Tests.PlayMode {
  /// <summary>
  /// Fusion Single-player smokes for logical projectile travel on cast-time spells (<see cref="SpellRegistry"/> id 1).
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

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator ProjectileSpell_DamageNotAppliedBeforeImpactTick() {
      var playerPrefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.PlayerCharacterPrefabPath);
      var dummyPrefab  = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.TrainingDummyPrefabPath);
      Assert.IsNotNull(playerPrefab);
      Assert.IsNotNull(dummyPrefab);

      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        var runner = _session.Runner;
        Assert.IsNotNull(runner);

        yield return FusionPlayModeTestHelpers.WaitFrames(5);

        var spawnPlayer = new Vector3(-2f, 1f, 0f);
        var spawnDummy  = new Vector3(6f, 0f, 6f); // planar distance sqrt(64+36)=10m (< Fireball range)

        var spawnFlags = NetworkSpawnFlags.SharedModeStateAuthLocalPlayer;

        yield return FusionPlayModeTestHelpers.SpawnPlayerPrefabBlocking(
          runner, playerPrefab, spawnPlayer, Quaternion.identity, spawnFlags, o => _player = o);

        runner.SetPlayerObject(runner.LocalPlayer, _player);
        Assert.IsTrue(runner.TryGetPlayerObject(runner.LocalPlayer, out var registered));
        Assert.AreEqual(_player.Id, registered.Id);

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner, dummyPrefab, spawnDummy, Quaternion.identity, PlayerRef.None, spawnFlags, o => _dummy = o);

        if (_dummy.TryGetComponent(out NetworkMobBrain mobBrain)) {
          FusionPlayModeTestHelpers.PinMobBrainNoCombat(mobBrain);
        }

        if (_dummy.TryGetComponent(out NetworkTransform mobNt)) {
          mobNt.DisableSharedModeInterpolation = true;
        }

        yield return FusionPlayModeTestHelpers.WaitFrames(3);

        Assert.IsTrue(_player.TryGetComponent(out NetworkCombatController combat));
        Assert.IsTrue(_player.TryGetComponent(out Health playerHealth));
        Assert.IsTrue(_dummy.TryGetComponent(out Health dummyHealth));

        playerHealth.AuthorityApplyStartingHealthIfUnset();
        dummyHealth.AuthorityApplyStartingHealthIfUnset();
        yield return FusionPlayModeTestHelpers.WaitFrames(5);

        float startDummyHp = dummyHealth.NetworkedHealth;

        Assert.IsFalse(SpellTravelLogic.HasProjectile(SpellRegistry.Get(2)));

        Assert.Greater(SpellRegistry.Get(1).ProjectileSpeedMetersPerSecond, 0f);
        Assert.IsTrue(SpellTravelLogic.HasProjectile(SpellRegistry.Get(1)));

        _session.InputRelay.TargetNetworkId = _dummy.Id;
        _session.InputRelay.PendingPulse    = FusionPlayModeSpellPulse.Spell1;

        yield return FusionPlayModeTestHelpers.WaitUntil(() => combat.PendingImpactSpellId != 0, 1200,
          messageOnFail:
          $"Fireball pending not scheduled ticks={runner.Tick} casting={combat.CurrentSpellId} end={combat.CastEndTick}");

        int pendingTick = combat.PendingImpactTick;

        while (runner.Tick < pendingTick) {
          Assert.AreEqual(startDummyHp, dummyHealth.NetworkedHealth, 0.35f,
            $"tick={runner.Tick} waited impact={pendingTick} hpBeforeImpact");
          yield return FusionPlayModeTestHelpers.WaitFrames(1);
        }

        Assert.Greater(playerHealth.NetworkedHealth, 0.5f, "Caster should stay alive for this smoke.");
      }
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator ProjectileSpell_DamageAppliedAfterImpactTick() {
      var playerPrefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.PlayerCharacterPrefabPath);
      var dummyPrefab  = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.TrainingDummyPrefabPath);
      Assert.IsNotNull(playerPrefab);
      Assert.IsNotNull(dummyPrefab);

      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        var runner = _session.Runner;
        yield return FusionPlayModeTestHelpers.WaitFrames(5);

        var spawnPlayer = new Vector3(-2f, 1f, 0f);
        var spawnDummy  = new Vector3(6f, 0f, 6f);
        var spawnFlags  = NetworkSpawnFlags.SharedModeStateAuthLocalPlayer;

        yield return FusionPlayModeTestHelpers.SpawnPlayerPrefabBlocking(
          runner, playerPrefab, spawnPlayer, Quaternion.identity, spawnFlags, o => _player = o);
        runner.SetPlayerObject(runner.LocalPlayer, _player);

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner, dummyPrefab, spawnDummy, Quaternion.identity, PlayerRef.None, spawnFlags, o => _dummy = o);

        if (_dummy.TryGetComponent(out NetworkMobBrain mobBrain)) {
          FusionPlayModeTestHelpers.PinMobBrainNoCombat(mobBrain);
        }

        if (_dummy.TryGetComponent(out NetworkTransform mobNt)) {
          mobNt.DisableSharedModeInterpolation = true;
        }

        yield return FusionPlayModeTestHelpers.WaitFrames(3);

        Assert.IsTrue(_player.TryGetComponent(out NetworkCombatController combat));
        Assert.IsTrue(_dummy.TryGetComponent(out Health dummyHealth));

        dummyHealth.AuthorityApplyStartingHealthIfUnset();
        yield return FusionPlayModeTestHelpers.WaitFrames(5);

        float startDummyHp = dummyHealth.NetworkedHealth;
        float fireballDmg  = SpellRegistry.Get(1).Damage;

        _session.InputRelay.TargetNetworkId = _dummy.Id;
        _session.InputRelay.PendingPulse    = FusionPlayModeSpellPulse.Spell1;

        yield return FusionPlayModeTestHelpers.WaitUntil(() => combat.PendingImpactSpellId != 0, 1200);

        int impactTick = combat.PendingImpactTick;
        yield return FusionPlayModeTestHelpers.WaitUntil(() => runner.Tick >= impactTick, 960,
          messageOnFail: $"impactWait tick={runner.Tick} need>={impactTick}");

        yield return FusionPlayModeTestHelpers.WaitUntil(() =>
            Mathf.Abs(dummyHealth.NetworkedHealth - (startDummyHp - fireballDmg)) < 0.36f,
          480,
          messageOnFail:
          $"post-impact hp={dummyHealth.NetworkedHealth} expected ~{startDummyHp - fireballDmg}");

        Assert.AreEqual(0, combat.PendingImpactSpellId);
      }
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator ProjectileSpell_DeadTarget_NoDelayedDamage_ClearPendingSafely() {
      var playerPrefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.PlayerCharacterPrefabPath);
      var dummyPrefab  = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.TrainingDummyPrefabPath);
      Assert.IsNotNull(playerPrefab);
      Assert.IsNotNull(dummyPrefab);

      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        var runner = _session.Runner;
        yield return FusionPlayModeTestHelpers.WaitFrames(5);

        var spawnPlayer = new Vector3(-2f, 1f, 0f);
        var spawnDummy  = new Vector3(6f, 0f, 6f);
        var spawnFlags  = NetworkSpawnFlags.SharedModeStateAuthLocalPlayer;

        yield return FusionPlayModeTestHelpers.SpawnPlayerPrefabBlocking(
          runner, playerPrefab, spawnPlayer, Quaternion.identity, spawnFlags, o => _player = o);
        runner.SetPlayerObject(runner.LocalPlayer, _player);

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner, dummyPrefab, spawnDummy, Quaternion.identity, PlayerRef.None, spawnFlags, o => _dummy = o);

        if (_dummy.TryGetComponent(out NetworkMobBrain mobBrain)) {
          FusionPlayModeTestHelpers.PinMobBrainNoCombat(mobBrain);
        }

        if (_dummy.TryGetComponent(out NetworkTransform mobNt)) {
          mobNt.DisableSharedModeInterpolation = true;
        }

        yield return FusionPlayModeTestHelpers.WaitFrames(3);

        Assert.IsTrue(_player.TryGetComponent(out NetworkCombatController combat));
        Assert.IsTrue(_dummy.TryGetComponent(out Health dummyHealth));

        dummyHealth.AuthorityApplyStartingHealthIfUnset();
        yield return FusionPlayModeTestHelpers.WaitFrames(5);

        float startHp = dummyHealth.NetworkedHealth;

        _session.InputRelay.TargetNetworkId = _dummy.Id;
        _session.InputRelay.PendingPulse    = FusionPlayModeSpellPulse.Spell1;

        yield return FusionPlayModeTestHelpers.WaitUntil(() => combat.PendingImpactSpellId != 0, 1200);
        int impactTick = combat.PendingImpactTick;

        dummyHealth.DealDamageRpc(startHp + 100f);
        yield return FusionPlayModeTestHelpers.WaitUntil(() => dummyHealth.IsDead, 240);

        Assert.AreEqual(0f, dummyHealth.NetworkedHealth, 0.01f);

        yield return FusionPlayModeTestHelpers.WaitUntil(() => runner.Tick > impactTick + 2, 600,
          messageOnFail: $"tick={runner.Tick} impactWas={impactTick}");

        Assert.IsTrue(dummyHealth.IsDead);
        Assert.AreEqual(0f, dummyHealth.NetworkedHealth, 0.01f,
          "Dead target should not accept delayed projectile damage.");
        Assert.AreEqual(0, combat.PendingImpactSpellId);
      }
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator ProjectileSpell_TargetMovesOutOfCastRange_StillDamagedByNetworkId() {
      var playerPrefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.PlayerCharacterPrefabPath);
      var dummyPrefab  = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.TrainingDummyPrefabPath);
      Assert.IsNotNull(playerPrefab);
      Assert.IsNotNull(dummyPrefab);

      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        var runner = _session.Runner;
        yield return FusionPlayModeTestHelpers.WaitFrames(5);

        var spawnPlayer = new Vector3(-2f, 1f, 0f);
        var spawnDummy  = new Vector3(6f, 0f, 6f);
        var spawnFlags  = NetworkSpawnFlags.SharedModeStateAuthLocalPlayer;

        yield return FusionPlayModeTestHelpers.SpawnPlayerPrefabBlocking(
          runner, playerPrefab, spawnPlayer, Quaternion.identity, spawnFlags, o => _player = o);
        runner.SetPlayerObject(runner.LocalPlayer, _player);

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner, dummyPrefab, spawnDummy, Quaternion.identity, PlayerRef.None, spawnFlags, o => _dummy = o);

        if (_dummy.TryGetComponent(out NetworkMobBrain mobBrain)) {
          FusionPlayModeTestHelpers.PinMobBrainNoCombat(mobBrain);
        }

        if (_dummy.TryGetComponent(out NetworkTransform mobNt)) {
          mobNt.DisableSharedModeInterpolation = true;
        }

        yield return FusionPlayModeTestHelpers.WaitFrames(3);

        Assert.IsTrue(_player.TryGetComponent(out NetworkCombatController combat));
        Assert.IsTrue(_dummy.TryGetComponent(out Health dummyHealth));

        dummyHealth.AuthorityApplyStartingHealthIfUnset();
        yield return FusionPlayModeTestHelpers.WaitFrames(5);

        float startHp = dummyHealth.NetworkedHealth;
        float dmg     = SpellRegistry.Get(1).Damage;

        _session.InputRelay.TargetNetworkId = _dummy.Id;
        _session.InputRelay.PendingPulse    = FusionPlayModeSpellPulse.Spell1;

        yield return FusionPlayModeTestHelpers.WaitUntil(() => combat.PendingImpactSpellId != 0, 1200);

        FusionPlayModeTestHelpers.TeleportNetworkObjectForPlayModeSmokeTest(_dummy, new Vector3(420f, 0f, 420f));

        yield return FusionPlayModeTestHelpers.WaitUntil(() =>
            Mathf.Abs(dummyHealth.NetworkedHealth - (startHp - dmg)) < 0.36f,
          960,
          messageOnFail:
          $"After distant teleport, dmg not applied hp={dummyHealth.NetworkedHealth} expect~{startHp - dmg}");

        Assert.IsFalse(dummyHealth.IsDead);
      }
    }
  }
}
