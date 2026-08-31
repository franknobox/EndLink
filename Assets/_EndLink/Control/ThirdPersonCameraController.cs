using EndLink.Party;
using Unity.Cinemachine;
using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 第三人称相机当前采用的自动距离模式。
    /// Idle 用于脱战待机近景，Active 用于移动和战斗远景。
    /// </summary>
    public enum CameraDistanceMode
    {
        Idle,
        Active
    }

    /// <summary>
    /// 第三人称相机控制器。
    /// 负责把玩家相机输入转换为 CameraTarget 的旋转，并根据玩家状态自动调整跟随距离。
    /// 实际跟随、缓动、越肩构图仍交给 Cinemachine 处理。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCameraInputReader))]
    [RequireComponent(typeof(CinemachineCamera))]
    [RequireComponent(typeof(CinemachineThirdPersonFollow))]
    public sealed class ThirdPersonCameraController : MonoBehaviour
    {
        /// <summary>持续处于脱战 Idle 多久后，镜头才开始拉近。</summary>
        public const float IdleDistanceDelay = 1f;

        /// <summary>持续处于移动或战斗多久后，镜头才开始拉远。</summary>
        public const float ActiveDistanceDelay = 0.5f;

        [Header("目标")]
        [Tooltip("玩家根节点。CameraTarget 会跟随这个对象的位置，但不会直接旋转玩家本体。")]
        [SerializeField]
        private Transform followTarget;

        [Tooltip("相机旋转中心。建议放在玩家子物体 CameraTarget 上，高度在胸口到头部之间。")]
        [SerializeField]
        private Transform cameraTarget;

        [Tooltip("主控玩家状态机。为空时会尝试从 Follow Target 自动获取。")]
        [SerializeField]
        private PlayerStateMachine playerStateMachine;

        [Tooltip("小队战斗上下文。处于战斗时，即使玩家当前 Idle，镜头也会保持远距离。")]
        [SerializeField]
        private PartyCombatContext partyCombatContext;

        [Tooltip("CameraTarget 相对玩家根节点的世界空间偏移。Y 越大，视角中心越高。")]
        [SerializeField, HideInInspector]
        private Vector3 targetWorldOffset = new Vector3(0f, 1.65f, 0f);

        [Tooltip("手柄右摇杆水平旋转速度，单位为度/秒。数值越大，左右转视角越快。")]
        [SerializeField, HideInInspector, Min(0f)]
        private float gamepadYawSpeed = 190f;

        [Tooltip("手柄右摇杆垂直旋转速度，单位为度/秒。数值越大，上下抬压镜头越快。")]
        [SerializeField, HideInInspector, Min(0f)]
        private float gamepadPitchSpeed = 120f;

        [Tooltip("鼠标水平灵敏度。数值越大，鼠标左右移动时视角转得越快。")]
        [SerializeField, HideInInspector, Min(0f)]
        private float mouseYawSensitivity = 0.12f;

        [Tooltip("鼠标垂直灵敏度。数值越大，鼠标上下移动时视角抬压越快。")]
        [SerializeField, HideInInspector, Min(0f)]
        private float mousePitchSensitivity = 0.1f;

        [Tooltip("相机最低俯仰角。数值越小，越允许镜头向下看或压低到角色身后。")]
        [SerializeField, HideInInspector]
        private float minPitch = -30f;

        [Tooltip("相机最高俯仰角。数值越大，越允许镜头抬高形成更俯视的战场视角。")]
        [SerializeField, HideInInspector]
        private float maxPitch = 55f;

        [Header("输入选项")]
        [Tooltip("是否反转垂直视角输入。开启后，向上推摇杆/移动鼠标会压低或抬高的方向相反。")]
        [SerializeField]
        private bool invertY;

        [Tooltip("脱战且保持 Idle 后的近景距离。数值越小，静止观察时镜头越贴近角色。")]
        [SerializeField, HideInInspector, Min(0.01f)]
        private float idleDistance = 5.5f;

        [Tooltip("移动、攻击或处于战斗上下文时的远景距离。数值越大，战斗视野越开阔。")]
        [SerializeField, HideInInspector, Min(0.01f)]
        private float activeDistance = 7f;

        [Tooltip("镜头从远景缓慢拉近到静止距离所用的平滑时间。")]
        [SerializeField, HideInInspector, Min(0.01f)]
        private float idleDistanceSmoothTime = 1.2f;

        [Tooltip("镜头从近景较快拉远到移动/战斗距离所用的平滑时间。")]
        [SerializeField, HideInInspector, Min(0.01f)]
        private float activeDistanceSmoothTime = 0.25f;

        [Tooltip("越肩支点偏移。X 影响左右越肩偏移，Y 影响镜头支点高度，Z 通常保持 0。")]
        [SerializeField, HideInInspector]
        private Vector3 shoulderOffset = new Vector3(0.8f, 1.45f, 0f);

        [Tooltip("肩部到相机手臂的垂直长度。数值越大，镜头越偏高，战场可见范围通常更开阔。")]
        [SerializeField, HideInInspector]
        private float verticalArmLength = 0f;

        [Tooltip("相机靠哪一侧肩膀。0 为左肩，1 为右肩，0.5 为居中。")]
        [SerializeField, HideInInspector, Range(0f, 1f)]
        private float cameraSide = 1f;

        [Tooltip("Cinemachine 跟随阻尼。X/Y/Z 分别影响本地轴向跟随滞后，数值越大越柔但越拖。")]
        [SerializeField, HideInInspector]
        private Vector3 damping = new Vector3(0.22f, 0.28f, 0.24f);

        [Tooltip("相机垂直视场角。数值越大画面越广、透视感越强；数值越小画面越窄、目标更近。")]
        [SerializeField, HideInInspector, Range(1f, 179f)]
        private float fieldOfView = 52f;

        [Tooltip("启用该相机时是否锁定并隐藏鼠标。第三人称自由视角通常开启，调试 UI 时可关闭。")]
        [SerializeField]
        private bool lockCursorOnEnable = true;

        private PlayerCameraInputReader _inputReader;
        private CinemachineCamera _cinemachineCamera;
        private CinemachineThirdPersonFollow _thirdPersonFollow;

        private float _yaw;
        private float _pitch;
        private float _currentDistance;
        private float _distanceSmoothVelocity;
        private float _distanceRequestDuration;
        private bool _lastActiveDistanceRequested = true;
        private CameraDistanceMode _distanceMode = CameraDistanceMode.Active;
        private bool _createdRuntimeCameraTarget;
        private Transform _lockTarget;
        private float _lockRotationSmoothTime = 0.12f;
        private float _lockYawVelocity;
        private float _lockPitchVelocity;
        private bool _hasTemporaryViewOverride;
        private bool _returningFromTemporaryView;
        private float _temporaryDistance;
        private float _temporaryFieldOfView;
        private Vector3 _temporaryShoulderOffset;
        private float _temporaryEnterSmoothTime = 0.12f;
        private float _temporaryExitSmoothTime = 0.22f;
        private float _currentFieldOfView;
        private float _fieldOfViewSmoothVelocity;
        private Vector3 _currentShoulderOffset;
        private Vector3 _shoulderOffsetSmoothVelocity;
        private Vector3 _additiveTargetWorldOffset;
        private Vector3 _additiveShoulderOffset;

        /// <summary>当前相机跟随的玩家根节点，供视角模式协调器解析玩家组件。</summary>
        public Transform FollowTarget => followTarget;

        /// <summary>Applies a runtime composition offset without changing the saved view preset.</summary>
        public void SetAdditiveViewOffset(Vector3 targetOffset, Vector3 shoulderOffsetDelta)
        {
            _additiveTargetWorldOffset = targetOffset;
            _additiveShoulderOffset = shoulderOffsetDelta;
        }

        /// <summary>Clears runtime composition offsets such as locomotion bob.</summary>
        public void ClearAdditiveViewOffset()
        {
            _additiveTargetWorldOffset = Vector3.zero;
            _additiveShoulderOffset = Vector3.zero;
        }

        /// <summary>读取当前自由相机参数，用于保存高速模式基准。</summary>
        public PlayerViewSettings CaptureViewSettings()
        {
            return new PlayerViewSettings(
                targetWorldOffset,
                gamepadYawSpeed,
                gamepadPitchSpeed,
                mouseYawSensitivity,
                mousePitchSensitivity,
                minPitch,
                maxPitch,
                idleDistance,
                activeDistance,
                idleDistanceSmoothTime,
                activeDistanceSmoothTime,
                shoulderOffset,
                verticalArmLength,
                cameraSide,
                damping,
                fieldOfView);
        }

        /// <summary>
        /// 应用一组视角参数。snapDistance 只在初始化时使用，运行时切换默认保留平滑距离过渡。
        /// </summary>
        public void ApplyViewSettings(PlayerViewSettings settings, bool snapDistance)
        {
            settings = settings.Sanitized();
            targetWorldOffset = settings.TargetWorldOffset;
            gamepadYawSpeed = settings.GamepadYawSpeed;
            gamepadPitchSpeed = settings.GamepadPitchSpeed;
            mouseYawSensitivity = settings.MouseYawSensitivity;
            mousePitchSensitivity = settings.MousePitchSensitivity;
            minPitch = settings.MinPitch;
            maxPitch = settings.MaxPitch;
            idleDistance = settings.IdleDistance;
            activeDistance = settings.ActiveDistance;
            idleDistanceSmoothTime = settings.IdleDistanceSmoothTime;
            activeDistanceSmoothTime = settings.ActiveDistanceSmoothTime;
            shoulderOffset = settings.ShoulderOffset;
            verticalArmLength = settings.VerticalArmLength;
            cameraSide = settings.CameraSide;
            damping = settings.Damping;
            fieldOfView = settings.FieldOfView;

            if (snapDistance)
            {
                _currentDistance = _distanceMode == CameraDistanceMode.Active
                    ? activeDistance
                    : idleDistance;
                _distanceSmoothVelocity = 0f;
                _currentFieldOfView = fieldOfView;
                _fieldOfViewSmoothVelocity = 0f;
                _currentShoulderOffset = shoulderOffset;
                _shoulderOffsetSmoothVelocity = Vector3.zero;
            }

            if (_cinemachineCamera != null && _thirdPersonFollow != null)
            {
                ApplyCinemachineSettings();
            }
        }

        /// <summary>
        /// 启用临时镜头构图。射击瞄准等短时模式使用该入口，不会覆盖当前视角模式保存的基础参数。
        /// </summary>
        public void SetTemporaryViewOverride(
            float distance,
            float overrideFieldOfView,
            Vector3 overrideShoulderOffset,
            float enterSmoothTime,
            float exitSmoothTime)
        {
            _temporaryDistance = Mathf.Max(0.01f, distance);
            _temporaryFieldOfView = Mathf.Clamp(overrideFieldOfView, 1f, 179f);
            _temporaryShoulderOffset = overrideShoulderOffset;
            _temporaryEnterSmoothTime = Mathf.Max(0.01f, enterSmoothTime);
            _temporaryExitSmoothTime = Mathf.Max(0.01f, exitSmoothTime);
            _hasTemporaryViewOverride = true;
            _returningFromTemporaryView = false;
            ResetViewOverrideVelocities();
        }

        /// <summary>解除临时镜头构图，并平滑恢复当前视角模式与自动距离。</summary>
        public void ClearTemporaryViewOverride()
        {
            if (!_hasTemporaryViewOverride)
            {
                return;
            }

            _hasTemporaryViewOverride = false;
            _returningFromTemporaryView = true;
            ResetViewOverrideVelocities();
        }

        /// <summary>让自由相机平滑朝向指定硬锁点。</summary>
        public void SetLockTarget(Transform target, float smoothTime)
        {
            if (target == null)
            {
                ClearLockTarget();
                return;
            }

            if (_lockTarget != target)
            {
                _lockYawVelocity = 0f;
                _lockPitchVelocity = 0f;
            }

            _lockTarget = target;
            _lockRotationSmoothTime = Mathf.Max(0.01f, smoothTime);
        }

        /// <summary>解除相机硬锁朝向，保留当前角度继续自由旋转。</summary>
        public void ClearLockTarget()
        {
            _lockTarget = null;
            _lockYawVelocity = 0f;
            _lockPitchVelocity = 0f;
        }

        /// <summary>
        /// 将 pitch 限制在合法范围内。独立成静态方法，方便测试和复用。
        /// </summary>
        public static float ClampPitch(float pitch, float minPitch, float maxPitch)
        {
            if (minPitch > maxPitch)
            {
                (minPitch, maxPitch) = (maxPitch, minPitch);
            }

            return Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        /// <summary>
        /// 根据玩家状态判断是否应请求移动/战斗远景。
        /// 当前只有真正脱战待机的 Idle 使用近景；未初始化和终止状态保持远景，避免镜头意外贴近。
        /// </summary>
        public static bool IsActiveCameraState(PlayerStateId stateId)
        {
            return stateId != PlayerStateId.Idle;
        }

        /// <summary>
        /// 根据持续请求时间决定是否完成近景/远景模式切换。
        /// 延迟只负责防止短暂停顿或点按移动造成镜头反复伸缩。
        /// </summary>
        public static CameraDistanceMode ResolveDistanceMode(
            CameraDistanceMode currentMode,
            bool activeRequested,
            float requestDuration)
        {
            if (activeRequested)
            {
                return currentMode == CameraDistanceMode.Idle && requestDuration >= ActiveDistanceDelay
                    ? CameraDistanceMode.Active
                    : currentMode;
            }

            return currentMode == CameraDistanceMode.Active && requestDuration >= IdleDistanceDelay
                ? CameraDistanceMode.Idle
                : currentMode;
        }

        private void Awake()
        {
            _inputReader = GetComponent<PlayerCameraInputReader>();
            _cinemachineCamera = GetComponent<CinemachineCamera>();
            _thirdPersonFollow = GetComponent<CinemachineThirdPersonFollow>();

            if (followTarget == null && cameraTarget != null)
            {
                followTarget = cameraTarget.parent != null ? cameraTarget.parent : cameraTarget.root;
            }

            if (cameraTarget == null)
            {
                Transform baseTarget = followTarget != null ? followTarget : _cinemachineCamera.Follow;

                if (baseTarget != null)
                {
                    followTarget ??= baseTarget;
                    cameraTarget = CreateRuntimeCameraTarget(baseTarget);
                }
                else
                {
                    Debug.LogError("ThirdPersonCameraController 需要指定 followTarget 或 cameraTarget。", this);
                    enabled = false;
                    return;
                }
            }

            if (playerStateMachine == null && followTarget != null)
            {
                playerStateMachine = followTarget.GetComponentInParent<PlayerStateMachine>();
            }

            Vector3 eulerAngles = cameraTarget != null ? cameraTarget.rotation.eulerAngles : transform.rotation.eulerAngles;
            _yaw = eulerAngles.y;
            _pitch = NormalizePitch(eulerAngles.x);

            // 开场使用远景。只有确认玩家持续脱战待机后，镜头才会自然拉近。
            _distanceMode = CameraDistanceMode.Active;
            _lastActiveDistanceRequested = true;
            _distanceRequestDuration = 0f;
            _currentDistance = activeDistance;
            _currentFieldOfView = fieldOfView;
            _currentShoulderOffset = shoulderOffset;

            ApplyCinemachineSettings();
        }

        private void OnDestroy()
        {
            if (!_createdRuntimeCameraTarget || cameraTarget == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(cameraTarget.gameObject);
            }
            else
            {
                DestroyImmediate(cameraTarget.gameObject);
            }
        }

        private void OnEnable()
        {
            if (!lockCursorOnEnable)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            ClearAdditiveViewOffset();

            if (!lockCursorOnEnable)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void LateUpdate()
        {
            if (cameraTarget == null)
            {
                return;
            }

            UpdateCameraTargetPosition();
            UpdateRotation(Time.deltaTime);
            UpdateAutomaticDistance(Time.deltaTime);
            ApplyCinemachineSettings();
        }

        private void OnValidate()
        {
            idleDistance = Mathf.Max(0.01f, idleDistance);
            activeDistance = Mathf.Max(idleDistance, activeDistance);
            idleDistanceSmoothTime = Mathf.Max(0.01f, idleDistanceSmoothTime);
            activeDistanceSmoothTime = Mathf.Max(0.01f, activeDistanceSmoothTime);

            if (minPitch > maxPitch)
            {
                (minPitch, maxPitch) = (maxPitch, minPitch);
            }

            damping.x = Mathf.Max(0f, damping.x);
            damping.y = Mathf.Max(0f, damping.y);
            damping.z = Mathf.Max(0f, damping.z);
        }

        private void UpdateCameraTargetPosition()
        {
            if (followTarget == null)
            {
                return;
            }

            // CameraTarget 跟随角色根节点位置，并抬高到胸口/头部之间。
            // 相机旋转只作用在 CameraTarget 上，不直接旋转玩家本体。
            cameraTarget.position = followTarget.position + targetWorldOffset + _additiveTargetWorldOffset;
        }

        private void UpdateRotation(float deltaTime)
        {
            if (_lockTarget != null)
            {
                UpdateLockRotation(deltaTime);
                return;
            }

            Vector2 lookInput = _inputReader.LookInput;

            if (_inputReader.IsPointerLookInput)
            {
                _yaw += lookInput.x * mouseYawSensitivity;
                _pitch += GetPitchInput(lookInput.y) * mousePitchSensitivity;
            }
            else
            {
                _yaw += lookInput.x * gamepadYawSpeed * deltaTime;
                _pitch += GetPitchInput(lookInput.y) * gamepadPitchSpeed * deltaTime;
            }

            _pitch = ClampPitch(_pitch, minPitch, maxPitch);
            cameraTarget.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void UpdateLockRotation(float deltaTime)
        {
            Vector3 toTarget = _lockTarget.position - cameraTarget.position;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            Vector3 desiredEuler = desiredRotation.eulerAngles;
            float desiredYaw = desiredEuler.y;
            float desiredPitch = ClampPitch(NormalizePitch(desiredEuler.x), minPitch, maxPitch);

            _yaw = Mathf.SmoothDampAngle(
                _yaw,
                desiredYaw,
                ref _lockYawVelocity,
                _lockRotationSmoothTime,
                Mathf.Infinity,
                deltaTime);
            _pitch = Mathf.SmoothDampAngle(
                _pitch,
                desiredPitch,
                ref _lockPitchVelocity,
                _lockRotationSmoothTime,
                Mathf.Infinity,
                deltaTime);

            cameraTarget.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void UpdateAutomaticDistance(float deltaTime)
        {
            bool activeRequested = IsActiveDistanceRequested();

            if (activeRequested != _lastActiveDistanceRequested)
            {
                _lastActiveDistanceRequested = activeRequested;
                _distanceRequestDuration = 0f;
            }
            else
            {
                _distanceRequestDuration += deltaTime;
            }

            _distanceMode = ResolveDistanceMode(
                _distanceMode,
                activeRequested,
                _distanceRequestDuration);

            float baseTargetDistance = _distanceMode == CameraDistanceMode.Active
                ? activeDistance
                : idleDistance;
            float baseSmoothTime = _distanceMode == CameraDistanceMode.Active
                ? activeDistanceSmoothTime
                : idleDistanceSmoothTime;
            float targetDistance = _hasTemporaryViewOverride
                ? _temporaryDistance
                : baseTargetDistance;
            float smoothTime = _hasTemporaryViewOverride
                ? _temporaryEnterSmoothTime
                : _returningFromTemporaryView
                    ? _temporaryExitSmoothTime
                    : baseSmoothTime;

            _currentDistance = Mathf.SmoothDamp(
                _currentDistance,
                targetDistance,
                ref _distanceSmoothVelocity,
                smoothTime,
                Mathf.Infinity,
                deltaTime);

            float targetFieldOfView = _hasTemporaryViewOverride
                ? _temporaryFieldOfView
                : fieldOfView;
            Vector3 targetShoulderOffset = _hasTemporaryViewOverride
                ? _temporaryShoulderOffset
                : shoulderOffset;
            float compositionSmoothTime = _hasTemporaryViewOverride
                ? _temporaryEnterSmoothTime
                : _returningFromTemporaryView
                    ? _temporaryExitSmoothTime
                    : 0f;

            if (compositionSmoothTime <= 0f)
            {
                _currentFieldOfView = targetFieldOfView;
                _currentShoulderOffset = targetShoulderOffset;
            }
            else
            {
                _currentFieldOfView = Mathf.SmoothDamp(
                    _currentFieldOfView,
                    targetFieldOfView,
                    ref _fieldOfViewSmoothVelocity,
                    compositionSmoothTime,
                    Mathf.Infinity,
                    deltaTime);
                _currentShoulderOffset = Vector3.SmoothDamp(
                    _currentShoulderOffset,
                    targetShoulderOffset,
                    ref _shoulderOffsetSmoothVelocity,
                    compositionSmoothTime,
                    Mathf.Infinity,
                    deltaTime);
            }

            if (_returningFromTemporaryView
                && Mathf.Abs(_currentDistance - baseTargetDistance) <= 0.01f
                && Mathf.Abs(_currentFieldOfView - fieldOfView) <= 0.05f
                && (_currentShoulderOffset - shoulderOffset).sqrMagnitude <= 0.0001f)
            {
                _returningFromTemporaryView = false;
            }
        }

        private bool IsActiveDistanceRequested()
        {
            if (partyCombatContext != null && partyCombatContext.IsInCombat)
            {
                return true;
            }

            // 缺少状态机引用时保守地维持远景，不让配置缺失造成镜头突然贴近。
            return playerStateMachine == null
                || IsActiveCameraState(playerStateMachine.CurrentStateId);
        }

        private void ApplyCinemachineSettings()
        {
            if (cameraTarget != null)
            {
                _cinemachineCamera.Target.TrackingTarget = cameraTarget;
                _cinemachineCamera.Target.CustomLookAtTarget = false;
            }

            _thirdPersonFollow.ShoulderOffset = _currentShoulderOffset + _additiveShoulderOffset;
            _thirdPersonFollow.VerticalArmLength = verticalArmLength;
            _thirdPersonFollow.CameraSide = cameraSide;
            _thirdPersonFollow.Damping = damping;
            _thirdPersonFollow.CameraDistance = _currentDistance;

            _cinemachineCamera.Lens.FieldOfView = _currentFieldOfView;
        }

        private void ResetViewOverrideVelocities()
        {
            _distanceSmoothVelocity = 0f;
            _fieldOfViewSmoothVelocity = 0f;
            _shoulderOffsetSmoothVelocity = Vector3.zero;
        }

        private Transform CreateRuntimeCameraTarget(Transform baseTarget)
        {
            GameObject targetObject = new GameObject("CameraTarget_Runtime");
            Transform targetTransform = targetObject.transform;

            targetTransform.SetPositionAndRotation(baseTarget.position + targetWorldOffset, baseTarget.rotation);
            targetTransform.SetParent(baseTarget, true);

            _createdRuntimeCameraTarget = true;
            return targetTransform;
        }

        private float GetPitchInput(float rawPitchInput)
        {
            return invertY ? rawPitchInput : -rawPitchInput;
        }

        private static float NormalizePitch(float eulerX)
        {
            return eulerX > 180f ? eulerX - 360f : eulerX;
        }
    }
}
