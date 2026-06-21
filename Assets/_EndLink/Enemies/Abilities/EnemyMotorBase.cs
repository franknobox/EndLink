using EndLink.Core;
using EndLink.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人基础地面移动组件。
    /// 这里的 Base 表示“最基础的一种地面移动能力”，不是纯抽象基类。
    /// 第一版基于 CharacterController，负责追击、停止、平滑转向和简单重力。
    /// 后续特殊敌人如果需要不同移动方式，可以继承并覆盖 MoveTo / FaceTarget / Stop。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class EnemyMotorBase : MonoBehaviour, ICombatKnockbackReceiver
    {
        [Header("移动")]
        [Tooltip("敌人的基础移动速度。实际速度会乘以 Move Speed Multiplier。")]
        [SerializeField, Min(0f)]
        private float moveSpeed = 3.2f;

        [Tooltip("敌人接近目标时的水平速度平滑时间。数值越小响应越快。")]
        [SerializeField, Min(0.01f)]
        private float accelerationSmoothTime = 0.08f;

        [Header("转向")]
        [Tooltip("敌人面向移动方向或目标方向的旋转速度。")]
        [SerializeField, Min(0f)]
        private float rotationSpeed = 540f;

        [Header("重力")]
        [Tooltip("重力加速度。通常保持负数。")]
        [SerializeField]
        private float gravity = -20f;

        [Tooltip("贴地时保留的向下速度，防止 CharacterController 在坡面或台阶边缘轻微悬空。")]
        [SerializeField, Min(0f)]
        private float groundedStickForce = 2f;

        [Tooltip("防止水平追击时被玩家、队友或敌人胶囊碰撞体顶到空中。基础地面敌人建议开启。")]
        [SerializeField]
        private bool preventPlanarCollisionLift = true;

        [Tooltip("按“根物体在脚底”的白模约定自动校正 CharacterController Center Y，避免第一次 Move 时因为胶囊底部埋进地面而被弹起。")]
        [SerializeField]
        private bool autoAlignControllerToFeet = true;

        [Header("NavMesh")]
        [Tooltip("存在并启用 NavMeshAgent 时优先使用 NavMesh 路径移动；没有有效 NavMeshAgent 时自动回退为直线 CharacterController 移动。")]
        [SerializeField]
        private bool useNavMeshWhenAvailable = true;

        [Tooltip("把敌人当前位置或目标点吸附到最近 NavMesh 的最大搜索距离。距离过小会导致找不到路径，过大会产生明显瞬移。")]
        [SerializeField, Min(0.1f)]
        private float navMeshSampleDistance = 1.5f;

        [Header("碰撞推挤")]
        [Tooltip("敌人正常移动撞到可接收外部位移的角色时，是否把挡路角色沿敌人移动方向挤开。")]
        [SerializeField]
        private bool pushExternalDisplacementReceivers = true;

        [Tooltip("敌人本帧移动量转成推挤位移时的倍率。1 表示敌人走多少，挡路角色最多被挤开多少。")]
        [SerializeField, Min(0f)]
        private float collisionPushMultiplier = 1f;

        [Tooltip("单次碰撞最多传给玩家或队友的位移，避免一帧内被高速敌人推得过远。")]
        [SerializeField, Min(0f)]
        private float maxCollisionPushDistance = 0.35f;

        [Header("受击后退")]
        [Tooltip("一次战斗击退分摊到多少秒内完成。时间越短冲击越直接，越长后退越柔和。")]
        [SerializeField, Min(0.01f)]
        private float combatKnockbackDuration = CombatKnockback.DefaultMotionDuration;

        private CharacterController _characterController;
        private readonly CombatKnockbackMotion _combatKnockbackMotion = new();
        private Vector3 _horizontalVelocity;
        private Vector3 _horizontalVelocitySmoothRef;
        private float _verticalVelocity;
        private float _moveSpeedMultiplier = 1f;
        private bool _isMoving;
        private bool _isApplyingPlanarMove;
        private Vector3 _currentPlanarMoveDirection;
        private float _currentPlanarMoveDistance;
        private NavMeshAgent _navMeshAgent;
        private Vector3 _lastNavDestination;
        private bool _hasLastNavDestination;

        /// <summary>当前是否正在执行水平移动。</summary>
        public virtual bool IsMoving => _isMoving;

        /// <summary>当前水平速度，主要用于动画桥接和调试显示。</summary>
        public virtual float CurrentSpeed => _horizontalVelocity.magnitude;

        /// <summary>移动速度倍率，可被减速、加速、受击硬直等系统临时修改。</summary>
        protected float MoveSpeedMultiplier => _moveSpeedMultiplier;

        protected virtual void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _navMeshAgent = GetComponent<NavMeshAgent>();
            AlignControllerToFeetIfNeeded();
            ConfigureNavMeshAgent();
        }

        protected virtual void LateUpdate()
        {
            TickGravity(Time.deltaTime);
            TickCombatKnockback(Time.deltaTime);
        }

        protected virtual void OnDisable()
        {
            _combatKnockbackMotion.Clear();
        }

        protected virtual void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            accelerationSmoothTime = Mathf.Max(0.01f, accelerationSmoothTime);
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
            groundedStickForce = Mathf.Max(0f, groundedStickForce);
            navMeshSampleDistance = Mathf.Max(0.1f, navMeshSampleDistance);
            collisionPushMultiplier = Mathf.Max(0f, collisionPushMultiplier);
            maxCollisionPushDistance = Mathf.Max(0f, maxCollisionPushDistance);
            combatKnockbackDuration = Mathf.Max(0.01f, combatKnockbackDuration);
            AlignControllerToFeetIfNeeded();
            ConfigureNavMeshAgent();
        }

        /// <summary>
        /// 朝目标位置移动。
        /// stopDistance 表示距离目标多近时停止，目标点通常由状态机或行为层决定。
        /// </summary>
        public virtual void MoveTo(Vector3 destination, float stopDistance, float deltaTime)
        {
            EnsureCharacterController();

            if (_characterController == null || !_characterController.enabled)
            {
                return;
            }

            if (TryMoveToWithNavMesh(destination, stopDistance, deltaTime))
            {
                return;
            }

            Vector3 toDestination = GetPlanarDirection(transform.position, destination);
            float stopDistanceSafe = Mathf.Max(0f, stopDistance);

            if (toDestination.sqrMagnitude <= stopDistanceSafe * stopDistanceSafe)
            {
                Stop();
                return;
            }

            Vector3 moveDirection = toDestination.normalized;
            Vector3 targetVelocity = moveDirection * moveSpeed * MoveSpeedMultiplier;
            _horizontalVelocity = Vector3.SmoothDamp(
                _horizontalVelocity,
                targetVelocity,
                ref _horizontalVelocitySmoothRef,
                accelerationSmoothTime,
                Mathf.Infinity,
                deltaTime);

            _isMoving = _horizontalVelocity.sqrMagnitude > 0.0001f;
            MovePlanar(_horizontalVelocity * Mathf.Max(0f, deltaTime));
            FaceDirection(moveDirection, deltaTime);
        }

        /// <summary>
        /// 平滑面向目标。
        /// 只负责旋转，不负责移动。
        /// </summary>
        public virtual void FaceTarget(Transform target, float deltaTime)
        {
            if (target == null)
            {
                return;
            }

            if (CombatTargetUtility.TryResolve(target, out ICombatTarget combatTarget))
            {
                target = combatTarget.LockPoint;
            }

            Vector3 direction = GetPlanarDirection(transform.position, target.position);
            FaceDirection(direction, deltaTime);
        }

        /// <summary>
        /// 停止当前水平移动。
        /// 死亡、受击、待机或目标失效时由状态机调用。
        /// </summary>
        public virtual void Stop()
        {
            _horizontalVelocity = Vector3.zero;
            _horizontalVelocitySmoothRef = Vector3.zero;
            _isMoving = false;
            ClearNavMeshPath();
        }

        /// <summary>
        /// 设置移动速度倍率。
        /// 小于 0 的数值会被钳制为 0。
        /// </summary>
        public virtual void SetMoveSpeedMultiplier(float multiplier)
        {
            _moveSpeedMultiplier = Mathf.Max(0f, multiplier);
            ConfigureNavMeshAgent();
        }

        /// <summary>
        /// 计算忽略 Y 轴的方向向量。
        /// </summary>
        protected static Vector3 GetPlanarDirection(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            return direction;
        }

        protected virtual void FaceDirection(Vector3 direction, float deltaTime)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.0001f || rotationSpeed <= 0f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Mathf.Max(0f, deltaTime));
        }

        /// <summary>
        /// 接收攻击命中的总水平击退位移，并转换为短时衰减后退。
        /// 该入口与普通移动碰撞推挤分离，因此不会让玩家或队友通过接触反向顶动敌人。
        /// </summary>
        public virtual void ApplyCombatKnockback(Vector3 displacement)
        {
            displacement.y = 0f;
            if (displacement.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Stop();
            _combatKnockbackMotion.AddDisplacement(displacement, combatKnockbackDuration);
        }

        private void TickCombatKnockback(float deltaTime)
        {
            if (!_combatKnockbackMotion.IsActive)
            {
                return;
            }

            EnsureCharacterController();
            if (_characterController == null || !_characterController.enabled)
            {
                _combatKnockbackMotion.Clear();
                return;
            }

            Vector3 displacement = _combatKnockbackMotion.Tick(deltaTime);
            if (displacement.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            MovePlanar(displacement);
            SyncNavMeshAgentToTransform();
        }

        private void MovePlanar(Vector3 displacement)
        {
            float previousY = transform.position.y;

            BeginPlanarMove(displacement);
            try
            {
                _characterController.Move(displacement);
            }
            finally
            {
                EndPlanarMove();
            }

            if (!preventPlanarCollisionLift || transform.position.y <= previousY)
            {
                return;
            }

            Vector3 position = transform.position;
            position.y = previousY;
            transform.position = position;
        }

        private bool TryMoveToWithNavMesh(Vector3 destination, float stopDistance, float deltaTime)
        {
            if (!CanUseNavMesh())
            {
                return false;
            }

            if (!TryResolveNavMeshDestination(destination, out Vector3 navDestination))
            {
                Stop();
                return true;
            }

            float stopDistanceSafe = Mathf.Max(0f, stopDistance);
            _navMeshAgent.stoppingDistance = Mathf.Max(0.05f, stopDistanceSafe);
            if (!UpdateNavMeshDestination(navDestination))
            {
                Stop();
                return true;
            }

            if (_navMeshAgent.pathPending)
            {
                _isMoving = false;
                SyncNavMeshAgentToTransform();
                return true;
            }

            if (!_navMeshAgent.hasPath || _navMeshAgent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Stop();
                return true;
            }

            float remainingDistance = GetNavMeshRemainingDistance(navDestination);
            if (remainingDistance <= stopDistanceSafe || moveSpeed <= 0f)
            {
                Stop();
                return true;
            }

            Vector3 navMoveVector = GetNavMeshMoveVector(navDestination);
            if (navMoveVector.sqrMagnitude <= 0.0001f)
            {
                _isMoving = false;
                SyncNavMeshAgentToTransform();
                return true;
            }

            Vector3 planarDirection = GetPlanarDirection(Vector3.zero, navMoveVector);
            if (planarDirection.sqrMagnitude <= 0.0001f)
            {
                planarDirection = GetPlanarDirection(transform.position, navDestination);
            }

            if (planarDirection.sqrMagnitude <= 0.0001f)
            {
                _isMoving = false;
                SyncNavMeshAgentToTransform();
                return true;
            }

            Vector3 targetVelocity = planarDirection.normalized * moveSpeed * MoveSpeedMultiplier;
            _horizontalVelocity = Vector3.SmoothDamp(
                _horizontalVelocity,
                targetVelocity,
                ref _horizontalVelocitySmoothRef,
                accelerationSmoothTime,
                Mathf.Infinity,
                deltaTime);

            float stepDistance = _horizontalVelocity.magnitude * Mathf.Max(0f, deltaTime);
            if (stepDistance <= 0.0001f)
            {
                _isMoving = false;
                SyncNavMeshAgentToTransform();
                return true;
            }

            float maxStepDistance = Mathf.Max(0f, remainingDistance - stopDistanceSafe);
            if (maxStepDistance > 0f)
            {
                stepDistance = Mathf.Min(stepDistance, maxStepDistance);
            }

            Vector3 moveDelta = navMoveVector.normalized * stepDistance;
            _isMoving = moveDelta.sqrMagnitude > 0.0001f;
            MoveNavigation(moveDelta);
            SyncNavMeshAgentToTransform();
            FaceDirection(planarDirection, deltaTime);
            return true;
        }

        private bool CanUseNavMesh()
        {
            if (!useNavMeshWhenAvailable)
            {
                return false;
            }

            if (_navMeshAgent == null)
            {
                _navMeshAgent = GetComponent<NavMeshAgent>();
            }

            if (_navMeshAgent == null || !_navMeshAgent.enabled)
            {
                return false;
            }

            ConfigureNavMeshAgent();

            if (_navMeshAgent.isOnNavMesh)
            {
                return true;
            }

            return TrySnapNavMeshAgentToSurface();
        }

        private bool TrySnapNavMeshAgentToSurface()
        {
            if (_navMeshAgent == null)
            {
                return false;
            }

            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSampleDistance, _navMeshAgent.areaMask))
            {
                return false;
            }

            if (!_navMeshAgent.Warp(hit.position))
            {
                return false;
            }

            transform.position = hit.position;
            _hasLastNavDestination = false;
            return true;
        }

        private bool TryResolveNavMeshDestination(Vector3 destination, out Vector3 navDestination)
        {
            navDestination = destination;

            if (_navMeshAgent == null)
            {
                return false;
            }

            if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, navMeshSampleDistance, _navMeshAgent.areaMask))
            {
                return false;
            }

            navDestination = hit.position;
            return true;
        }

        private bool UpdateNavMeshDestination(Vector3 navDestination)
        {
            if (_navMeshAgent == null || !_navMeshAgent.isOnNavMesh)
            {
                return false;
            }

            if (_hasLastNavDestination && (navDestination - _lastNavDestination).sqrMagnitude <= 0.04f)
            {
                return true;
            }

            if (_navMeshAgent.SetDestination(navDestination))
            {
                _lastNavDestination = navDestination;
                _hasLastNavDestination = true;
                return true;
            }

            return false;
        }

        private float GetNavMeshRemainingDistance(Vector3 fallbackDestination)
        {
            if (_navMeshAgent == null || !_navMeshAgent.isOnNavMesh)
            {
                return Vector3.Distance(transform.position, fallbackDestination);
            }

            if (!_navMeshAgent.pathPending && _navMeshAgent.hasPath && !float.IsInfinity(_navMeshAgent.remainingDistance))
            {
                return _navMeshAgent.remainingDistance;
            }

            return Vector3.Distance(transform.position, fallbackDestination);
        }

        private Vector3 GetNavMeshMoveVector(Vector3 fallbackDestination)
        {
            if (_navMeshAgent == null || !_navMeshAgent.isOnNavMesh)
            {
                return fallbackDestination - transform.position;
            }

            Vector3 moveVector = _navMeshAgent.nextPosition - transform.position;
            if (moveVector.sqrMagnitude > 0.0001f)
            {
                return moveVector;
            }

            moveVector = _navMeshAgent.steeringTarget - transform.position;
            if (moveVector.sqrMagnitude > 0.0001f)
            {
                return moveVector;
            }

            return fallbackDestination - transform.position;
        }

        private void MoveNavigation(Vector3 displacement)
        {
            BeginPlanarMove(displacement);
            try
            {
                _characterController.Move(displacement);
            }
            finally
            {
                EndPlanarMove();
            }
        }

        private void SyncNavMeshAgentToTransform()
        {
            if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.isOnNavMesh)
            {
                _navMeshAgent.nextPosition = transform.position;
            }
        }

        private void ClearNavMeshPath()
        {
            _hasLastNavDestination = false;

            if (_navMeshAgent == null || !_navMeshAgent.enabled || !_navMeshAgent.isOnNavMesh)
            {
                return;
            }

            _navMeshAgent.ResetPath();
            _navMeshAgent.nextPosition = transform.position;
        }

        private void ConfigureNavMeshAgent()
        {
            if (_navMeshAgent == null)
            {
                _navMeshAgent = GetComponent<NavMeshAgent>();
            }

            if (_navMeshAgent == null)
            {
                return;
            }

            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }

            _navMeshAgent.updatePosition = false;
            _navMeshAgent.updateRotation = false;
            _navMeshAgent.autoBraking = false;
            _navMeshAgent.speed = moveSpeed * MoveSpeedMultiplier;
            _navMeshAgent.acceleration = accelerationSmoothTime > 0f
                ? Mathf.Max(moveSpeed / accelerationSmoothTime, moveSpeed)
                : moveSpeed;
            _navMeshAgent.angularSpeed = rotationSpeed;

            if (_characterController == null)
            {
                return;
            }

            _navMeshAgent.radius = _characterController.radius;
            _navMeshAgent.height = _characterController.height;
            _navMeshAgent.baseOffset = 0f;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!ShouldPushCollisionTarget(hit))
            {
                return;
            }

            IExternalDisplacementReceiver receiver = hit.collider.GetComponentInParent<IExternalDisplacementReceiver>();
            if (receiver == null || !receiver.CanReceiveExternalDisplacement)
            {
                return;
            }

            Vector3 pushDirection = ResolvePushDirection(hit);
            if (pushDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float pushDistance = _currentPlanarMoveDistance * collisionPushMultiplier;
            if (maxCollisionPushDistance > 0f)
            {
                pushDistance = Mathf.Min(pushDistance, maxCollisionPushDistance);
            }

            if (pushDistance <= 0f)
            {
                return;
            }

            receiver.AddExternalDisplacement(pushDirection.normalized * pushDistance);
        }

        private void BeginPlanarMove(Vector3 displacement)
        {
            displacement.y = 0f;
            _currentPlanarMoveDistance = displacement.magnitude;
            _currentPlanarMoveDirection = _currentPlanarMoveDistance > 0.0001f
                ? displacement / _currentPlanarMoveDistance
                : Vector3.zero;
            _isApplyingPlanarMove = _currentPlanarMoveDistance > 0.0001f;
        }

        private void EndPlanarMove()
        {
            _isApplyingPlanarMove = false;
            _currentPlanarMoveDirection = Vector3.zero;
            _currentPlanarMoveDistance = 0f;
        }

        private bool ShouldPushCollisionTarget(ControllerColliderHit hit)
        {
            if (!pushExternalDisplacementReceivers || !_isApplyingPlanarMove || _currentPlanarMoveDistance <= 0f)
            {
                return false;
            }

            if (hit == null || hit.collider == null)
            {
                return false;
            }

            Transform hitTransform = hit.collider.transform;
            return hitTransform != transform && !hitTransform.IsChildOf(transform);
        }

        private Vector3 ResolvePushDirection(ControllerColliderHit hit)
        {
            Vector3 pushDirection = _currentPlanarMoveDirection;

            if (pushDirection.sqrMagnitude <= 0.0001f && hit.transform != null)
            {
                pushDirection = hit.transform.position - transform.position;
                pushDirection.y = 0f;
            }

            pushDirection.y = 0f;
            return pushDirection;
        }

        private void TickGravity(float deltaTime)
        {
            EnsureCharacterController();

            if (_characterController == null || !_characterController.enabled)
            {
                return;
            }

            if (_characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -groundedStickForce;
            }

            _verticalVelocity += gravity * Mathf.Max(0f, deltaTime);
            _characterController.Move(Vector3.up * _verticalVelocity * Mathf.Max(0f, deltaTime));
        }

        private void EnsureCharacterController()
        {
            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
                AlignControllerToFeetIfNeeded();
            }
        }

        private void AlignControllerToFeetIfNeeded()
        {
            if (!autoAlignControllerToFeet)
            {
                return;
            }

            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }

            if (_characterController == null)
            {
                return;
            }

            float height = Mathf.Max(_characterController.height, _characterController.radius * 2f);
            Vector3 center = _characterController.center;
            center.y = height * 0.5f;

            _characterController.height = height;
            _characterController.center = center;
        }
    }
}
