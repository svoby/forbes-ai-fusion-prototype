using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Networked HP, damage RPC routed to State Authority, death and timed respawn at
/// the spawn point; optional void kill when world Y falls below <see cref="FallKillBelowWorldY"/>.
/// Pure state and authority logic; presentation lives in <see cref="HealthView"/>,
/// which subscribes to <see cref="IsDeadChanged"/>.
/// </summary>
public class Health : NetworkBehaviour {
  public float StartingHealth = 100f;
  public float RespawnDelaySeconds = 3f;

  /// <summary>If true, State Authority kills the entity when world-space Y drops below <see cref="FallKillBelowWorldY"/>.</summary>
  public bool FallKillEnabled = true;

  /// <summary>World-space vertical limit (axis Y). When <c>transform.position.y</c> is below this, the player dies and respawns like lethal damage.</summary>
  public float FallKillBelowWorldY = -50f;

  /// <summary>Added to respawn Y so the CharacterController is not embedded in the floor after teleport.</summary>
  public float RespawnVerticalNudge = 0.1f;

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

  /// <summary>Authority-only: one-shot apply requested by tests when Editor spawn order leaves HP at default.</summary>
  bool _applyStartingHealthIfUnsetDue;

  /// <summary>Authority-only: record spawn position this tick after NetworkTransform applied (see <see cref="Spawned"/>).</summary>
  int _spawnRecordDueTick = -1;

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
      _spawnRecordDueTick = Runner != null ? Runner.Tick + 1 : -1;
      ForbesLog.Health($"Spawned authority spawnPos={SpawnPosition} startHP={StartingHealth} obj={name}", this);
    }

    // Networked props are now safe to read; let view subscribers (HealthView) apply the initial value.
    IsDeadChanged?.Invoke(IsDead);
  }

  /// <summary>
  /// State Authority: schedule aligning HP with <see cref="StartingHealth"/> on the next simulation tick
  /// if it is still unset (Editor PlayMode quirk with some spawns). Does not resurrect real deaths (respawn tick scheduled).
  /// </summary>
  public void AuthorityApplyStartingHealthIfUnset() {
    if (!HasStateAuthority) {
      return;
    }

    _applyStartingHealthIfUnsetDue = true;
  }

  /// <summary>
  /// State authority only. Restores <see cref="NetworkedHealth"/> to <see cref="StartingHealth"/> while alive (PlayMode smoke stability).
  /// </summary>
  internal void AuthorityResetNetworkedHealthToStartingForTests() {
    if (!HasStateAuthority || IsDead) {
      return;
    }

    NetworkedHealth = StartingHealth;
  }

  public override void FixedUpdateNetwork() {
    if (!HasStateAuthority) {
      return;
    }

    if (_applyStartingHealthIfUnsetDue) {
      _applyStartingHealthIfUnsetDue = false;
      if (StartingHealth > 0f &&
          NetworkedHealth <= 0.001f &&
          !(IsDead && RespawnAtTick > 0)) {
        ForbesLog.Health($"AuthorityApplyStartingHealthIfUnset tick={Runner.Tick} -> {StartingHealth} obj={name}", this);
        NetworkedHealth = StartingHealth;
        if (IsDead) {
          IsDead = false;
          RespawnAtTick = 0;
        }
      }
    }

    if (_spawnRecordDueTick >= 0 && Runner.Tick >= _spawnRecordDueTick) {
      SpawnPosition = transform.position;
      _spawnRecordDueTick = -1;
      ForbesLog.Health($"SpawnPosition finalized after NT spawn tick={Runner.Tick} pos={SpawnPosition} obj={name}", this);
    }

    if (!IsDead && FallKillEnabled && transform.position.y < FallKillBelowWorldY) {
      ForbesLog.Health($"FallKill y={transform.position.y:F2} below={FallKillBelowWorldY} obj={name}", this);
      ApplyDeathAuthority();
      return;
    }

    if (!IsDead || RespawnAtTick == 0) {
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

    Vector3 pos = SpawnPosition;
    pos.y += Mathf.Max(0f, RespawnVerticalNudge);
    Quaternion rot = transform.rotation;

    if (TryGetComponent<NetworkTransform>(out var netTransform)) {
      netTransform.Teleport(pos, rot);
    } else {
      transform.SetPositionAndRotation(pos, rot);
    }

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
      ApplyDeathAuthority();
    }
  }

  void ApplyDeathAuthority() {
    if (!HasStateAuthority || IsDead) {
      return;
    }

    NetworkedHealth = 0f;
    IsDead = true;
    int delayTicks = Mathf.CeilToInt(RespawnDelaySeconds * Runner.TickRate);
    RespawnAtTick = Runner.Tick + Mathf.Max(1, delayTicks);
    ForbesLog.Health($"Killed respawnAtTick={RespawnAtTick} obj={name}", this);
  }
}
