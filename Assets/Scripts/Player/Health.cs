using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Networked HP, damage RPC routed to State Authority, death and timed respawn at
/// the spawn point. Pure state and authority logic; presentation lives in
/// <see cref="HealthView"/>, which subscribes to <see cref="IsDeadChanged"/>.
/// </summary>
public class Health : NetworkBehaviour {
  public float StartingHealth = 100f;
  public float RespawnDelaySeconds = 3f;

  [Networked]
  public float NetworkedHealth { get; set; }

  [Networked, OnChangedRender(nameof(OnIsDeadChangedRender))]
  public NetworkBool IsDead { get; set; }

  /// <summary>Simulation tick on State Authority when the player should respawn (0 = not scheduled).</summary>
  [Networked]
  public int RespawnAtTick { get; set; }

  [Networked]
  public Vector3 SpawnPosition { get; set; }

  /// <summary>Render-side notification fired by Fusion when <see cref="IsDead"/> changes.</summary>
  public event Action<bool> IsDeadChanged;

  CharacterController _controller;

  void Awake() {
    _controller = GetComponent<CharacterController>();
  }

  void OnIsDeadChangedRender() {
    ForbesLog.Health($"IsDead -> {IsDead} obj={name}", this);
    IsDeadChanged?.Invoke(IsDead);
  }

  public override void Spawned() {
    if (HasStateAuthority) {
      SpawnPosition = transform.position;
      NetworkedHealth = StartingHealth;
      ForbesLog.Health($"Spawned authority spawnPos={SpawnPosition} startHP={StartingHealth} obj={name}", this);
    }

    // Networked props are now safe to read; let view subscribers (HealthView) apply the initial value.
    IsDeadChanged?.Invoke(IsDead);
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
    ForbesLog.Health($"Respawn at {SpawnPosition} obj={name}", this);

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

    ForbesLog.Health($"DealDamageRpc dmg={damage} before={NetworkedHealth} obj={name}", this);
    NetworkedHealth = Mathf.Max(0f, NetworkedHealth - damage);
    if (NetworkedHealth <= 0f) {
      IsDead = true;
      int delayTicks = Mathf.CeilToInt(RespawnDelaySeconds * Runner.TickRate);
      RespawnAtTick = Runner.Tick + Mathf.Max(1, delayTicks);
      ForbesLog.Health($"Killed respawnAtTick={RespawnAtTick} obj={name}", this);
    }
  }
}
