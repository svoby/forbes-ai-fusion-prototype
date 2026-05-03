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

  CharacterController _controller;
  Health _health;
  Vector3 _spawnPosition;
  Vector3 _velocity;
  Vector3 _destination;
  NetworkMobBrainState _state;
  int _idleUntilTick;

  public override void Spawned() {
    _controller = GetComponent<CharacterController>();
    _health = GetComponent<Health>();

    if (!HasStateAuthority) {
      return;
    }

    _spawnPosition = transform.position;
    _state = NetworkMobBrainState.Idle;
    _idleUntilTick = Runner.Tick;
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
