using UnityEngine;

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
    public class EnemyMotorBase : MonoBehaviour
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

        private CharacterController _characterController;
        private Vector3 _horizontalVelocity;
        private Vector3 _horizontalVelocitySmoothRef;
        private float _verticalVelocity;
        private float _moveSpeedMultiplier = 1f;
        private bool _isMoving;

        /// <summary>当前是否正在执行水平移动。</summary>
        public virtual bool IsMoving => _isMoving;

        /// <summary>当前水平速度，主要用于动画桥接和调试显示。</summary>
        public virtual float CurrentSpeed => _horizontalVelocity.magnitude;

        /// <summary>移动速度倍率，可被减速、加速、受击硬直等系统临时修改。</summary>
        protected float MoveSpeedMultiplier => _moveSpeedMultiplier;

        protected virtual void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }

        protected virtual void Update()
        {
            TickGravity(Time.deltaTime);
        }

        protected virtual void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            accelerationSmoothTime = Mathf.Max(0.01f, accelerationSmoothTime);
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
            groundedStickForce = Mathf.Max(0f, groundedStickForce);
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
            _characterController.Move(_horizontalVelocity * Mathf.Max(0f, deltaTime));
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
        }

        /// <summary>
        /// 设置移动速度倍率。
        /// 小于 0 的数值会被钳制为 0。
        /// </summary>
        public virtual void SetMoveSpeedMultiplier(float multiplier)
        {
            _moveSpeedMultiplier = Mathf.Max(0f, multiplier);
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
            }
        }
    }
}
