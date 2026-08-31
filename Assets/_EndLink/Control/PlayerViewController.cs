using System;
using EndLink.Combat;
using UnityEngine;

namespace EndLink.Core
{
    /// <summary>玩家当前采用的视角与目标锁定风格。</summary>
    public enum PlayerViewMode
    {
        /// <summary>较高、较远的自由镜头，继续使用自动软目标。</summary>
        FastAction = 0,

        /// <summary>较近、较低的镜头，并允许通过目标锁定输入建立硬锁。</summary>
        SoulsLike = 1
    }

    /// <summary>
    /// 一组第三人称镜头参数。
    /// PlayerViewController 分别保存高速动作与魂类两套预设，ThirdPersonCameraController 只负责应用。
    /// </summary>
    [Serializable]
    public struct PlayerViewSettings
    {
        [Header("目标与旋转")]
        [Tooltip("相机旋转中心相对玩家根节点的世界空间偏移。Y 越小，镜头观察中心越低。")]
        [SerializeField]
        private Vector3 targetWorldOffset;

        [Tooltip("手柄右摇杆水平旋转速度，单位为度/秒。")]
        [SerializeField, Min(0f)]
        private float gamepadYawSpeed;

        [Tooltip("手柄右摇杆垂直旋转速度，单位为度/秒。")]
        [SerializeField, Min(0f)]
        private float gamepadPitchSpeed;

        [Tooltip("鼠标水平视角灵敏度。")]
        [SerializeField, Min(0f)]
        private float mouseYawSensitivity;

        [Tooltip("鼠标垂直视角灵敏度。")]
        [SerializeField, Min(0f)]
        private float mousePitchSensitivity;

        [Tooltip("允许的最低俯仰角。")]
        [SerializeField]
        private float minPitch;

        [Tooltip("允许的最高俯仰角。")]
        [SerializeField]
        private float maxPitch;

        [Header("距离")]
        [Tooltip("脱战待机时的镜头距离。")]
        [SerializeField, Min(0.01f)]
        private float idleDistance;

        [Tooltip("移动或战斗时的镜头距离。")]
        [SerializeField, Min(0.01f)]
        private float activeDistance;

        [Tooltip("镜头拉近到待机距离时的平滑时间。")]
        [SerializeField, Min(0.01f)]
        private float idleDistanceSmoothTime;

        [Tooltip("镜头拉远到移动/战斗距离时的平滑时间。")]
        [SerializeField, Min(0.01f)]
        private float activeDistanceSmoothTime;

        [Header("Cinemachine 构图")]
        [Tooltip("Cinemachine Third Person Follow 的肩部支点偏移。")]
        [SerializeField]
        private Vector3 shoulderOffset;

        [Tooltip("肩部支点到相机手臂的垂直长度。")]
        [SerializeField]
        private float verticalArmLength;

        [Tooltip("相机肩位。0 为左肩，1 为右肩，0.5 为居中。")]
        [SerializeField, Range(0f, 1f)]
        private float cameraSide;

        [Tooltip("Cinemachine 跟随阻尼。")]
        [SerializeField]
        private Vector3 damping;

        [Tooltip("相机垂直视场角。")]
        [SerializeField, Range(1f, 179f)]
        private float fieldOfView;

        public Vector3 TargetWorldOffset => targetWorldOffset;
        public float GamepadYawSpeed => gamepadYawSpeed;
        public float GamepadPitchSpeed => gamepadPitchSpeed;
        public float MouseYawSensitivity => mouseYawSensitivity;
        public float MousePitchSensitivity => mousePitchSensitivity;
        public float MinPitch => minPitch;
        public float MaxPitch => maxPitch;
        public float IdleDistance => idleDistance;
        public float ActiveDistance => activeDistance;
        public float IdleDistanceSmoothTime => idleDistanceSmoothTime;
        public float ActiveDistanceSmoothTime => activeDistanceSmoothTime;
        public Vector3 ShoulderOffset => shoulderOffset;
        public float VerticalArmLength => verticalArmLength;
        public float CameraSide => cameraSide;
        public Vector3 Damping => damping;
        public float FieldOfView => fieldOfView;

