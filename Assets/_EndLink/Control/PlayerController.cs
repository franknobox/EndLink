using EndLink.Combat;
using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 玩家角色控制器。
    /// 只负责根据 PlayerInputReader 提供的输入驱动 CharacterController 移动和朝向，
    /// 不直接读取键盘、手柄或 InputAction。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour, IExternalDisplacementReceiver, ICombatKnockbackReceiver
    {
        private const float MoveInputDeadZoneSqr = 0.0001f;

        [Header("移动参数")]
        [Tooltip("普通移动速度，单位米/秒。")]
        [SerializeField, Min(0f)]
        private float moveSpeed = 5f;

        [Tooltip("按住冲刺键时的移动速度，单位米/秒。")]
        [SerializeField, Min(0f)]
        private float sprintSpeed = 7.5f;

        [SerializeField, Min(0.01f)]
        private float accelerationSmoothTime = 0.12f;

        [SerializeField, Min(0.01f)]
        private float decelerationSmoothTime = 0.08f;

        [Header("转向参数")]
        [SerializeField, Min(0f)]
        private float rotationSharpness = 14f;

        [Header("重力参数")]
        [SerializeField]
        private float gravity = -20f;

        [SerializeField]
        private float groundedStickForce = -2f;

        [Header("跳跃参数")]
        [Tooltip("单次跳跃的目标高度，单位米。第一版只做基础单段跳。")]
        [SerializeField, Min(0f)]
        private float jumpHeight = 1.6f;

        [Tooltip("两次跳跃之间的最短间隔，防止贴地瞬间重复触发。")]
        [SerializeField, Min(0f)]
        private float jumpCooldown = 0.1f;

        [Header("方向参考")]
        [Tooltip("移动方向参考。拖 Main Camera 后，WASD/左摇杆会按相机朝向转换为世界移动方向。")]
        [SerializeField]
        private Transform movementReference;

        private CharacterController _characterController;

        // 当前水平速度。只保存 XZ 平面的速度，Y 轴交给重力单独处理。
        private Vector3 _planarVelocity;

        // Mathf.SmoothDamp 需要 ref 形式的阻尼速度缓存，X/Z 分开保存。
        private float _velocityXSmoothRef;
        private float _velocityZSmoothRef;

        // CharacterController 不自带重力，需要手动累计垂直速度。
        private float _verticalVelocity;
        private bool _isSprinting;
        private float _nextJumpAllowedTime;

        /// <summary>当前帧玩家是否正在冲刺移动。</summary>
        public bool IsSprinting => _isSprinting;

        /// <summary>当前 CharacterController 是否认为玩家贴地。</summary>
        public bool IsGrounded => _characterController != null && _characterController.isGrounded;

        /// <summary>当前是否满足基础跳跃条件。</summary>
        public bool CanJump => isActiveAndEnabled
            && _characterController != null
            && _characterController.enabled
            && _characterController.isGrounded
            && jumpHeight > 0f
            && gravity < 0f
            && Time.time >= _nextJumpAllowedTime;

        /// <summary>玩家是否能被敌人的正常移动挤开。</summary>
        public bool CanReceiveExternalDisplacement => isActiveAndEnabled;

        /// <summary>
        /// 移动方向参考。
        /// 通常设置为 Main Camera 或 CameraTarget，用于把 WASD/左摇杆输入转换为相机相对移动。
        /// </summary>
        public Transform MovementReference
        {
            get => movementReference;
            set => movementReference = value;
        }

        /// <summary>
        /// 根据输入和参考旋转计算 XZ 平面的移动方向。
        /// 独立成静态方法，便于测试相机相对移动换算是否正确。
        /// </summary>
        public static Vector3 GetPlanarMoveDirectionForReference(Vector2 moveInput, Quaternion referenceRotation)
        {
            if (moveInput.sqrMagnitude <= MoveInputDeadZoneSqr)
            {
                return Vector3.zero;
            }

            Vector3 inputDirection = new Vector3(moveInput.x, 0f, moveInput.y);
            inputDirection = Vector3.ClampMagnitude(inputDirection, 1f);

            Vector3 referenceForward = Vector3.ProjectOnPlane(referenceRotation * Vector3.forward, Vector3.up);
            Vector3 referenceRight = Vector3.ProjectOnPlane(referenceRotation * Vector3.right, Vector3.up);

            if (referenceForward.sqrMagnitude <= MoveInputDeadZoneSqr || referenceRight.sqrMagnitude <= MoveInputDeadZoneSqr)
            {
                return inputDirection;
            }

            referenceForward.Normalize();
            referenceRight.Normalize();

            Vector3 moveDirection = referenceRight * inputDirection.x + referenceForward * inputDirection.z;
            return Vector3.ClampMagnitude(moveDirection, 1f);
        }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }

        /// <summary>
        /// 根据状态机传入的移动输入执行一帧移动。
        /// PlayerController 不再自行读取输入或判断当前是否允许移动，这些决策交给状态机。
        /// </summary>
        public void TickMovement(Vector2 moveInput, float deltaTime)
        {
            TickMovement(moveInput, false, deltaTime);
        }

        /// <summary>
        /// 根据状态机传入的移动输入和冲刺修饰执行一帧移动。
        /// 冲刺只是移动速度修饰，不单独改变玩家状态。
        /// </summary>
        public void TickMovement(Vector2 moveInput, bool sprintRequested, float deltaTime)
        {
            Vector3 desiredMoveDirection = GetDesiredMoveDirection(moveInput);
            _isSprinting = sprintRequested && desiredMoveDirection.sqrMagnitude > MoveInputDeadZoneSqr;

            float targetSpeed = _isSprinting ? sprintSpeed : moveSpeed;
            Vector3 targetPlanarVelocity = desiredMoveDirection * (targetSpeed * Mathf.Clamp01(moveInput.magnitude));

            SmoothPlanarVelocity(targetPlanarVelocity, deltaTime);
            UpdateVerticalVelocity(deltaTime);

            // CharacterController.Move 接收“本帧位移”，所以速度需要乘以 deltaTime。
            Vector3 motion = _planarVelocity;
            motion.y = _verticalVelocity;
            _characterController.Move(motion * deltaTime);

            RotateTowardsMoveDirection(desiredMoveDirection, deltaTime);
        }

        /// <summary>
        /// 执行一帧闪避位移。
        /// 闪避由状态机决定方向、速度和持续时间；控制器只负责用 CharacterController 移动并处理贴地重力。
        /// </summary>
        public void TickDodgeMovement(Vector3 dodgeDirection, float dodgeSpeed, float deltaTime, bool faceDodgeDirection = true)
        {
            dodgeDirection = Vector3.ProjectOnPlane(dodgeDirection, Vector3.up);

            if (dodgeDirection.sqrMagnitude <= MoveInputDeadZoneSqr || dodgeSpeed <= 0f)
            {
                TickMovement(Vector2.zero, false, deltaTime);
                return;
            }

            UpdateVerticalVelocity(deltaTime);

            Vector3 motion = dodgeDirection.normalized * dodgeSpeed;
            motion.y = _verticalVelocity;
            _characterController.Move(motion * deltaTime);

            if (faceDodgeDirection)
            {
                FaceDirection(dodgeDirection, false);
            }
        }

        /// <summary>
        /// 尝试执行一次基础单段跳。
        /// 状态机负责决定哪些状态能请求跳跃，控制器只负责写入垂直初速度。
        /// </summary>
        public bool TryJump()
        {
            if (!CanJump)
            {
                return false;
            }

            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _nextJumpAllowedTime = Time.time + jumpCooldown;
            return true;
        }

        /// <summary>
        /// 璁╃帺瀹舵湰鍦?Z 杞存鏂瑰悜闈㈠悜鎸囧畾涓栫晫鏂瑰悜銆?
        /// 鏀诲嚮銆佹妧鑳芥垨鍚庣画閿佸畾鍔ㄤ綔鍙互璋冪敤瀹冿紝璁╄鑹叉湞鍚戝拰鏀诲嚮鍔ㄧ敾姝ｉ潰淇濇寔涓€鑷淬€?
        /// </summary>
        public void FaceDirection(Vector3 worldDirection, bool instant)
        {
            Vector3 planarDirection = Vector3.ProjectOnPlane(worldDirection, Vector3.up);

            if (planarDirection.sqrMagnitude <= MoveInputDeadZoneSqr)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(planarDirection.normalized, Vector3.up);

            if (instant || rotationSharpness <= 0f)
            {
                transform.rotation = targetRotation;
                return;
            }

            float lerpFactor = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lerpFactor);
        }

        /// <summary>
        /// 接收敌人移动碰撞带来的外部位移。
        /// 这里只处理 XZ 平面，避免敌人水平移动把玩家顶上天；真正的击飞、击退之后应走战斗受击流程。
        /// </summary>
        public void AddExternalDisplacement(Vector3 displacement)
        {
            displacement.y = 0f;

            if (displacement.sqrMagnitude <= MoveInputDeadZoneSqr)
            {
                return;
            }

            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }

            if (_characterController != null && _characterController.enabled)
            {
                _characterController.Move(displacement);
                return;
            }

            transform.position += displacement;
        }

        /// <summary>
        /// 接收攻击命中的瞬时击退。
        /// 第一版复用 CharacterController 的水平外部位移入口，不处理击飞或持续受力。
        /// </summary>
        public void ApplyCombatKnockback(Vector3 displacement)
        {
            AddExternalDisplacement(displacement);
        }

        private void OnValidate()
        {
            // Inspector 中允许直接调参，这里保证重力与贴地力始终向下。
            gravity = -Mathf.Abs(gravity);
            groundedStickForce = -Mathf.Abs(groundedStickForce);
            jumpHeight = Mathf.Max(0f, jumpHeight);
            jumpCooldown = Mathf.Max(0f, jumpCooldown);
        }

        private void SmoothPlanarVelocity(Vector3 targetPlanarVelocity, float deltaTime)
        {
            // 加速和减速通常需要不同手感：起步略柔，松手更快收住。
            float smoothTime = targetPlanarVelocity.sqrMagnitude > _planarVelocity.sqrMagnitude
                ? accelerationSmoothTime
                : decelerationSmoothTime;

            _planarVelocity.x = Mathf.SmoothDamp(
                _planarVelocity.x,
                targetPlanarVelocity.x,
                ref _velocityXSmoothRef,
                smoothTime,
                Mathf.Infinity,
                deltaTime);

            _planarVelocity.z = Mathf.SmoothDamp(
                _planarVelocity.z,
                targetPlanarVelocity.z,
                ref _velocityZSmoothRef,
                smoothTime,
                Mathf.Infinity,
                deltaTime);
        }

        private void UpdateVerticalVelocity(float deltaTime)
        {
            if (_characterController.isGrounded && _verticalVelocity < 0f)
            {
                // 给一个轻微向下速度，让 CharacterController 稳定贴住地面。
                _verticalVelocity = groundedStickForce;
                return;
            }

            _verticalVelocity += gravity * deltaTime;
        }

        /// <summary>
        /// 把输入方向转换为世界 XZ 平面移动方向。
        /// 闪避、移动和后续其它行动状态可以共用这套相机相对方向换算。
        /// </summary>
        public Vector3 GetDesiredMoveDirection(Vector2 moveInput)
        {
            if (moveInput.sqrMagnitude <= MoveInputDeadZoneSqr)
            {
                return Vector3.zero;
            }

            Quaternion referenceRotation = movementReference != null ? movementReference.rotation : Quaternion.identity;
            return GetPlanarMoveDirectionForReference(moveInput, referenceRotation);
        }

        private void RotateTowardsMoveDirection(Vector3 desiredMoveDirection, float deltaTime)
        {
            if (desiredMoveDirection.sqrMagnitude <= MoveInputDeadZoneSqr || rotationSharpness <= 0f)
            {
                return;
            }

            // LookRotation 会让角色本地 Z 轴正方向（transform.forward）对准移动方向。
            Quaternion targetRotation = Quaternion.LookRotation(desiredMoveDirection, Vector3.up);

            // 指数插值比固定比例 Slerp 更稳定，不同帧率下的转向手感更接近。
            float lerpFactor = 1f - Mathf.Exp(-rotationSharpness * deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lerpFactor);
        }
    }
}
