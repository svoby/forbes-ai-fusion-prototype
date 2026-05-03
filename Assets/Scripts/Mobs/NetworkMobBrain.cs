using Fusion;
using UnityEngine;

/// <summary>
/// Minimal authority-only wander on the XZ plane. Position replication uses
/// <see cref="NetworkTransform"/> on the same <see cref="NetworkObject"/>.
/// </summary>
public class NetworkMobBrain : NetworkBehaviour {
  const float GravityValue = -9.81f;

  public float WanderRadius = 8f;
  public float MoveSpeed = 6f;
  public float ArrivalThreshold = 0.3f;
  public float MinLegDistance = 1f;
  public int IdleTicksMin = 8;
  public int IdleTicksMax = 24;

  public float AttackRange = 2f;
  public float AttackDamage = 10f;
  public float AttackIntervalSeconds = 1.5f;
  public float AggroRadius = 6f;

  CharacterController _controller;
  Health _health;
  Vector3 _spawnPosition;
  Vector3 _velocity;
  Vector3 _destination;
  NetworkMobBrainState _state;
  int _idleUntilTick;
  int _nextAttackTick;

  /// <summary>Authority: record wander origin on tick+1 so <see cref="NetworkTransform"/> spawn pose is applied first.</summary>
  int _spawnRecordDueTick = -1;

  public override void Spawned() {
    _controller = GetComponent<CharacterController>();
    _health = GetComponent<Health>();

    if (!HasStateAuthority) {
      return;
    }

    _spawnRecordDueTick = Runner != null ? Runner.Tick + 1 : -1;
    _state = NetworkMobBrainState.Idle;
    _idleUntilTick = Runner.Tick;
    _nextAttackTick = Runner.Tick;
  }

  /// <summary>
  /// State Authority: call after an external teleport (e.g. respawn) so wander origin and melee distance use the new pose.
  /// </summary>
  public void RefreshWanderOriginAuthority() {
    if (!HasStateAuthority) {
      return;
    }
    _spawnPosition = transform.position;
  }

  public override void FixedUpdateNetwork() {
    if (!HasStateAuthority || _controller == null) {
      return;
    }

    if (_health != null && _health.IsDead) {
      return;
    }

    float dt = Runner.DeltaTime;

    if (_controller.isGrounded) {
      _velocity = new Vector3(0f, -1f, 0f);
    }

    _velocity.y += GravityValue * dt;

    if (_spawnRecordDueTick >= 0 && Runner.Tick >= _spawnRecordDueTick) {
      _spawnPosition = transform.position;
      _spawnRecordDueTick = -1;
    }

    if (_spawnRecordDueTick >= 0) {
      _controller.Move(_velocity * dt);
      return;
    }

    switch (_state) {
      case NetworkMobBrainState.Idle:
        if (NetworkMobBrainLogic.ShouldLeaveIdle(_state, Runner.Tick, _idleUntilTick)) {
          PickNewDestination();
          _state = NetworkMobBrainState.Wander;
          goto case NetworkMobBrainState.Wander;
        }

        _controller.Move(_velocity * dt);
        break;

      case NetworkMobBrainState.Wander:
        var pos = transform.position;
        if (NetworkMobBrainLogic.HasArrivedHorizontally(pos, _destination, ArrivalThreshold)) {
          _state = NetworkMobBrainState.Idle;
          int span = Mathf.Max(0, IdleTicksMax - IdleTicksMin);
          int jitter = span > 0 ? Random.Range(0, span + 1) : 0;
          _idleUntilTick = Runner.Tick + Mathf.Max(1, IdleTicksMin + jitter);
          break;
        }

        if (NetworkMobBrainLogic.TryGetHorizontalDirection(pos, _destination, out var dir)) {
          transform.rotation = NetworkMobBrainLogic.RotationFacingHorizontal(dir, transform.rotation);
          Vector3 planar = dir * (MoveSpeed * dt);
          _controller.Move(planar + _velocity * dt);
        } else {
          _controller.Move(_velocity * dt);
        }

        break;
    }

    TryMeleeAuthority();
  }

  void TryMeleeAuthority() {
    if (!NetworkMobBrainLogic.CanAttackAtTick(Runner.Tick, _nextAttackTick)) {
      return;
    }

    Vector3 pos = transform.position;
    float attackR = Mathf.Max(0f, AttackRange);
    float considerR = Mathf.Max(Mathf.Max(0f, AggroRadius), attackR);
    Health best = null;
    float bestSqr = float.MaxValue;
    Health[] candidates = UnityEngine.Object.FindObjectsByType<Health>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    for (var i = 0; i < candidates.Length; i++) {
      Health h = candidates[i];
      if (h == null || h == _health) {
        continue;
      }

      if (h.Object != null && !h.Object.IsValid) {
        continue;
      }

      if (h.IsDead) {
        continue;
      }

      Vector3 hp = h.transform.position;
      if (!NetworkMobBrainLogic.IsWithinHorizontalRange(pos, hp, considerR)) {
        continue;
      }

      if (!NetworkMobBrainLogic.IsWithinHorizontalRange(pos, hp, attackR)) {
        continue;
      }

      float sqr = NetworkMobBrainLogic.HorizontalSqrDistance(pos, hp);
      if (sqr < bestSqr) {
        bestSqr = sqr;
        best = h;
      }
    }

    if (best == null) {
      return;
    }

    best.DealDamageRpc(AttackDamage);
    int cooldownTicks = NetworkMobBrainLogic.SecondsToTicks(AttackIntervalSeconds, Runner.TickRate);
    _nextAttackTick = Runner.Tick + cooldownTicks;
  }

  void PickNewDestination() {
    const int maxAttempts = 12;
    Vector3 pos = transform.position;

    for (var attempt = 0; attempt < maxAttempts; attempt++) {
      Vector3 candidate = NetworkMobBrainLogic.PickDestinationXZ(
        _spawnPosition,
        WanderRadius,
        Random.value,
        Random.value);

      if (NetworkMobBrainLogic.HorizontalSqrDistance(pos, candidate) >= MinLegDistance * MinLegDistance) {
        _destination = candidate;
        return;
      }
    }

    _destination = NetworkMobBrainLogic.PickDestinationXZ(_spawnPosition, WanderRadius, Random.value, Random.value);
  }
}
