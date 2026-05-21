using UnityEngine;

namespace EndLink.Ally
{
    /// <summary>
    /// 队友停止跟随后如何处理朝向。
    /// </summary>
    public enum AllyIdleFacingMode
    {
        /// <summary>停下后继续面向跟随目标。</summary>
        FaceFollowTarget = 0,

        /// <summary>停下后保持当前朝向，只在移动时转向。</summary>
        KeepCurrentRotation = 1,

        /// <summary>停下后面向跟随目标的正前方，适合队伍一起看向行进方向。</summary>
        FaceFollowTargetForward = 2
    }

    /// <summary>
    /// 队友跟随移动组件。
    /// 第一版不依赖 NavMesh，只负责把队友平滑移动到主控附近的队形位置。
    /// 状态机只在 Follow 状态中调用 TickFollow，不在这里判断队友当前是否允许跟随。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AllyFollowMotor : MonoBehaviour
    {
        [SerializeField, HideInInspector]
        private Transform followTarget;

        [Header("距离")]
        [Tooltip("没有队伍槽位偏移时使用的默认后方跟随距离。正常情况下由 PartyManager 写入槽位偏移。")]
        [SerializeField, Min(0.01f)]
        private float followDistance = 2.5f;

        [Tooltip("距离目标队形点小于该值时停止移动，用于避免到点后微小抖动。")]
        [SerializeField, Min(0f)]
        private float stopDistance = 0.15f;

        [Tooltip("队形点软半径。进入这个范围就算到位，不会强制踩死一个精确点。")]
        [SerializeField, Min(0f)]
        private float followSlotSoftness = 0.75f;

        [Header("移动")]
        [Tooltip("队友朝队形点移动的基础最大速度，单位是米/秒。")]
        [SerializeField, Min(0f)]
        private float moveSpeed = 4f;

        [Tooltip("接近目标点时的速度阻尼时间。值越小越跟手，值越大越柔和。")]
        [SerializeField, Min(0.001f)]
        private float arrivalSmoothTime = 0.12f;

        [Tooltip("距离队形点超过该值时进入追赶模式。设置为 0 表示不启用追赶加速。")]
        [SerializeField, Min(0f)]
        private float catchUpDistance = 5f;

        [Tooltip("追赶模式下的速度倍率。只有距离超过 catchUpDistance 时生效。")]
        [SerializeField, Min(1f)]
        private float catchUpSpeedMultiplier = 1.75f;

        [Tooltip("距离队形点超过该值时直接瞬移归位。设置为 0 表示不启用瞬移归位。")]
        [SerializeField, Min(0f)]
        private float teleportDistance = 12f;

        [Header("转向")]
        [Tooltip("队友转向速度，单位是角度/秒。移动时优先面向移动方向。")]
        [SerializeField, Min(0f)]
        private float rotationSpeed = 540f;

        [Tooltip("停止跟随后如何处理朝向。Keep Current Rotation 可以避免队友站定后一直盯着主控。")]
        [SerializeField]
        private AllyIdleFacingMode idleFacingMode = AllyIdleFacingMode.FaceFollowTargetForward;

        [SerializeField, HideInInspector]
        private Vector3 formationOffset = new Vector3(1.5f, 0f, -2.5f);

        [Header("简易避让")]
        [Tooltip("是否启用第一版角色间简易避让。只做局部排斥，不做 NavMesh 寻路。")]
        [SerializeField]
        private bool avoidanceEnabled = true;

        [Tooltip("队友离跟随目标小于该半径时，会被轻微推离主控。")]
        [SerializeField, Min(0f)]
        private float followTargetAvoidRadius = 1.15f;

        [Tooltip("队友离其他队友小于该半径时，会被轻微推开。需要配置 avoidanceLayerMask 才能检测到其他队友。")]
        [SerializeField, Min(0f)]
        private float allyAvoidRadius = 1f;

        [Tooltip("避让修正强度。值越大，队友越倾向于绕开主控和其他队友。")]
        [SerializeField, Min(0f)]
        private float avoidanceStrength = 1.25f;

        [Tooltip("参与队友间避让检测的 Layer。建议给队友角色设置单独 Layer 后在这里勾选。主控避让不依赖该 Layer。")]
        [SerializeField]
        private LayerMask avoidanceLayerMask;

        private const int AvoidanceOverlapCapacity = 8;
        private CharacterController _characterController;
        private readonly Collider[] _avoidanceOverlaps = new Collider[AvoidanceOverlapCapacity];
        private Vector3 _desiredWorldPosition;
        private float _currentSpeed;
        private float _speedVelocity;

        /// <summary>当前跟随目标。</summary>
        public Transform FollowTarget => followTarget;

        /// <summary>默认跟随距离。</summary>
        public float FollowDistance => followDistance;

        /// <summary>停止移动距离。</summary>
        public float StopDistance => stopDistance;

        /// <summary>队形点软半径。</summary>
        public float FollowSlotSoftness => followSlotSoftness;

        /// <summary>基础移动速度。</summary>
        public float MoveSpeed => moveSpeed;

        /// <summary>接近目标点时的速度阻尼时间。</summary>
        public float ArrivalSmoothTime => arrivalSmoothTime;

        /// <summary>进入追赶模式的距离。</summary>
        public float CatchUpDistance => catchUpDistance;

        /// <summary>追赶模式速度倍率。</summary>
        public float CatchUpSpeedMultiplier => catchUpSpeedMultiplier;

        /// <summary>瞬移归位距离。</summary>
        public float TeleportDistance => teleportDistance;

        /// <summary>转向速度。</summary>
        public float RotationSpeed => rotationSpeed;

        /// <summary>停止跟随后朝向模式。</summary>
        public AllyIdleFacingMode IdleFacingMode => idleFacingMode;

        /// <summary>当前运行时使用的本地队形偏移，通常由 PartyManager 写入。</summary>
        public Vector3 FormationOffset => formationOffset;

        /// <summary>是否启用简易避让。</summary>
        public bool AvoidanceEnabled => avoidanceEnabled;

        /// <summary>跟随目标避让半径。</summary>
        public float FollowTargetAvoidRadius => followTargetAvoidRadius;

        /// <summary>队友间避让半径。</summary>
        public float AllyAvoidRadius => allyAvoidRadius;

        /// <summary>避让修正强度。</summary>
        public float AvoidanceStrength => avoidanceStrength;

        /// <summary>队友间避让检测 Layer。</summary>
        public LayerMask AvoidanceLayerMask => avoidanceLayerMask;

        /// <summary>最近一次计算得到的世界队形点，方便调试和后续可视化。</summary>
        public Vector3 DesiredWorldPosition => _desiredWorldPosition;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _desiredWorldPosition = transform.position;
        }

