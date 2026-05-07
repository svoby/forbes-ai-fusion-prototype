using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Cosmetic-only observer: reads <see cref="ActiveSpellInstanceRegistry"/> on the
/// same GameObject and manages one local fireball sphere per active TargetedProjectile slot.
/// Never applies damage, never writes networked state, not a NetworkBehaviour.
/// </summary>
[RequireComponent(typeof(ActiveSpellInstanceRegistry))]
public class ActiveSpellInstancePresenter : MonoBehaviour {
  const float  VisualDiameter    = 0.3f;
  internal const string FireballVisualName = "FireballVisual";

  ActiveSpellInstanceRegistry _registry;
  NetworkRunner               _runner;

  readonly Dictionary<int, GameObject> _activeVisuals = new();
  readonly List<int>                   _toRemove      = new();

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

    // Remove visuals for completed (inactive) slots.
    _toRemove.Clear();
    foreach (var kv in _activeVisuals) {
      if (!_registry.Instances[kv.Key].IsActive) {
        _toRemove.Add(kv.Key);
      }
    }
    foreach (int idx in _toRemove) {
      Destroy(_activeVisuals[idx]);
      _activeVisuals.Remove(idx);
    }

    // Spawn visuals for newly active slots with a projectile spell.
    for (int i = 0; i < ActiveSpellInstanceRegistry.Capacity; i++) {
      var inst = _registry.Instances[i];
      if (!inst.IsActive || _activeVisuals.ContainsKey(i)) {
        continue;
      }
      var spell = SpellRegistry.Get(inst.SpellId);
      if (!SpellTravelLogic.HasProjectile(spell)) {
        continue;
      }
      _activeVisuals[i] = CreateFireballVisual();
    }

    // Update position of all live visuals.
    foreach (var kv in _activeVisuals) {
      UpdateVisualPosition(kv.Key, kv.Value);
    }
  }

  void UpdateVisualPosition(int slotIndex, GameObject visual) {
    var inst = _registry.Instances[slotIndex];
    if (!_runner.TryFindObject(inst.TargetId, out var targetObj) || targetObj == null) {
      visual.SetActive(false);
      return;
    }

    var   spell   = SpellRegistry.Get(inst.SpellId);
    float dist    = Vector3.Distance(inst.Origin, targetObj.transform.position);
    float elapsed = (_runner.Tick - inst.ReleaseTick) * _runner.DeltaTime;
    float t       = dist > 0.001f
      ? Mathf.Clamp01(elapsed * spell.ProjectileSpeedMetersPerSecond / dist)
      : 1f;

    visual.transform.position = Vector3.Lerp(inst.Origin, targetObj.transform.position, t);
    visual.SetActive(true);
  }

  static GameObject CreateFireballVisual() {
    var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    sphere.name = FireballVisualName;
    sphere.transform.localScale = Vector3.one * VisualDiameter;

    if (sphere.TryGetComponent<Collider>(out var col)) {
      Destroy(col);
    }

    sphere.GetComponent<Renderer>().material = SpellVisualColors.NewFireballOrbMaterial();
    sphere.SetActive(false);
    return sphere;
  }

  void OnDestroy() {
    foreach (var kv in _activeVisuals) {
      if (kv.Value != null) {
        Destroy(kv.Value);
      }
    }
    _activeVisuals.Clear();
  }
}
