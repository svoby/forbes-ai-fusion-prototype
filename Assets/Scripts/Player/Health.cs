// Verbose combat / HP logs — delete this line to silence.
#define FORBES_HEALTH_LOG

using Fusion;
using UnityEngine;

/// <summary>
/// Networked HP, damage via RPC to State Authority, death and timed respawn at spawn point.
/// </summary>
public class Health : NetworkBehaviour {
  public float StartingHealth = 100f;
  public float RespawnDelaySeconds = 3f;

  [Networked, OnChangedRender(nameof(HealthChanged))]
  public float NetworkedHealth { get; set; }

  [Networked, OnChangedRender(nameof(DeadVisualChanged))]
  public NetworkBool IsDead { get; set; }

  /// <summary>Simulation tick on State Authority when the player should respawn (0 = not scheduled).</summary>
  [Networked]
  public int RespawnAtTick { get; set; }

  [Networked]
  public Vector3 SpawnPosition { get; set; }

  CharacterController _controller;
  MeshRenderer _renderer;

  void Awake() {
    _controller = GetComponent<CharacterController>();
    _renderer = GetComponent<MeshRenderer>();
  }

  void HealthChanged() {
#if FORBES_HEALTH_LOG
    Debug.Log($"[ForbesHealth] HP={NetworkedHealth:0.#} isDead={IsDead} obj={name}", this);
#endif
  }

  void DeadVisualChanged() {
    if (_renderer != null) {
      _renderer.enabled = !IsDead;
    }
  }

  public override void Spawned() {
    if (HasStateAuthority) {
      SpawnPosition = transform.position;
      NetworkedHealth = StartingHealth;
#if FORBES_HEALTH_LOG
      Debug.Log($"[ForbesHealth] Spawned authority spawnPos={SpawnPosition} startHP={StartingHealth} obj={name}", this);
#endif
    }
  }

  public override void FixedUpdateNetwork() {
    if (!HasStateAuthority || !IsDead || RespawnAtTick == 0) {
      return;
    }

    if (Runner.Tick >= RespawnAtTick) {
      Respawn();
    }
  }

  void Respawn() {
#if FORBES_HEALTH_LOG
    Debug.Log($"[ForbesHealth] Respawn at {SpawnPosition} obj={name}", this);
#endif
    if (_controller != null) {
      _controller.enabled = false;
    }

    transform.position = SpawnPosition;
    if (_controller != null) {
      _controller.enabled = true;
    }

    NetworkedHealth = StartingHealth;
    IsDead = false;
    RespawnAtTick = 0;
  }

  [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
  public void DealDamageRpc(float damage) {
    if (!HasStateAuthority || IsDead) {
      return;
    }

#if FORBES_HEALTH_LOG
    Debug.Log($"[ForbesHealth] DealDamageRpc dmg={damage} before={NetworkedHealth} obj={name}", this);
#endif
    NetworkedHealth = Mathf.Max(0f, NetworkedHealth - damage);
    if (NetworkedHealth <= 0f) {
      IsDead = true;
      int delayTicks = Mathf.CeilToInt(RespawnDelaySeconds * Runner.TickRate);
      RespawnAtTick = Runner.Tick + Mathf.Max(1, delayTicks);
#if FORBES_HEALTH_LOG
      Debug.Log($"[ForbesHealth] Killed respawnAtTick={RespawnAtTick} obj={name}", this);
#endif
    }
  }
}