        internal PlayerViewSettings(
            Vector3 targetWorldOffset,
            float gamepadYawSpeed,
            float gamepadPitchSpeed,
            float mouseYawSensitivity,
            float mousePitchSensitivity,
            float minPitch,
            float maxPitch,
            float idleDistance,
            float activeDistance,
            float idleDistanceSmoothTime,
            float activeDistanceSmoothTime,
            Vector3 shoulderOffset,
            float verticalArmLength,
            float cameraSide,
            Vector3 damping,
            float fieldOfView)
        {
            this.targetWorldOffset = targetWorldOffset;
            this.gamepadYawSpeed = gamepadYawSpeed;
            this.gamepadPitchSpeed = gamepadPitchSpeed;
            this.mouseYawSensitivity = mouseYawSensitivity;
            this.mousePitchSensitivity = mousePitchSensitivity;
            this.minPitch = minPitch;
            this.maxPitch = maxPitch;
            this.idleDistance = idleDistance;
            this.activeDistance = activeDistance;
            this.idleDistanceSmoothTime = idleDistanceSmoothTime;
            this.activeDistanceSmoothTime = activeDistanceSmoothTime;
            this.shoulderOffset = shoulderOffset;
            this.verticalArmLength = verticalArmLength;
            this.cameraSide = cameraSide;
            this.damping = damping;
            this.fieldOfView = fieldOfView;
        }

        /// <summary>创建第一版偏高、偏远、旋转较快的高速动作视角预设。</summary>
        public static PlayerViewSettings CreateFastActionDefault()
        {
            return new PlayerViewSettings(
                new Vector3(0f, 1.65f, 0f),
                190f,
                120f,
                0.12f,
                0.1f,
                -30f,
                55f,
                5.5f,
                7f,
                1.2f,
                0.25f,
                new Vector3(0.8f, 1.45f, 0f),
                0f,
                1f,
                new Vector3(0.22f, 0.28f, 0.24f),
                52f);
        }

        /// <summary>创建偏近、偏低、旋转较慢的魂类视角预设。</summary>
        public static PlayerViewSettings CreateSoulsLikeDefault()
        {
            return new PlayerViewSettings(
                new Vector3(0f, 1.2f, 0f),
                125f,
                85f,
                0.08f,
                0.07f,
                -22f,
                55f,
                3.6f,
                4.2f,
                0.65f,
                0.2f,
                new Vector3(0.35f, 0.5f, 0f),
                0.1f,
                0.5f,
                new Vector3(0.15f, 0.2f, 0.16f),
                50f);
        }

        /// <summary>钳制 Inspector 数据，避免无效距离或角度破坏相机。</summary>
        internal PlayerViewSettings Sanitized()
        {
            PlayerViewSettings value = this;
            value.gamepadYawSpeed = Mathf.Max(0f, value.gamepadYawSpeed);
            value.gamepadPitchSpeed = Mathf.Max(0f, value.gamepadPitchSpeed);
            value.mouseYawSensitivity = Mathf.Max(0f, value.mouseYawSensitivity);
            value.mousePitchSensitivity = Mathf.Max(0f, value.mousePitchSensitivity);

            if (value.minPitch > value.maxPitch)
            {
                (value.minPitch, value.maxPitch) = (value.maxPitch, value.minPitch);
            }

            value.idleDistance = Mathf.Max(0.01f, value.idleDistance);
            value.activeDistance = Mathf.Max(value.idleDistance, value.activeDistance);
            value.idleDistanceSmoothTime = Mathf.Max(0.01f, value.idleDistanceSmoothTime);
            value.activeDistanceSmoothTime = Mathf.Max(0.01f, value.activeDistanceSmoothTime);
            value.cameraSide = Mathf.Clamp01(value.cameraSide);
            value.damping.x = Mathf.Max(0f, value.damping.x);
            value.damping.y = Mathf.Max(0f, value.damping.y);
            value.damping.z = Mathf.Max(0f, value.damping.z);
            value.fieldOfView = Mathf.Clamp(value.fieldOfView, 1f, 179f);
            return value;
        }
    }

    /// <summary>
    /// 玩家视角模式协调器。
    /// 挂在第三人称相机物体上，负责切换镜头预设，并在魂类模式下协调硬锁目标、镜头朝向和玩家锁定操控。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ThirdPersonCameraController))]
    public sealed class PlayerViewController : MonoBehaviour
    {
        [Header("模式")]
        [Tooltip("进入游戏时采用的视角模式。两套参数都在本组件中独立保存和调教。")]
        [SerializeField]
        private PlayerViewMode viewMode = PlayerViewMode.FastAction;