        private void Reset()
        {
            _characterController = GetComponent<CharacterController>();
            formationOffset = new Vector3(1.5f, 0f, -followDistance);
        }

        private void OnValidate()
        {
            followDistance = Mathf.Max(0.01f, followDistance);
            stopDistance = Mathf.Max(0f, stopDistance);
            followSlotSoftness = Mathf.Max(0f, followSlotSoftness);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            arrivalSmoothTime = Mathf.Max(0.001f, arrivalSmoothTime);
            catchUpDistance = Mathf.Max(0f, catchUpDistance);
            catchUpSpeedMultiplier = Mathf.Max(1f, catchUpSpeedMultiplier);
            teleportDistance = Mathf.Max(0f, teleportDistance);
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
            followTargetAvoidRadius = Mathf.Max(0f, followTargetAvoidRadius);
            allyAvoidRadius = Mathf.Max(0f, allyAvoidRadius);
            avoidanceStrength = Mathf.Max(0f, avoidanceStrength);
        }

        /// <summary>
        /// 设置跟随目标。
        /// 由队伍管理器或 AllyStateMachine 调用，组件本身不关心目标来自哪里。
        /// </summary>
        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
            ResetSpeed();
        }

        /// <summary>
        /// 设置队友相对跟随目标的本地队形偏移。
        /// 主要由 PartyManager 在初始化固定小队槽位时调用。
        /// </summary>
        public void SetFormationOffset(Vector3 offset)
        {
            formationOffset = offset;
        }

        /// <summary>
        /// 执行一帧跟随移动。
        /// 只在 AllyFollowState 中调用，避免 Idle、Assist、Hit、Dead 状态继续抢移动控制权。
        /// </summary>
        public void TickFollow(float deltaTime)
        {
            if (deltaTime <= 0f || followTarget == null)
            {
                ResetSpeed();
                return;
            }

            _desiredWorldPosition = CalculateDesiredWorldPosition();

            Vector3 toDesired = _desiredWorldPosition - transform.position;
            toDesired.y = 0f;

            float distance = toDesired.magnitude;

            if (ShouldTeleport(distance))
            {
                TeleportToDesiredPosition();
                RotateAfterArrive(deltaTime);
                return;
            }

            Vector3 avoidanceVector = CalculateAvoidanceVector();
            float arriveRadius = Mathf.Max(stopDistance, followSlotSoftness);
            if (distance <= arriveRadius || moveSpeed <= 0f)
            {
                SmoothSpeedTo(0f, deltaTime);
                ApplyAvoidanceOnly(avoidanceVector, deltaTime);
                RotateAfterArrive(deltaTime);
                return;
            }

            Vector3 moveDirection = toDesired / distance;
            Vector3 finalMoveDirection = BlendAvoidance(moveDirection, avoidanceVector);
            float targetSpeed = CalculateTargetSpeed(distance, arriveRadius);
            float currentSpeed = SmoothSpeedTo(targetSpeed, deltaTime);
            float step = Mathf.Min(currentSpeed * deltaTime, distance - arriveRadius);

            if (step > 0f)
            {
                Move(finalMoveDirection * step);
                RotateTowards(finalMoveDirection, deltaTime);
            }
        }

        private Vector3 CalculateDesiredWorldPosition()
        {
            Vector3 offset = formationOffset;

            // 如果没有配置队形偏移，则默认站到主控后方 followDistance 的位置。
            if (offset.sqrMagnitude <= 0.0001f)
            {
                offset = Vector3.back * followDistance;
            }

            Vector3 worldOffset = followTarget.TransformDirection(offset);
            worldOffset.y = 0f;

            Vector3 desiredPosition = followTarget.position + worldOffset;
            desiredPosition.y = transform.position.y;
            return desiredPosition;
        }

        private bool ShouldTeleport(float distance)
        {
            return teleportDistance > 0f && distance >= teleportDistance;
        }

        private void TeleportToDesiredPosition()
        {
            transform.position = _desiredWorldPosition;
            ResetSpeed();
        }

        private float CalculateTargetSpeed(float distance, float arriveRadius)
        {
            float speed = moveSpeed;

            if (catchUpDistance > 0f && distance >= catchUpDistance)
            {
                speed *= catchUpSpeedMultiplier;
            }

            float slowDownDistance = Mathf.Max(0.5f, followSlotSoftness);
            float speedFactor = Mathf.InverseLerp(arriveRadius, arriveRadius + slowDownDistance, distance);
            return speed * speedFactor;
        }

        private float SmoothSpeedTo(float targetSpeed, float deltaTime)
        {
            _currentSpeed = Mathf.SmoothDamp(
                _currentSpeed,
                targetSpeed,
                ref _speedVelocity,
                arrivalSmoothTime,
                Mathf.Infinity,
                deltaTime);

            if (targetSpeed <= 0f && _currentSpeed < 0.01f)
            {
                ResetSpeed();
            }

            return _currentSpeed;
        }

        private void ResetSpeed()
        {
            _currentSpeed = 0f;
            _speedVelocity = 0f;
        }

        private Vector3 CalculateAvoidanceVector()
        {
            if (!avoidanceEnabled || avoidanceStrength <= 0f)
            {
                return Vector3.zero;
            }

            Vector3 avoidance = Vector3.zero;
            avoidance += CalculateFollowTargetAvoidance();
            avoidance += CalculateAllyAvoidance();

            return Vector3.ClampMagnitude(avoidance, 1f);
        }

        private Vector3 CalculateFollowTargetAvoidance()
        {
            if (followTarget == null || followTargetAvoidRadius <= 0f)
            {
                return Vector3.zero;
            }

            return CalculateRepulsionFromPosition(followTarget.position, followTargetAvoidRadius);
        }

        private Vector3 CalculateAllyAvoidance()
        {
            if (allyAvoidRadius <= 0f || avoidanceLayerMask.value == 0)
            {
                return Vector3.zero;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                allyAvoidRadius,
                _avoidanceOverlaps,
                avoidanceLayerMask,
                QueryTriggerInteraction.Ignore);

            Vector3 avoidance = Vector3.zero;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _avoidanceOverlaps[i];
                if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                Vector3 closestPoint = hit.ClosestPoint(transform.position);
                avoidance += CalculateRepulsionFromPosition(closestPoint, allyAvoidRadius);
            }

            return avoidance;
        }

        private Vector3 CalculateRepulsionFromPosition(Vector3 sourcePosition, float radius)
        {
            Vector3 away = transform.position - sourcePosition;
            away.y = 0f;

            float distance = away.magnitude;
            if (distance >= radius)
            {
                return Vector3.zero;
            }

            if (distance <= 0.0001f)
            {
                away = -transform.forward;
                distance = 0f;
            }

            float weight = 1f - Mathf.Clamp01(distance / radius);
            return away.normalized * weight;
        }

        private Vector3 BlendAvoidance(Vector3 moveDirection, Vector3 avoidanceVector)
        {
            if (avoidanceVector.sqrMagnitude <= 0.0001f)
            {
                return moveDirection;
            }

            Vector3 blended = moveDirection + avoidanceVector * avoidanceStrength;
            blended.y = 0f;

            return blended.sqrMagnitude > 0.0001f ? blended.normalized : moveDirection;
        }

        private void ApplyAvoidanceOnly(Vector3 avoidanceVector, float deltaTime)
        {
            if (avoidanceVector.sqrMagnitude <= 0.0001f || moveSpeed <= 0f)
            {
                return;
            }

            Vector3 direction = avoidanceVector.normalized;
            float step = moveSpeed * avoidanceStrength * deltaTime;
            Move(direction * step);
            RotateTowards(direction, deltaTime);
        }

        private void Move(Vector3 displacement)
        {
            if (_characterController != null && _characterController.enabled)
            {
                _characterController.Move(displacement);
                return;
            }

            transform.position += displacement;
        }

        private void RotateAfterArrive(float deltaTime)
        {
            switch (idleFacingMode)
            {
                case AllyIdleFacingMode.KeepCurrentRotation:
                    return;
                case AllyIdleFacingMode.FaceFollowTargetForward:
                    RotateTowardsFollowTargetForward(deltaTime);
                    return;
                default:
                    RotateTowardsFollowTarget(deltaTime);
                    return;
            }
        }

        private void RotateTowardsFollowTarget(float deltaTime)
        {
            Vector3 toTarget = followTarget.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            RotateTowards(toTarget.normalized, deltaTime);
        }

        private void RotateTowardsFollowTargetForward(float deltaTime)
        {
            Vector3 forward = followTarget.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            RotateTowards(forward.normalized, deltaTime);
        }

        private void RotateTowards(Vector3 direction, float deltaTime)
        {
            if (rotationSpeed <= 0f || direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * deltaTime);
        }
    }
}
