using UnityEngine;

namespace EndLink.Ally
{
    /// <summary>
    /// 队友跟随移动组件。
    /// 第一版不依赖 NavMesh，只负责把队友平滑移动到主控附近的队形位置。
    /// 状态机只在 Follow 状态中调用 TickFollow，不在这里判断队友当前是否允许跟随。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AllyFollowMotor : MonoBehaviour
    {
        [Header("跟随目标")]
        [Tooltip("队友要跟随的目标，通常是固定主控角色的根物体。也可以由 AllyStateMachine.SetFollowTarget 在运行时写入。")]
        [SerializeField]
        private Transform followTarget;

        [Header("距离")]
        [Tooltip("当 formationOffset 为零时使用的默认后方跟随距离。formationOffset 非零时，队友会优先移动到队形偏移点。")]
        [SerializeField, Min(0.01f)]
        private float followDistance = 2.5f;

        [Tooltip("距离目标队形点小于该值时停止移动，用于避免到点后微小抖动。")]
        [SerializeField, Min(0f)]
        private float stopDistance = 0.15f;

        [Header("移动")]
        [Tooltip("队友朝队形点移动的最大速度，单位是米/秒。")]
        [SerializeField, Min(0f)]
        private float moveSpeed = 4f;

        [Tooltip("队友转向速度，单位是角度/秒。移动时优先面向移动方向，停下时面向跟随目标。")]
        [SerializeField, Min(0f)]
        private float rotationSpeed = 540f;

        [Tooltip("队友相对跟随目标的本地队形偏移。X 为左右，Z 为前后，常用值例如 (1.5, 0, -2.5)。")]
        [SerializeField]
        private Vector3 formationOffset = new Vector3(1.5f, 0f, -2.5f);

        private CharacterController _characterController;
        private Vector3 _desiredWorldPosition;

        /// <summary>当前跟随目标。</summary>
        public Transform FollowTarget => followTarget;

        /// <summary>默认跟随距离。</summary>
        public float FollowDistance => followDistance;

        /// <summary>停止移动距离。</summary>
        public float StopDistance => stopDistance;

        /// <summary>移动速度。</summary>
        public float MoveSpeed => moveSpeed;

        /// <summary>转向速度。</summary>
        public float RotationSpeed => rotationSpeed;

        /// <summary>本地队形偏移。</summary>
        public Vector3 FormationOffset => formationOffset;

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
            moveSpeed = Mathf.Max(0f, moveSpeed);
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
        }

        /// <summary>
        /// 设置跟随目标。
        /// 由队伍管理器或 AllyStateMachine 调用，组件本身不关心目标来自哪里。
        /// </summary>
        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
        }

        /// <summary>
        /// 执行一帧跟随移动。
        /// 只在 AllyFollowState 中调用，避免 Idle、Assist、Hit、Dead 状态继续抢移动控制权。
        /// </summary>
        public void TickFollow(float deltaTime)
        {
            if (deltaTime <= 0f || followTarget == null)
            {
                return;
            }

            _desiredWorldPosition = CalculateDesiredWorldPosition();

            Vector3 toDesired = _desiredWorldPosition - transform.position;
            toDesired.y = 0f;

            float distance = toDesired.magnitude;
            if (distance > stopDistance && moveSpeed > 0f)
            {
                Vector3 moveDirection = toDesired / distance;
                float step = Mathf.Min(moveSpeed * deltaTime, distance);

                Move(moveDirection * step);
                RotateTowards(moveDirection, deltaTime);
                return;
            }

            RotateTowardsFollowTarget(deltaTime);
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

        private void Move(Vector3 displacement)
        {
            if (_characterController != null && _characterController.enabled)
            {
                _characterController.Move(displacement);
                return;
            }

            transform.position += displacement;
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