        [Header("玩家引用")]
        [Tooltip("玩家目标选择组件。为空时会尝试从相机 Follow Target 自动获取。")]
        [SerializeField]
        private PlayerTargeting playerTargeting;

        [Tooltip("玩家输入读取器。为空时会尝试从相机 Follow Target 自动获取。")]
        [SerializeField]
        private PlayerInputReader playerInputReader;

        [Tooltip("相机 Look 输入读取器。硬锁时用鼠标横向滑动或手柄右摇杆左右推动切换目标；为空时从当前相机物体自动获取。")]
        [SerializeField]
        private PlayerCameraInputReader cameraInputReader;

        [Tooltip("玩家移动控制器。魂类硬锁时由本组件指定持续面向目标，并启用目标相对移动。为空时会尝试从相机 Follow Target 自动获取。")]
        [SerializeField]
        private PlayerController playerController;

        [Header("高速动作视角")]
        [Tooltip("Fast Action 模式使用的偏高、偏远和较快旋转参数。首次升级组件时会从当前 ThirdPersonCameraController 自动捕获。")]
        [SerializeField]
        private PlayerViewSettings fastActionSettings = PlayerViewSettings.CreateFastActionDefault();

        [Header("魂类视角")]
        [Tooltip("Souls Like 模式使用的近距、低机位和较慢旋转参数。")]
        [SerializeField]
        private PlayerViewSettings soulsLikeSettings = PlayerViewSettings.CreateSoulsLikeDefault();

        [Header("射击瞄准")]
        [Tooltip("瞄准期间的镜头距离。该值只作为临时覆盖，不会修改高速或魂类视角预设。")]
        [SerializeField, Min(0.01f)]
        private float aimDistance = 2.6f;

        [Tooltip("瞄准期间的相机垂直视场角。数值越小，目标在画面中越大。")]
        [SerializeField, Range(1f, 179f)]
        private float aimFieldOfView = 44f;

        [Tooltip("瞄准期间的越肩支点偏移。X 控制左右肩位，Y 控制瞄准视点高度。")]
        [SerializeField]
        private Vector3 aimShoulderOffset = new(0.9f, 1.25f, 0f);

        [Tooltip("进入瞄准时镜头拉近所用的平滑时间。")]
        [SerializeField, Min(0.01f)]
        private float aimEnterSmoothTime = 0.12f;

        [Tooltip("退出瞄准时镜头恢复所用的平滑时间。")]
        [SerializeField, Min(0.01f)]
        private float aimExitSmoothTime = 0.22f;

        [Tooltip("硬锁时镜头平滑转向目标所用的阻尼时间。越小跟随越紧，越大越柔和。")]
        [SerializeField, Min(0.01f)]
        private float hardLockRotationSmoothTime = 0.12f;

        [Header("移动步态")]
        [Tooltip("是否根据玩家实际移动速度叠加轻微镜头步态晃动。")]
        [SerializeField]
        private bool enableLocomotionBob = true;

        [Tooltip("普通移动时的左右、上下晃动幅度，单位为米。")]
        [SerializeField]
        private Vector2 walkBobAmplitude = new(0.018f, 0.025f);

        [Tooltip("冲刺时的左右、上下晃动幅度，单位为米。")]
        [SerializeField]
        private Vector2 sprintBobAmplitude = new(0.028f, 0.038f);

        [Tooltip("普通移动与冲刺时每秒的步态周期数。")]
        [SerializeField]
        private Vector2 bobFrequency = new(1.65f, 2.15f);

        [Tooltip("达到完整步态幅度所需的水平速度，单位米/秒。")]
        [SerializeField, Min(0.01f)]
        private float fullBobSpeed = 3.5f;

        [Tooltip("低于该水平速度时视为静止。")]
        [SerializeField, Min(0f)]
        private float minimumBobSpeed = 0.12f;

        [Tooltip("步态进入和退出的权重变化速度。")]
        [SerializeField, Min(0.01f)]
        private float bobBlendSpeed = 7f;

