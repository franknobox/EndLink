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
    public sealed class PlayerController : MonoBehaviour
    {
        private const float MoveInputDeadZoneSqr = 0.0001f;

        [Header("移动参数")]
        [SerializeField, Min(0f)]
        private float moveSpeed = 5f;

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
            Vector3 desiredMoveDirection = GetDesiredMoveDirection(moveInput);
            Vector3 targetPlanarVelocity = desiredMoveDirection * (moveSpeed * Mathf.Clamp01(moveInput.magnitude));

            SmoothPlanarVelocity(targetPlanarVelocity, deltaTime);
            UpdateVerticalVelocity(deltaTime);

            // CharacterController.Move 接收“本帧位移”，所以速度需要乘以 deltaTime。
            Vector3 motion = _planarVelocity;
            motion.y = _verticalVelocity;
            _characterController.Move(motion * deltaTime);

            RotateTowardsMoveDirection(desiredMoveDirection, deltaTime);
        }

        private void OnValidate()
        {
            // Inspector 中允许直接调参，这里保证重力与贴地力始终向下。
            gravity = -Mathf.Abs(gravity);
            groundedStickForce = -Mathf.Abs(groundedStickForce);
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

        private Vector3 GetDesiredMoveDirection(Vector2 moveInput)
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
