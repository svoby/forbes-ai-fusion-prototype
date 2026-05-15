using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Cosmetic-only observer: reads <see cref="ActiveSpellInstanceRegistry"/> on the
/// same GameObject and manages one local projectile visual per active TargetedProjectile instance.
/// Never applies damage, never writes networked state, not a NetworkBehaviour.
/// <para>
/// Visuals are keyed by <see cref="ActiveSpellInstance.InstanceId"/>, not by registry index.
/// When the same entry index is reused by a new spell, the old visual is destroyed and a
/// fresh one is created for the new InstanceId.
/// </para>
/// <para>
/// Visual position is reconstructed on first appearance (fast-forward from Origin through
/// elapsed ticks) then advanced each render frame using the same step model as the server.
/// Visual position is never networked.
/// </para>
/// </summary>
[RequireComponent(typeof(ActiveSpellInstanceRegistry))]
public class ActiveSpellInstancePresenter : MonoBehaviour {
  /// <summary>Maps a SpellId to the cosmetic prefab used for that spell's projectile visual.</summary>
  [System.Serializable]
  public struct ProjectileVisualEntry {
    public byte       SpellId;
    public GameObject Prefab;
  }

  const float VisualDiameter = 0.3f;
  internal const string ProjectileVisualName = "ProjectileVisual";

  /// <summary>
  /// Per-spell projectile prefabs. Each entry maps a SpellId to the prefab instantiated as
  /// its cosmetic projectile. Any colliders on the prefab are removed at runtime.
  /// If no entry matches the active spell, a primitive sphere fallback is used.
  /// </summary>
  [SerializeField] ProjectileVisualEntry[] _projectileVisuals = {};

  ActiveSpellInstanceRegistry _registry;
  NetworkRunner               _runner;

  // Both dictionaries keyed by InstanceId (not registry index).
  readonly Dictionary<int, GameObject> _activeVisuals   = new();
  readonly Dictionary<int, Vector3>    _visualPositions = new();
  readonly List<int>                   _toRemove        = new();

  void Awake() {
    _registry = GetComponent<ActiveSpellInstanceRegistry>();
  }

  void Start() {
    _runner = FindFirstObjectByType<NetworkRunner>();
  }

  void LateUpdate() {
    if (_registry == null || _runner == null) {
      return;
    }

    // Collect active InstanceIds currently in the registry.
    // Remove visuals for any InstanceId that is no longer present.
    _toRemove.Clear();
    foreach (var id in _activeVisuals.Keys) {
      if (!IsInstanceIdActive(id)) {
        _toRemove.Add(id);
      }
    }
    foreach (int id in _toRemove) {
      Destroy(_activeVisuals[id]);
      _activeVisuals.Remove(id);
      _visualPositions.Remove(id);
    }

    // Spawn visuals for newly active instances and advance all live visual positions.
    for (int i = 0; i < ActiveSpellInstanceRegistry.Capacity; i++) {
      var inst = _registry.Instances[i];
      if (!inst.IsActive || inst.InstanceId == 0) {
        continue;
      }

      var spell = SpellRegistry.Get(inst.SpellId);
      if (!SpellTravelLogic.HasProjectile(spell)) {
        continue;
      }

      if (!_activeVisuals.ContainsKey(inst.InstanceId)) {
        // New active instance: reconstruct starting position then create visual.
        if (!_runner.TryFindObject(inst.TargetId, out var initialTarget) || initialTarget == null) {
          continue;
        }

        var reconstructed = ReconstructVisualPosition(
          inst.Origin,
          initialTarget.transform.position,
          spell.ProjectileSpeedMetersPerSecond,
          _runner.DeltaTime,
          _runner.Tick - inst.ReleaseTick);

        _visualPositions[inst.InstanceId] = reconstructed;
        _activeVisuals[inst.InstanceId]   = CreateProjectileVisual(inst.SpellId);
      }
    }

    // Advance all live visual positions and apply to transforms.
    foreach (var kv in _activeVisuals) {
      AdvanceAndUpdateVisual(kv.Key, kv.Value);
    }
  }

  // Fast-forwards from origin through elapsedTicks simulation steps toward currentTargetPos.
  // Assumes the target was at its current position throughout (acceptable for a cosmetic visual).
  static Vector3 ReconstructVisualPosition(
    Vector3 origin, Vector3 currentTargetPos, float speed, float tickDt, int elapsedTicks) {
    var pos = origin;
    for (int t = 0; t < elapsedTicks; t++) {
      pos = SpellTravelLogic.AdvanceMissilePosition(pos, currentTargetPos, speed, tickDt);
    }

    return pos;
  }

  void AdvanceAndUpdateVisual(int instanceId, GameObject visual) {
    // Find the registry entry for this InstanceId.
    ActiveSpellInstance inst = default;
    bool found = false;
    for (int i = 0; i < ActiveSpellInstanceRegistry.Capacity; i++) {
      var candidate = _registry.Instances[i];
      if (candidate.IsActive && candidate.InstanceId == instanceId) {
        inst  = candidate;
        found = true;
        break;
      }
    }

    if (!found) {
      visual.SetActive(false);
      return;
    }

    if (!_runner.TryFindObject(inst.TargetId, out var targetObj) || targetObj == null) {
      visual.SetActive(false);
      return;
    }

    var   spell    = SpellRegistry.Get(inst.SpellId);
    float speed    = spell.ProjectileSpeedMetersPerSecond;
    var   targetPos = targetObj.transform.position;

    var current = _visualPositions[instanceId];
    current = SpellTravelLogic.AdvanceMissilePosition(current, targetPos, speed, Time.deltaTime);
    _visualPositions[instanceId] = current;

    visual.transform.position = current;
    visual.SetActive(true);
  }

  bool IsInstanceIdActive(int instanceId) {
    for (int i = 0; i < ActiveSpellInstanceRegistry.Capacity; i++) {
      var inst = _registry.Instances[i];
      if (inst.IsActive && inst.InstanceId == instanceId) {
        return true;
      }
    }

    return false;
  }

  GameObject CreateProjectileVisual(byte spellId) {
    var prefab = FindVisualPrefab(spellId);
    GameObject go;
    if (prefab != null) {
      go      = Instantiate(prefab);
      go.name = ProjectileVisualName;
      DisableAndDestroyCollidersInChildren(go);
    } else {
      go = CreatePrimitiveSphereVisual();
    }

    go.SetActive(false);
    return go;
  }

  // Disables every collider in the hierarchy synchronously before the deferred Destroy,
  // so the visual never participates in a physics step after creation.
  internal static void DisableAndDestroyCollidersInChildren(GameObject go) {
    foreach (var col in go.GetComponentsInChildren<Collider>()) {
      col.enabled = false;
      Destroy(col);
    }
  }

  GameObject FindVisualPrefab(byte spellId) {
    foreach (var entry in _projectileVisuals) {
      if (entry.SpellId == spellId) {
        return entry.Prefab;
      }
    }

    return null;
  }

  static GameObject CreatePrimitiveSphereVisual() {
    var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    sphere.name = ProjectileVisualName;
    sphere.transform.localScale = Vector3.one * VisualDiameter;

    DisableAndDestroyCollidersInChildren(sphere);

    sphere.GetComponent<Renderer>().material = SpellVisualColors.NewFireballOrbMaterial();
    return sphere;
  }

  void OnDestroy() {
    foreach (var kv in _activeVisuals) {
      if (kv.Value != null) {
        Destroy(kv.Value);
      }
    }
    _activeVisuals.Clear();
    _visualPositions.Clear();
  }
}