        [Tooltip("射击瞄准时保留的步态比例，0 表示瞄准时完全关闭晃动。")]
        [SerializeField, Range(0f, 1f)]
        private float aimBobMultiplier = 0.18f;

        [Header("硬锁目标切换")]
        [Tooltip("鼠标在硬锁期间需要累计多少横向像素位移才切换一次目标。越小越灵敏，越大越不容易误触。")]
        [SerializeField, Min(1f)]
        private float mouseTargetSwitchThreshold = 36f;

        [Tooltip("手柄右摇杆横向输入达到该值时切换一次目标。切换后必须先让摇杆回中。")]
        [SerializeField, Range(0.1f, 1f)]
        private float gamepadTargetSwitchThreshold = 0.65f;

        [Tooltip("手柄右摇杆横向输入回落到该值以内时，允许下一次目标切换。必须小于触发阈值。")]
        [SerializeField, Range(0f, 0.9f)]
        private float gamepadTargetSwitchResetThreshold = 0.25f;

        [Tooltip("两次硬锁目标切换之间的最短间隔，避免鼠标快速抖动或设备切换造成连续跳转。")]
        [SerializeField, Min(0f)]
        private float targetSwitchCooldown = 0.18f;

        [SerializeField, HideInInspector]
        private bool fastActionSettingsInitialized;

        private ThirdPersonCameraController _cameraController;
        private PlayerViewMode _appliedMode;
        private bool _initialized;
        private float _mouseTargetSwitchAccumulator;
        private float _nextTargetSwitchTime;
        private bool _gamepadTargetSwitchArmed = true;
        private bool _aimViewActive;
        private PlayerStateMachine _playerStateMachine;
        private float _bobPhase;
        private float _bobWeight;

        /// <summary>当前选择的视角模式。</summary>
        public PlayerViewMode ViewMode => viewMode;

        /// <summary>当前是否正在魂类模式中硬锁目标。</summary>
        public bool IsHardLocked => viewMode == PlayerViewMode.SoulsLike
            && playerTargeting != null
            && playerTargeting.IsHardLocked;

        /// <summary>当前硬锁目标；没有硬锁时返回 null。</summary>
        public Transform LockedTarget => IsHardLocked ? playerTargeting.HardLockedTarget : null;

        /// <summary>当前是否由射击瞄准临时覆盖镜头构图。</summary>
        public bool IsAimViewActive => _aimViewActive;

        private void Reset()
        {
            _cameraController = GetComponent<ThirdPersonCameraController>();
            fastActionSettings = _cameraController != null
                ? _cameraController.CaptureViewSettings()
                : PlayerViewSettings.CreateFastActionDefault();
            soulsLikeSettings = PlayerViewSettings.CreateSoulsLikeDefault();
            hardLockRotationSmoothTime = 0.12f;
            mouseTargetSwitchThreshold = 36f;
            gamepadTargetSwitchThreshold = 0.65f;
            gamepadTargetSwitchResetThreshold = 0.25f;
            targetSwitchCooldown = 0.18f;
            aimDistance = 2.6f;
            aimFieldOfView = 44f;
            aimShoulderOffset = new Vector3(0.9f, 1.25f, 0f);
            aimEnterSmoothTime = 0.12f;
            aimExitSmoothTime = 0.22f;
            enableLocomotionBob = true;
            walkBobAmplitude = new Vector2(0.018f, 0.025f);
            sprintBobAmplitude = new Vector2(0.028f, 0.038f);
            bobFrequency = new Vector2(1.65f, 2.15f);
            fullBobSpeed = 3.5f;
            minimumBobSpeed = 0.12f;
            bobBlendSpeed = 7f;
            aimBobMultiplier = 0.18f;
            fastActionSettingsInitialized = true;
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            ApplyMode(viewMode, true);
        }

