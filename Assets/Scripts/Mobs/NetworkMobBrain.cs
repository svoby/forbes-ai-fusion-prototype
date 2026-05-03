using Fusion;
using UnityEngine;

/// <summary>
/// Authority-only wander / chase / leash on the XZ plane. Position replication uses
/// <see cref="NetworkTransform"/> on the same <see cref="NetworkObject"/>.
/// </summary>
public class NetworkMobBrain : NetworkBehaviour {
  const float GravityValue = -9.81f;

  public float WanderRadius = 8f;
  /// <summary>Wander / idle movement speed (half of run by default).</summary>
  public float WalkSpeed = 3f;
  /// <summary>Chase and leash-return movement speed.</summary>
  public float RunSpeed = 6f;
  public float ArrivalThreshold = 0.3f;
  public float MinLegDistance = 1f;
  public int IdleTicksMin = 8;
  public int IdleTicksMax = 24;

  public float LeashRadius = 12f;
  public float StopDistanceBuffer = 0.15f;

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
  NetworkId _currentTargetId;

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
    _currentTargetId = default;
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

    if (_state == NetworkMobBrainState.Idle || _state == NetworkMobBrainState.Wander) {
      if (TryAcquireAggroTargetAuthority()) {
        _state = NetworkMobBrainState.Chase;
      }
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
        var posW = transform.position;
        if (NetworkMobBrainLogic.HasArrivedHorizontally(posW, _destination, ArrivalThreshold)) {
          _state = NetworkMobBrainState.Idle;
          int span = Mathf.Max(0, IdleTicksMax - IdleTicksMin);
          int jitter = span > 0 ? Random.Range(0, span + 1) : 0;
          _idleUntilTick = Runner.Tick + Mathf.Max(1, IdleTicksMin + jitter);
          _controller.Move(_velocity * dt);
          break;
        }

        if (NetworkMobBrainLogic.TryGetHorizontalDirection(posW, _destination, out var dirW)) {
          transform.rotation = NetworkMobBrainLogic.RotationFacingHorizontal(dirW, transform.rotation);
          float wanderSpeed = NetworkMobBrainLogic.SelectMobSpeed(_state, WalkSpeed, RunSpeed);
          Vector3 planarW = dirW * (wanderSpeed * dt);
          _controller.Move(planarW + _velocity * dt);
        } else {
          _controller.Move(_velocity * dt);
        }

        break;

      case NetworkMobBrainState.Chase:
        TickChaseAuthority(dt);
        break;

      case NetworkMobBrainState.Return:
        TickReturnAuthority(dt);
        break;
    }

