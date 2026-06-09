using EndLink.Party;
using EndLink.Core;
using EndLink.Combat;
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
    public sealed class AllyFollowMotor : MonoBehaviour, IExternalDisplacementReceiver, ICombatKnockbackReceiver
    {
        [SerializeField, HideInInspector]
        private Transform followTarget;

        [SerializeField, HideInInspector]
        private float stopDistance = 0.15f;

        [SerializeField, HideInInspector]
        private float followSlotSoftness = 0.75f;

        [SerializeField, HideInInspector]
        private float followDeadZoneRadius = 5f;

        [SerializeField, HideInInspector]
        private float moveSpeed = 4f;

        [SerializeField, HideInInspector]
        private float sprintSyncSpeedMultiplier = 1.5f;

        [SerializeField, HideInInspector]
        private float arrivalSmoothTime = 0.12f;

        [SerializeField, HideInInspector]
        private float catchUpDistance = 5f;

        [SerializeField, HideInInspector]
        private float catchUpSpeedMultiplier = 1.75f;

        [SerializeField, HideInInspector]
        private float teleportDistance = 15f;

        [SerializeField, HideInInspector]
        private float rotationSpeed = 540f;

        [SerializeField, HideInInspector]
        private AllyIdleFacingMode idleFacingMode = AllyIdleFacingMode.FaceFollowTargetForward;

        [SerializeField, HideInInspector]
        private Vector3 formationOffset = new Vector3(2f, 0f, -2.5f);

        [SerializeField, HideInInspector]
        private bool avoidanceEnabled = true;

        [SerializeField, HideInInspector]
        private float followTargetAvoidRadius = 1.15f;

        [SerializeField, HideInInspector]
        private float allyAvoidRadius = 1f;

        [SerializeField, HideInInspector]
        private float avoidanceStrength = 1.25f;

        [SerializeField, HideInInspector]
        private LayerMask avoidanceLayerMask;

        private const int AvoidanceOverlapCapacity = 8;
        private const int DeadZoneGizmoSegments = 64;
        private CharacterController _characterController;
        private PlayerController _followTargetPlayerController;
        private readonly Collider[] _avoidanceOverlaps = new Collider[AvoidanceOverlapCapacity];
        private Vector3 _desiredWorldPosition;
        private Vector3 _followDeadZoneAnchorPosition;
        private float _currentSpeed;
        private float _speedVelocity;
        private bool _isRepositioning;

        /// <summary>当前跟随目标。</summary>
        public Transform FollowTarget => followTarget;

        /// <summary>停止移动距离。</summary>
        public float StopDistance => stopDistance;

        /// <summary>队形点软半径。</summary>
        public float FollowSlotSoftness => followSlotSoftness;

        /// <summary>跟随死区半径。</summary>
        public float FollowDeadZoneRadius => followDeadZoneRadius;

        /// <summary>基础移动速度。</summary>
        public float MoveSpeed => moveSpeed;

        /// <summary>主控冲刺时的跟随速度倍率。</summary>
        public float SprintSyncSpeedMultiplier => sprintSyncSpeedMultiplier;

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

        /// <summary>队友是否能被敌人的正常移动挤开。</summary>
        public bool CanReceiveExternalDisplacement => isActiveAndEnabled;

        /// <summary>最近一次计算得到的世界队形点，方便调试和后续可视化。</summary>
        public Vector3 DesiredWorldPosition => _desiredWorldPosition;

        /// <summary>
        /// 当前是否正在因为跟随死区而保持原地。
        /// PartyManager 的动态站位会读取它，避免在死区内重写队形 offset 导致队友被强制拉动。
        /// </summary>
        public bool IsHoldingFollowDeadZone => ShouldHoldFollowDeadZone();

        /// <summary>当前是否正在向新的跟随队形点归位。</summary>
        public bool IsRepositioning => _isRepositioning;

        /// <summary>
        /// 跟随目标当前是否处在这个队友的死区圆内。
        /// 这个判断不关心队友是否正在归位，给 PartyManager 判断“玩家是否仍在死区内，是否不该换位”使用。
        /// </summary>
        public bool IsFollowTargetInsideDeadZone => IsFollowTargetInsideCurrentDeadZone();

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _desiredWorldPosition = transform.position;
            _followDeadZoneAnchorPosition = transform.position;
        }

        private void Reset()
        {
            _characterController = GetComponent<CharacterController>();
            formationOffset = new Vector3(2f, 0f, -2.5f);
        }

        private void OnValidate()
        {
            stopDistance = Mathf.Max(0f, stopDistance);
            followSlotSoftness = Mathf.Max(0f, followSlotSoftness);
            followDeadZoneRadius = Mathf.Max(0f, followDeadZoneRadius);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            sprintSyncSpeedMultiplier = Mathf.Max(1f, sprintSyncSpeedMultiplier);
            arrivalSmoothTime = Mathf.Max(0.001f, arrivalSmoothTime);
            catchUpDistance = Mathf.Max(0f, catchUpDistance);
            catchUpSpeedMultiplier = Mathf.Max(1f, catchUpSpeedMultiplier);
            teleportDistance = Mathf.Max(0f, teleportDistance);
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
            followTargetAvoidRadius = Mathf.Max(0f, followTargetAvoidRadius);
            allyAvoidRadius = Mathf.Max(0f, allyAvoidRadius);
            avoidanceStrength = Mathf.Max(0f, avoidanceStrength);
        }

        private void OnDrawGizmos()
        {
            DrawDeadZoneGizmo();
        }

        private void OnDrawGizmosSelected()
        {
        }

        /// <summary>
        /// 设置跟随目标。
        /// 由队伍管理器或 AllyStateMachine 调用，组件本身不关心目标来自哪里。
        /// </summary>
        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
            _followTargetPlayerController = target != null ? target.GetComponent<PlayerController>() : null;
            ResetFollowDeadZoneAnchor();
            RequestReposition();
            ResetSpeed();
        }

        /// <summary>
        /// 设置队友相对跟随目标的本地队形偏移。
        /// 主要由 PartyManager 在初始化固定小队槽位时调用。
        /// </summary>
        public void SetFormationOffset(Vector3 offset)
        {
            if ((formationOffset - offset).sqrMagnitude <= 0.0001f)
            {
                return;
            }

            formationOffset = offset;
            RequestReposition();
        }

        /// <summary>
        /// 从助战、动作、受击等外部移动状态回到 Follow 时调用。
        /// 这些状态可能已经改变了队友位置，必须丢弃旧死区圆心并重新走一次归位。
        /// </summary>
        public void ResumeFollowFromCurrentPosition()
        {
            ResetFollowDeadZoneAnchor();
            RequestReposition();
            ResetSpeed();
        }

        /// <summary>
        /// 应用小队统一跟随参数。
        /// PartyManager 调用它完成统一调参；本组件仍只负责使用这些参数执行实际移动。
        /// </summary>
        public void ApplySettings(PartyFollowSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            stopDistance = Mathf.Max(0f, settings.StopDistance);
            followSlotSoftness = Mathf.Max(0f, settings.FollowSlotSoftness);
            followDeadZoneRadius = Mathf.Max(0f, settings.FollowDeadZoneRadius);
            moveSpeed = Mathf.Max(0f, settings.MoveSpeed);
            sprintSyncSpeedMultiplier = Mathf.Max(1f, settings.SprintSyncSpeedMultiplier);
            arrivalSmoothTime = Mathf.Max(0.001f, settings.ArrivalSmoothTime);
            catchUpDistance = Mathf.Max(0f, settings.CatchUpDistance);
            catchUpSpeedMultiplier = Mathf.Max(1f, settings.CatchUpSpeedMultiplier);
            teleportDistance = Mathf.Max(0f, settings.TeleportDistance);
            rotationSpeed = Mathf.Max(0f, settings.RotationSpeed);
            idleFacingMode = settings.IdleFacingMode;
            avoidanceEnabled = settings.AvoidanceEnabled;
            followTargetAvoidRadius = Mathf.Max(0f, settings.FollowTargetAvoidRadius);
            allyAvoidRadius = Mathf.Max(0f, settings.AllyAvoidRadius);
            avoidanceStrength = Mathf.Max(0f, settings.AvoidanceStrength);
            avoidanceLayerMask = settings.AvoidanceLayerMask;
        }

        /// <summary>
        /// 执行一帧跟随移动。
        /// 只在 AllyFollowState 中调用，避免 Idle、Assist、Action、Hit、LinkDown 状态继续抢移动控制权。
        /// </summary>
        public void TickFollow(float deltaTime)
        {
            if (deltaTime <= 0f || followTarget == null)
            {
                ResetSpeed();
                return;
            }

            if (ShouldHoldFollowDeadZone())
            {
                _desiredWorldPosition = _followDeadZoneAnchorPosition;
                SmoothSpeedTo(0f, deltaTime);
                ApplyAvoidanceOnly(CalculateAvoidanceVector(), deltaTime, false);
                _followDeadZoneAnchorPosition = transform.position;
                return;
            }

            _isRepositioning = true;
            SyncFollowDeadZoneAnchorToCurrentPosition();
            _desiredWorldPosition = CalculateDesiredWorldPosition();

            Vector3 toDesired = _desiredWorldPosition - transform.position;
            toDesired.y = 0f;

            float distance = toDesired.magnitude;

            if (ShouldTeleport(distance))
            {
                TeleportToDesiredPosition();
                ResetFollowDeadZoneAnchor();
                RotateAfterArrive(deltaTime);
                return;
            }

            Vector3 avoidanceVector = CalculateAvoidanceVector();
            float arriveRadius = Mathf.Max(stopDistance, 0.05f);
            if (distance <= arriveRadius || moveSpeed <= 0f)
            {
                SmoothSpeedTo(0f, deltaTime);
                ApplyAvoidanceOnly(avoidanceVector, deltaTime);
                ResetFollowDeadZoneAnchor();
                RotateAfterArrive(deltaTime);
                return;
            }

            Vector3 moveDirection = toDesired / distance;
            Vector3 finalMoveDirection = BlendAvoidance(moveDirection, avoidanceVector);
            float targetSpeed = CalculateTargetSpeed(distance, arriveRadius, true);
            float currentSpeed = SmoothSpeedTo(targetSpeed, deltaTime);
            float step = Mathf.Min(currentSpeed * deltaTime, distance - arriveRadius);

            if (step > 0f)
            {
                Move(finalMoveDirection * step);
                SyncFollowDeadZoneAnchorToCurrentPosition();
                RotateTowards(finalMoveDirection, deltaTime);
            }
        }

        /// <summary>
        /// 移动到指定世界坐标附近。
        /// 用于助战接近、后续重新站位或行为树 Action，不修改跟随目标和队形偏移。
        /// </summary>
        public void TickMoveToPosition(Vector3 worldPosition, float arriveDistance, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                ResetSpeed();
                return;
            }

            _desiredWorldPosition = worldPosition;
            _desiredWorldPosition.y = transform.position.y;

            Vector3 toDesired = _desiredWorldPosition - transform.position;
            toDesired.y = 0f;

            float distance = toDesired.magnitude;
            Vector3 avoidanceVector = CalculateAvoidanceVector();
            float arriveRadius = Mathf.Max(0f, arriveDistance);

            if (distance <= arriveRadius || moveSpeed <= 0f)
            {
                SmoothSpeedTo(0f, deltaTime);
                ApplyAvoidanceOnly(avoidanceVector, deltaTime);
                RotateTowardsPosition(worldPosition, deltaTime);
                return;
            }

            Vector3 moveDirection = toDesired / distance;
            Vector3 finalMoveDirection = BlendAvoidance(moveDirection, avoidanceVector);
            float targetSpeed = CalculateTargetSpeed(distance, arriveRadius, false);
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

            // 如果没有配置队形偏移，则使用默认右后方站位，避免零向量导致队友贴到主控身上。
            if (offset.sqrMagnitude <= 0.0001f)
            {
                offset = new Vector3(2f, 0f, -2.5f);
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

        private bool ShouldHoldFollowDeadZone()
        {
            if (_isRepositioning || followDeadZoneRadius <= 0f || followTarget == null)
            {
                return false;
            }

            return IsFollowTargetInsideCurrentDeadZone();
        }

        private bool IsFollowTargetInsideCurrentDeadZone()
        {
            if (followDeadZoneRadius <= 0f || followTarget == null)
            {
                return false;
            }

            // 死区判断以队友自己的当前站位为中心；主控仍在该半径内时，这个队友保持原地。
            Vector3 toTarget = followTarget.position - _followDeadZoneAnchorPosition;
            toTarget.y = 0f;
            return toTarget.sqrMagnitude <= followDeadZoneRadius * followDeadZoneRadius;
        }

        private void ResetFollowDeadZoneAnchor()
        {
            SyncFollowDeadZoneAnchorToCurrentPosition();
            _isRepositioning = false;
        }

        private void SyncFollowDeadZoneAnchorToCurrentPosition()
        {
            _followDeadZoneAnchorPosition = transform.position;
            _followDeadZoneAnchorPosition.y = transform.position.y;
        }

        private void RequestReposition()
        {
            _isRepositioning = true;
        }

        private void TeleportToDesiredPosition()
        {
            transform.position = _desiredWorldPosition;
            ResetSpeed();
        }

        private float CalculateTargetSpeed(float distance, float arriveRadius, bool allowSprintSync)
        {
            float speed = moveSpeed;

            if (allowSprintSync && IsFollowTargetSprinting())
            {
                speed *= sprintSyncSpeedMultiplier;
            }

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

        private bool IsFollowTargetSprinting()
        {
            return _followTargetPlayerController != null && _followTargetPlayerController.IsSprinting;
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

        private void ApplyAvoidanceOnly(Vector3 avoidanceVector, float deltaTime, bool rotateTowardsAvoidance = true)
        {
            if (avoidanceVector.sqrMagnitude <= 0.0001f || moveSpeed <= 0f)
            {
                return;
            }

            Vector3 direction = avoidanceVector.normalized;
            float step = moveSpeed * avoidanceStrength * deltaTime;
            Move(direction * step);

            if (rotateTowardsAvoidance)
            {
                RotateTowards(direction, deltaTime);
            }
        }

        /// <summary>
        /// 接收敌人正常移动时传来的外部位移。
        /// 被挤开后同步死区圆心，避免队友被推走后仍然使用旧站位作为死区中心。
        /// </summary>
        public void AddExternalDisplacement(Vector3 displacement)
        {
            displacement.y = 0f;

            if (displacement.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Move(displacement);
            SyncFollowDeadZoneAnchorToCurrentPosition();
        }

        /// <summary>
        /// 接收攻击命中的瞬时击退。
        /// 被击退后同步跟随死区圆心，避免跟随系统立刻把队友拉回击退前的位置。
        /// </summary>
        public void ApplyCombatKnockback(Vector3 displacement)
        {
            AddExternalDisplacement(displacement);
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

        private void RotateTowardsPosition(Vector3 worldPosition, float deltaTime)
        {
            Vector3 toPosition = worldPosition - transform.position;
            toPosition.y = 0f;

            if (toPosition.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            RotateTowards(toPosition.normalized, deltaTime);
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

        private Vector3 ResolveDeadZoneGizmoCenter()
        {
            Vector3 center;

            if (Application.isPlaying)
            {
                center = _followDeadZoneAnchorPosition;
            }
            else
            {
                center = transform.position;
            }

            center.y = transform.position.y;
            return center;
        }

        private void DrawDeadZoneGizmo()
        {
            if (followDeadZoneRadius <= 0f)
            {
                return;
            }

            Color previousColor = Gizmos.color;
            Gizmos.color = new Color(0.02f, 0.18f, 0.85f, 0.85f);

            Vector3 center = ResolveDeadZoneGizmoCenter();
            DrawHorizontalCircle(center, followDeadZoneRadius, DeadZoneGizmoSegments);
            Gizmos.DrawLine(center, center + Vector3.forward * followDeadZoneRadius);

            Gizmos.color = previousColor;
        }

        private static void DrawHorizontalCircle(Vector3 center, float radius, int segmentCount)
        {
            float angleStep = Mathf.PI * 2f / segmentCount;
            Vector3 previousPoint = center + new Vector3(radius, 0f, 0f);

            for (int i = 1; i <= segmentCount; i++)
            {
                float angle = angleStep * i;
                Vector3 nextPoint = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius);

                Gizmos.DrawLine(previousPoint, nextPoint);
                previousPoint = nextPoint;
            }
        }
    }
}