        private void Start()
        {
            // 所有 Awake 完成后再补一次引用，兼容相机控制器运行时创建 CameraTarget 的场景。
            ResolvePlayerReferences();
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            if (_appliedMode != viewMode)
            {
                ApplyMode(viewMode, false);
            }

            UpdateLocomotionBob(Time.deltaTime);

            if (_aimViewActive)
            {
                playerInputReader?.ConsumeTargetLockPressed();
                ResetTargetSwitchInput();
                _cameraController.ClearLockTarget();
                return;
            }

            bool handledTargetLockInput = false;
            if (playerInputReader != null && playerInputReader.ConsumeTargetLockPressed())
            {
                if (viewMode == PlayerViewMode.SoulsLike)
                {
                    playerTargeting?.ToggleHardLock();
                    ResetTargetSwitchInput();
                    handledTargetLockInput = true;
                }
            }

            if (viewMode == PlayerViewMode.SoulsLike
                && playerTargeting != null
                && playerTargeting.IsHardLocked)
            {
                if (!handledTargetLockInput)
                {
                    HandleTargetSwitchInput();
                }
            }
            else
            {
                ResetTargetSwitchInput();
            }

            RefreshCameraLockTarget();
        }

        private void OnDisable()
        {
            _bobPhase = 0f;
            _bobWeight = 0f;
            _cameraController?.ClearAdditiveViewOffset();

            if (!_initialized)
            {
                return;
            }

            playerTargeting?.ClearHardLock();
            playerController?.ClearFacingTarget();
            _cameraController.ClearLockTarget();
            _cameraController.ClearTemporaryViewOverride();
            _aimViewActive = false;
            _cameraController.ApplyViewSettings(fastActionSettings, false);
        }

        private void OnValidate()
        {
            EnsureFastActionSettingsInitialized();
            fastActionSettings = fastActionSettings.Sanitized();
            soulsLikeSettings = soulsLikeSettings.Sanitized();
            hardLockRotationSmoothTime = Mathf.Max(0.01f, hardLockRotationSmoothTime);
            mouseTargetSwitchThreshold = Mathf.Max(1f, mouseTargetSwitchThreshold);
            gamepadTargetSwitchThreshold = Mathf.Clamp(gamepadTargetSwitchThreshold, 0.1f, 1f);
            gamepadTargetSwitchResetThreshold = Mathf.Clamp(
                gamepadTargetSwitchResetThreshold,
                0f,
                Mathf.Max(0f, gamepadTargetSwitchThreshold - 0.05f));
            targetSwitchCooldown = Mathf.Max(0f, targetSwitchCooldown);
            aimDistance = Mathf.Max(0.01f, aimDistance);
            aimFieldOfView = Mathf.Clamp(aimFieldOfView, 1f, 179f);
            aimEnterSmoothTime = Mathf.Max(0.01f, aimEnterSmoothTime);
            aimExitSmoothTime = Mathf.Max(0.01f, aimExitSmoothTime);
            walkBobAmplitude.x = Mathf.Max(0f, walkBobAmplitude.x);
            walkBobAmplitude.y = Mathf.Max(0f, walkBobAmplitude.y);
            sprintBobAmplitude.x = Mathf.Max(0f, sprintBobAmplitude.x);
            sprintBobAmplitude.y = Mathf.Max(0f, sprintBobAmplitude.y);
            bobFrequency.x = Mathf.Max(0f, bobFrequency.x);
            bobFrequency.y = Mathf.Max(0f, bobFrequency.y);
            fullBobSpeed = Mathf.Max(0.01f, fullBobSpeed);
            minimumBobSpeed = Mathf.Clamp(minimumBobSpeed, 0f, fullBobSpeed);
            bobBlendSpeed = Mathf.Max(0.01f, bobBlendSpeed);
            aimBobMultiplier = Mathf.Clamp01(aimBobMultiplier);

            if (Application.isPlaying && _initialized && isActiveAndEnabled)
            {
                ApplyMode(viewMode, false);
                if (_aimViewActive)
                {
                    ApplyAimViewOverride();
                }
            }
        }

