using System.Collections;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;
using UnityEngine.TestTools;

namespace Forbes.Tests.PlayMode {
  /// <summary>
  /// Fusion single-player smoke: opt-in caster mob intent requests Fireball through
  /// <see cref="NetworkCombatController"/> instead of applying damage itself.
  /// </summary>
  [TestFixture]
  public class NetworkMobBrainCasterSmokeTests {
    FusionSinglePlayerTestSession _session;
    NetworkObject                 _caster;
    NetworkObject                 _victim;

    [SetUp]
    public void SetUp() {
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
      _session = new FusionSinglePlayerTestSession();
      _caster  = null;
      _victim  = null;
    }

    [TearDown]
    public void TearDown() {
      PlayModeTargetingCleanup.DestroyAutoCreatedTargetingSystem();
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator FusionSingle_CasterMob_RequestsFireball_ThroughCombatController() {
      yield return FusionPlayModeTestHelpers.RunWithFusionSession(_session, Body);

      IEnumerator Body() {
        NetworkRunner runner = _session.Runner;
        Assert.IsNotNull(runner);
        yield return FusionPlayModeTestHelpers.WaitFrames(5);

        // Fusion TrySpawn requires prefabs listed in NetworkProjectConfig PrefabTable.
        // Runtime-built GameObjects are not valid spawn prefabs (PrefabTable miss).
        GameObject dummyPrefab = FusionPlayModeTestAssets.LoadPrefab(FusionPlayModeTestAssets.TrainingDummyPrefabPath);
        Assert.IsNotNull(dummyPrefab, "TrainingDummy.prefab missing.");

        var spawnFlags = NetworkSpawnFlags.SharedModeStateAuthLocalPlayer;
        var casterSpawn = Vector3.zero;
        var victimFar   = new Vector3(80f, 0f, 0f);
        var victimNear  = new Vector3(10f, 0f, 0f);

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner,
          dummyPrefab,
          casterSpawn,
          Quaternion.identity,
          PlayerRef.None,
          spawnFlags,
          o => {
            _caster = o;
            ConfigureCaster(o.GetComponent<NetworkMobBrain>());
          });

        yield return FusionPlayModeTestHelpers.SpawnPrefabBlocking(
          runner,
          dummyPrefab,
          victimFar,
          Quaternion.identity,
          PlayerRef.None,
          spawnFlags,
          o => {
            _victim = o;
            FusionPlayModeTestHelpers.PinMobBrainNoCombat(o.GetComponent<NetworkMobBrain>());
          });

        var casterBrain  = _caster.GetComponent<NetworkMobBrain>();
        var casterCombat = _caster.GetComponent<NetworkCombatController>();
        var casterHealth = _caster.GetComponent<Health>();
        var victimHealth = _victim.GetComponent<Health>();

        Assert.IsNotNull(casterBrain);
        Assert.IsNotNull(casterCombat, "Caster mob must use the existing NetworkCombatController runtime.");
        Assert.IsNotNull(_caster.GetComponent<ActiveSpellInstanceRegistry>(),
          "Caster needs ActiveSpellInstanceRegistry for projectile simulation.");
        Assert.IsTrue(casterBrain.HasStateAuthority, "Caster mob intent must run on State Authority.");
        Assert.IsTrue(casterCombat.HasStateAuthority, "Combat requests must be made by State Authority.");

        casterHealth.AuthorityApplyStartingHealthIfUnset();
        victimHealth.AuthorityApplyStartingHealthIfUnset();
        yield return FusionPlayModeTestHelpers.WaitFrames(8);

        float victimHpBeforeCast = victimHealth.NetworkedHealth;
        float casterHpBeforeCast = casterHealth.NetworkedHealth;

        FusionPlayModeTestHelpers.TeleportNetworkObjectForPlayModeSmokeTest(_victim, victimNear);
        if (_victim.TryGetComponent(out NetworkMobBrain victimBrain)) {
          victimBrain.RefreshWanderOriginAuthority();
        }

        yield return FusionPlayModeTestHelpers.WaitUntilLazy(
          () => casterCombat.CurrentSpellId == 1 && casterCombat.IsCasting,
          600,
          () =>
            $"Caster did not start Fireball. spell={casterCombat.CurrentSpellId} " +
            $"isCasting={casterCombat.IsCasting} target={casterCombat.CastTarget.Raw}");

        Assert.AreEqual(_victim.Id, casterCombat.CastTarget,
          "Caster intent should pass the acquired target NetworkId into TryRequestCast.");
        Assert.LessOrEqual(
          NetworkMobBrainLogic.HorizontalSqrDistance(casterSpawn, _caster.transform.position),
          0.1f * 0.1f,
          "Caster should hold position while casting.");

        float expectedHp = victimHpBeforeCast - SpellRegistry.Get(1).Damage;
        yield return FusionPlayModeTestHelpers.WaitUntilLazy(
          () => Mathf.Abs(victimHealth.NetworkedHealth - expectedHp) < 0.5f,
          1200,
          () =>
            $"Fireball damage did not arrive. victimHP={victimHealth.NetworkedHealth} " +
            $"expected~={expectedHp} registryHasInstance=" +
            $"{_caster.GetComponent<ActiveSpellInstanceRegistry>()?.HasActiveInstanceForCaster(_caster.Id)}");

        Assert.AreEqual(casterHpBeforeCast, casterHealth.NetworkedHealth, 1e-2f,
          "Caster mob should not damage itself.");
        Assert.AreEqual(expectedHp, victimHealth.NetworkedHealth, 0.5f,
          "Caster mob should apply Fireball damage through NetworkCombatController, not melee damage.");
      }
    }

    static void ConfigureCaster(NetworkMobBrain brain) {
      Assert.IsNotNull(brain);
      brain.CombatMode     = NetworkMobBrainCombatMode.Caster;
      brain.CasterSpellId  = 1;
      brain.WanderRadius   = 0f;
      brain.WalkSpeed      = 0f;
      brain.RunSpeed       = 0f;
      brain.MinLegDistance = 0f;
      brain.IdleTicksMin   = 1;
      brain.IdleTicksMax   = 1;
      brain.AggroRadius    = 15f;
      brain.LeashRadius    = 40f;

      // If caster mode accidentally falls through to melee, this will make the
      // final HP assertion fail loudly instead of matching Fireball damage.
      brain.AttackRange           = 30f;
      brain.AttackDamage          = 99f;
      brain.AttackIntervalSeconds = 0.05f;
    }
  }
}
