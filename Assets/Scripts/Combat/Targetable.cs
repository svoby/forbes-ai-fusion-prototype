using Fusion;
using UnityEngine;

/// <summary>
/// Marker component that makes a networked object selectable by <see cref="TargetingController"/>.
/// Add to any prefab (player, training dummy) that should be clickable.
/// The <see cref="NetworkObject"/> sibling is required so the target can be identified
/// across the network by its <see cref="NetworkId"/>.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[DisallowMultipleComponent]
public class Targetable : MonoBehaviour {
  [Tooltip("Name shown in the HUD when this object is targeted. Defaults to the GameObject name.")]
  [SerializeField] string _displayName;

  NetworkObject _netObj;

  public NetworkObject NetworkObject => _netObj != null ? _netObj : (_netObj = GetComponent<NetworkObject>());

  /// <summary>Name displayed in the combat HUD when this target is selected.</summary>
  public string DisplayName => string.IsNullOrEmpty(_displayName) ? gameObject.name : _displayName;
}
