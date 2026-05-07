using System.Collections;
using Fusion;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Editor-only test scaffolding. After the local player is spawned by
/// <see cref="PlayerSpawner"/>, places networked training mobs near the player.
/// Compiles in builds (so scene references stay valid) but is a no-op
/// outside the Editor.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerSpawner))]
public class TrainingDummySpawner : MonoBehaviour {
#if UNITY_EDITOR
  const string DefaultPrefabAssetPath = "Assets/TrainingDummy.prefab";
#endif

  [Tooltip("Editor / solo test: static target with Health (no input). Spawned once after the local player joins.")]
  [SerializeField] GameObject TrainingDummyPrefab;

  [SerializeField] bool spawnInEditor = true;

  [Tooltip("Offset in the local player's space (X=right, Y=up, Z=forward). Default is straight ahead so FP camera sees the dummy.")]
  [SerializeField] Vector3 spawnOffsetLocal = new Vector3(0f, 0f, 4f);

  [Tooltip("Editor / solo test: spawn a second training dummy configured as a Fireball caster mob.")]
  [SerializeField] bool spawnCasterMobInEditor = true;

  [Tooltip("Offset in the local player's space for the caster mob. Placed beside the regular dummy so it has a nearby target.")]
  [SerializeField] Vector3 casterSpawnOffsetLocal = new Vector3(8f, 0f, 4f);

  NetworkRunner _runner;
  PlayerSpawner _spawner;
  bool _spawned;
  bool _spawnStarted;

  void Awake() {
    _runner = GetComponent<NetworkRunner>();
    _spawner = GetComponent<PlayerSpawner>();

#if UNITY_EDITOR
    if (Application.isEditor && TrainingDummyPrefab == null) {
      TrainingDummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPrefabAssetPath);
      if (TrainingDummyPrefab != null) {
        EditorUtility.SetDirty(this);
        ForbesLog.Net($"TrainingDummySpawner restored prefab from {DefaultPrefabAssetPath}");
      }
    }
#endif

    if (_spawner != null) {
      _spawner.LocalPlayerSpawned += OnLocalPlayerSpawned;
    }
  }

  void OnDestroy() {
    if (_spawner != null) {
      _spawner.LocalPlayerSpawned -= OnLocalPlayerSpawned;
    }
  }

  void OnLocalPlayerSpawned() {
    if (!Application.isEditor || !spawnInEditor) {
      return;
    }

    if (_spawned || _spawnStarted) {
      return;
    }

    if (_runner == null || TrainingDummyPrefab == null) {
      return;
    }

    _spawnStarted = true;
    StartCoroutine(CoSpawnTrainingDummies());
  }

  IEnumerator CoSpawnTrainingDummies() {
    if (TrainingDummyPrefab.GetComponent<NetworkObject>() == null) {
      ForbesLog.Error("TrainingDummySpawner: TrainingDummyPrefab must be a root object with a NetworkObject.");
      _spawnStarted = false;
      yield break;
    }

    const int maxWaitFrames = 32;
    for (var frame = 0; frame < maxWaitFrames; frame++) {
      yield return null;

      if (!Application.isEditor || !spawnInEditor || _runner == null || !_runner.IsRunning) {
        _spawnStarted = false;
        yield break;
      }

      if (!_runner.TryGetPlayerObject(_runner.LocalPlayer, out var playerObj) || playerObj == null) {
        continue;
      }

      var worldPos = playerObj.transform.TransformPoint(spawnOffsetLocal);
      worldPos.y = Mathf.Max(worldPos.y, 0.5f);
      var rot = playerObj.transform.rotation;

      var spawnedDummy = _runner.Spawn(
        TrainingDummyPrefab,
        worldPos,
        rot,
        PlayerRef.None,
        null,
        NetworkSpawnFlags.SharedModeStateAuthLocalPlayer);

      if (spawnedDummy != null) {
        if (spawnCasterMobInEditor) {
          SpawnCasterMob(playerObj);
        }

        _spawned = true;
        ForbesLog.Net($"TrainingDummy spawned name={spawnedDummy.name} id={spawnedDummy.Id}");
        yield break;
      }

      ForbesLog.Error("TrainingDummySpawner: Spawn returned null. Check FusionPrefab label on TrainingDummy and Shared Mode spawn rules.");
      _spawnStarted = false;
      yield break;
    }

    ForbesLog.Warn("TrainingDummySpawner: training dummy not spawned (local player object not ready in time).");
    _spawnStarted = false;
  }

  void SpawnCasterMob(NetworkObject playerObj) {
    if (TrainingDummyPrefab.GetComponent<NetworkCombatController>() == null) {
      ForbesLog.Error("TrainingDummySpawner: caster mob requires NetworkCombatController on TrainingDummyPrefab.");
      return;
    }

    var worldPos = playerObj.transform.TransformPoint(casterSpawnOffsetLocal);
    worldPos.y = Mathf.Max(worldPos.y, 0.5f);
    var rot = playerObj.transform.rotation;

    var spawnedCaster = _runner.Spawn(
      TrainingDummyPrefab,
      worldPos,
      rot,
      PlayerRef.None,
      null,
      NetworkSpawnFlags.SharedModeStateAuthLocalPlayer);

    if (spawnedCaster == null) {
      ForbesLog.Error("TrainingDummySpawner: caster mob spawn returned null.");
      return;
    }

    spawnedCaster.name = "Caster Mob";

    if (spawnedCaster.TryGetComponent(out NetworkMobBrain brain)) {
      brain.CombatMode = NetworkMobBrainCombatMode.Caster;
      brain.CasterSpellId = 1;
      brain.AggroRadius = 30f;
      brain.LeashRadius = 40f;
    }

    ForbesLog.Net($"Caster mob spawned name={spawnedCaster.name} id={spawnedCaster.Id}");
  }
}