        /// <summary>
        /// 从 ThirdPersonCameraController 当前 Inspector 参数重新抓取高速动作预设。
        /// 用于保留旧相机调参，或把执行层的临时参数同步回本组件。
        /// </summary>
        [ContextMenu("Capture Current Camera As Fast Action Preset")]
        public void CaptureCurrentAsFastAction()
        {
            _cameraController ??= GetComponent<ThirdPersonCameraController>();
            if (_cameraController == null)
            {
                return;
            }

            fastActionSettings = _cameraController.CaptureViewSettings().Sanitized();
            fastActionSettingsInitialized = true;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// 切换视角模式。第一版主要用于 Inspector 和调试入口，后续设置菜单可以复用该方法。
        /// </summary>
        public void SetViewMode(PlayerViewMode mode)
        {
            viewMode = mode;

            if (_initialized && isActiveAndEnabled)
            {
                ApplyMode(mode, false);
            }
        }

        /// <summary>
        /// 开关射击瞄准镜头。瞄准只临时覆盖构图；退出后继续使用当前 Fast Action / Souls Like 预设。
        /// </summary>
        public void SetAimView(bool active)
        {
            Initialize();
            if (_cameraController == null)
            {
                return;
            }

            if (active)
            {
                _aimViewActive = true;
                playerTargeting?.ClearHardLock();
                _cameraController.ClearLockTarget();
                ResetTargetSwitchInput();
                ApplyAimViewOverride();
                return;
            }

            if (!_aimViewActive)
            {
                return;
            }

            _aimViewActive = false;
            _cameraController.ClearTemporaryViewOverride();
            RefreshCameraLockTarget();
        }

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _cameraController = GetComponent<ThirdPersonCameraController>();
            cameraInputReader ??= GetComponent<PlayerCameraInputReader>();
            EnsureFastActionSettingsInitialized();
            ResolvePlayerReferences();

            _appliedMode = viewMode;
            _initialized = true;
        }

        private void ResolvePlayerReferences()
        {
            Transform followTarget = _cameraController != null ? _cameraController.FollowTarget : null;
            if (followTarget == null)
            {
                return;
            }

            playerTargeting ??= followTarget.GetComponentInParent<PlayerTargeting>();
            playerInputReader ??= followTarget.GetComponentInParent<PlayerInputReader>();
            playerController ??= followTarget.GetComponentInParent<PlayerController>();
            _playerStateMachine ??= followTarget.GetComponentInParent<PlayerStateMachine>();
        }

        private void UpdateLocomotionBob(float deltaTime)
        {
            if (_cameraController == null)
            {
                return;
            }

            if (!enableLocomotionBob || playerController == null)
            {
                _bobWeight = 0f;
                _cameraController.ClearAdditiveViewOffset();
                return;
            }

            bool isLocomotionState = _playerStateMachine == null
                || _playerStateMachine.CurrentStateId == PlayerStateId.Move;
            float planarSpeed = playerController.PlanarSpeed;
            bool canBob = isLocomotionState
                && playerController.IsGrounded
                && planarSpeed > minimumBobSpeed;

            float speedWeight = canBob
                ? Mathf.InverseLerp(minimumBobSpeed, fullBobSpeed, planarSpeed)
                : 0f;
            float targetWeight = _aimViewActive
                ? speedWeight * aimBobMultiplier
                : speedWeight;

            _bobWeight = Mathf.MoveTowards(
                _bobWeight,
                targetWeight,
                bobBlendSpeed * Mathf.Max(0f, deltaTime));

            if (canBob)
            {
                float sprintBlend = playerController.IsSprinting ? 1f : 0f;
                float frequency = Mathf.Lerp(bobFrequency.x, bobFrequency.y, sprintBlend);
                frequency *= Mathf.Lerp(0.7f, 1f, speedWeight);
                _bobPhase = Mathf.Repeat(
                    _bobPhase + deltaTime * frequency * Mathf.PI * 2f,
                    Mathf.PI * 4f);
            }

            if (_bobWeight <= 0.0001f)
            {
                _cameraController.ClearAdditiveViewOffset();
                return;
            }

            float sprintAmount = playerController.IsSprinting ? 1f : 0f;
            Vector2 amplitude = Vector2.Lerp(walkBobAmplitude, sprintBobAmplitude, sprintAmount);
            float horizontal = Mathf.Sin(_bobPhase * 0.5f) * amplitude.x;
            float vertical = -Mathf.Cos(_bobPhase) * amplitude.y;
            Vector3 shoulderMotion = new Vector3(horizontal, vertical, 0f) * _bobWeight;
            _cameraController.SetAdditiveViewOffset(Vector3.zero, shoulderMotion);
        }