    if (_state != NetworkMobBrainState.Return) {
      TryMeleeAuthority();
    }
  }

  void BeginReturnAuthority() {
    _currentTargetId = default;
    _state = NetworkMobBrainState.Return;
  }

  void TickChaseAuthority(float dt) {
    if (!TryResolveChaseTargetAuthority(out Health target)) {
      BeginReturnAuthority();
      _controller.Move(_velocity * dt);
      return;
    }

    Vector3 mobPos = transform.position;
    Vector3 tpos = target.transform.position;
    bool alive = !target.IsDead &&
                 target.Object != null &&
                 target.Object.IsValid;

    if (NetworkMobBrainLogic.ShouldAbortChaseAndReturn(_spawnPosition, mobPos, tpos, LeashRadius, alive)) {
      BeginReturnAuthority();
      _controller.Move(_velocity * dt);
      return;
    }

    float attackR = Mathf.Max(0f, AttackRange);
    bool holdPosition = attackR <= Mathf.Epsilon
      ? NetworkMobBrainLogic.HorizontalSqrDistance(mobPos, tpos) <= 1e-6f
      : NetworkMobBrainLogic.IsWithinHorizontalRange(mobPos, tpos, attackR);

    if (holdPosition) {
      _controller.Move(_velocity * dt);
      return;
    }

    if (NetworkMobBrainLogic.TryGetHorizontalDirection(mobPos, tpos, out var dirC)) {
      transform.rotation = NetworkMobBrainLogic.RotationFacingHorizontal(dirC, transform.rotation);
      float speed = NetworkMobBrainLogic.SelectMobSpeed(_state, WalkSpeed, RunSpeed);
      Vector3 planarC = dirC * (speed * dt);
      _controller.Move(planarC + _velocity * dt);
    } else {
      _controller.Move(_velocity * dt);
    }
  }

  void TickReturnAuthority(float dt) {
    Vector3 pos = transform.position;
    float homeTh = ArrivalThreshold + Mathf.Max(0f, StopDistanceBuffer);
    if (NetworkMobBrainLogic.HasArrivedHorizontally(pos, _spawnPosition, homeTh)) {
      _state = NetworkMobBrainState.Idle;
      _currentTargetId = default;
      int span = Mathf.Max(0, IdleTicksMax - IdleTicksMin);
      int jitter = span > 0 ? Random.Range(0, span + 1) : 0;
      _idleUntilTick = Runner.Tick + Mathf.Max(1, IdleTicksMin + jitter);
      _controller.Move(_velocity * dt);
      return;
    }

    if (NetworkMobBrainLogic.TryGetHorizontalDirection(pos, _spawnPosition, out var dirR)) {
      transform.rotation = NetworkMobBrainLogic.RotationFacingHorizontal(dirR, transform.rotation);
      float speed = NetworkMobBrainLogic.SelectMobSpeed(_state, WalkSpeed, RunSpeed);
      Vector3 planarR = dirR * (speed * dt);
      _controller.Move(planarR + _velocity * dt);
    } else {
      _controller.Move(_velocity * dt);
    }
  }

  /// <summary>
  /// Prototype: scene scan for <see cref="Health"/>. Kept in one place for a future spatial/query replacement.
  /// </summary>
  static Health[] LoadHealthScanSnapshot() {
    return UnityEngine.Object.FindObjectsByType<Health>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
  }

  bool TryAcquireAggroTargetAuthority() {
    Vector3 pos = transform.position;
    float aggro = Mathf.Max(0f, AggroRadius);
    Health best = null;
    float bestSqr = float.MaxValue;

    Health[] candidates = LoadHealthScanSnapshot();
    for (var i = 0; i < candidates.Length; i++) {
      Health h = candidates[i];
      if (h == null || h == _health) {
        continue;
      }

      if (h.Object == null || !h.Object.IsValid) {
        continue;
      }

      if (h.IsDead) {
        continue;
      }

      Vector3 hp = h.transform.position;
      if (!NetworkMobBrainLogic.IsWithinHorizontalRange(pos, hp, aggro)) {
        continue;
      }

      float sqr = NetworkMobBrainLogic.HorizontalSqrDistance(pos, hp);
      if (sqr < bestSqr) {
        bestSqr = sqr;
        best = h;
      }
    }

    if (best == null) {
      return false;
    }

    _currentTargetId = best.Object.Id;
    return true;
  }

  bool TryResolveChaseTargetAuthority(out Health target) {
    target = null;
    if (!_currentTargetId.IsValid || Runner == null) {
      return false;
    }

    if (!Runner.TryFindObject(_currentTargetId, out NetworkObject obj) || obj == null) {
      return false;
    }

    if (!obj.TryGetComponent(out target) || target == null || target == _health) {
      return false;
    }

    if (target.Object == null || !target.Object.IsValid) {
      return false;
    }

    return true;
  }

  void TryMeleeAuthority() {
    if (!NetworkMobBrainLogic.CanAttackAtTick(Runner.Tick, _nextAttackTick)) {
      return;
    }

    Vector3 pos = transform.position;
    float attackR = Mathf.Max(0f, AttackRange);

    if (_state == NetworkMobBrainState.Chase) {
      if (!TryResolveChaseTargetAuthority(out Health chase) || chase.IsDead) {
        return;
      }

      Vector3 hp = chase.transform.position;
      if (NetworkMobBrainLogic.IsWithinHorizontalRange(pos, hp, attackR)) {
        chase.DealDamageRpc(AttackDamage);
        int cooldownTicks = NetworkMobBrainLogic.SecondsToTicks(AttackIntervalSeconds, Runner.TickRate);
        _nextAttackTick = Runner.Tick + cooldownTicks;
      }

      return;
    }

    float considerR = Mathf.Max(Mathf.Max(0f, AggroRadius), attackR);
    Health best = null;
    float bestSqr = float.MaxValue;
    Health[] candidates = LoadHealthScanSnapshot();
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

      Vector3 hpos = h.transform.position;
      if (!NetworkMobBrainLogic.IsWithinHorizontalRange(pos, hpos, considerR)) {
        continue;
      }

      if (!NetworkMobBrainLogic.IsWithinHorizontalRange(pos, hpos, attackR)) {
        continue;
      }

      float sqr = NetworkMobBrainLogic.HorizontalSqrDistance(pos, hpos);
      if (sqr < bestSqr) {
        bestSqr = sqr;
        best = h;
      }
    }

    if (best == null) {
      return;
    }

    best.DealDamageRpc(AttackDamage);
    int cd = NetworkMobBrainLogic.SecondsToTicks(AttackIntervalSeconds, Runner.TickRate);
    _nextAttackTick = Runner.Tick + cd;
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