        private void ApplyMode(PlayerViewMode mode, bool snapDistance)
        {
            _appliedMode = mode;

            if (mode == PlayerViewMode.FastAction)
            {
                playerTargeting?.ClearHardLock();
                if (!_aimViewActive)
                {
                    playerController?.ClearFacingTarget();
                }
                _cameraController.ClearLockTarget();
                _cameraController.ApplyViewSettings(fastActionSettings, snapDistance);
                return;
            }

            _cameraController.ApplyViewSettings(soulsLikeSettings, snapDistance);
            RefreshCameraLockTarget();
        }

        private void RefreshCameraLockTarget()
        {
            if (_aimViewActive)
            {
                _cameraController.ClearLockTarget();
                return;
            }

            if (viewMode != PlayerViewMode.SoulsLike || playerTargeting == null || !playerTargeting.IsHardLocked)
            {
                _cameraController.ClearLockTarget();
                playerController?.ClearFacingTarget();
                return;
            }

            Transform lockPoint = playerTargeting.CurrentLockPoint;
            _cameraController.SetLockTarget(
                lockPoint,
                hardLockRotationSmoothTime);
            playerController?.SetFacingTarget(lockPoint);
        }

        private void ApplyAimViewOverride()
        {
            _cameraController.SetTemporaryViewOverride(
                aimDistance,
                aimFieldOfView,
                aimShoulderOffset,
                aimEnterSmoothTime,
                aimExitSmoothTime);
        }

        private void HandleTargetSwitchInput()
        {
            cameraInputReader ??= GetComponent<PlayerCameraInputReader>();
            if (cameraInputReader == null)
            {
                return;
            }

            Vector2 lookInput = cameraInputReader.LookInput;
            if (cameraInputReader.IsPointerLookInput)
            {
                HandleMouseTargetSwitch(lookInput);
                return;
            }

            HandleGamepadTargetSwitch(lookInput.x);
        }

        private void HandleMouseTargetSwitch(Vector2 lookInput)
        {
            _gamepadTargetSwitchArmed = true;

            float horizontal = lookInput.x;
            if (Mathf.Abs(horizontal) <= Mathf.Abs(lookInput.y))
            {
                _mouseTargetSwitchAccumulator = Mathf.MoveTowards(
                    _mouseTargetSwitchAccumulator,
                    0f,
                    Mathf.Abs(lookInput.y));
                return;
            }

            _mouseTargetSwitchAccumulator = Mathf.Clamp(
                _mouseTargetSwitchAccumulator + horizontal,
                -mouseTargetSwitchThreshold,
                mouseTargetSwitchThreshold);

            if (Mathf.Abs(_mouseTargetSwitchAccumulator) < mouseTargetSwitchThreshold
                || Time.unscaledTime < _nextTargetSwitchTime)
            {
                return;
            }

            int direction = _mouseTargetSwitchAccumulator < 0f ? -1 : 1;
            playerTargeting.SwitchHardLockTarget(direction);
            _mouseTargetSwitchAccumulator = 0f;
            _nextTargetSwitchTime = Time.unscaledTime + targetSwitchCooldown;
        }

        private void HandleGamepadTargetSwitch(float horizontal)
        {
            _mouseTargetSwitchAccumulator = 0f;

            float absoluteHorizontal = Mathf.Abs(horizontal);
            if (absoluteHorizontal <= gamepadTargetSwitchResetThreshold)
            {
                _gamepadTargetSwitchArmed = true;
                return;
            }

            if (!_gamepadTargetSwitchArmed
                || absoluteHorizontal < gamepadTargetSwitchThreshold
                || Time.unscaledTime < _nextTargetSwitchTime)
            {
                return;
            }

            playerTargeting.SwitchHardLockTarget(horizontal < 0f ? -1 : 1);
            _gamepadTargetSwitchArmed = false;
            _nextTargetSwitchTime = Time.unscaledTime + targetSwitchCooldown;
        }

        private void ResetTargetSwitchInput()
        {
            _mouseTargetSwitchAccumulator = 0f;
            _gamepadTargetSwitchArmed = false;
        }

        private void EnsureFastActionSettingsInitialized()
        {
            if (fastActionSettingsInitialized)
            {
                return;
            }

            _cameraController ??= GetComponent<ThirdPersonCameraController>();
            fastActionSettings = _cameraController != null
                ? _cameraController.CaptureViewSettings()
                : PlayerViewSettings.CreateFastActionDefault();
            fastActionSettingsInitialized = true;
        }
    }
}
