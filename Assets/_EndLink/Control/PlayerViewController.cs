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

        /// <summary>创建第一版偏近、偏低、旋转较慢的魂类视角预设。</summary>
        public static PlayerViewSettings CreateSoulsLikeDefault()
        {
            return new PlayerViewSettings(
                new Vector3(0f, 1.35f, 0f),
                125f,
                85f,
                0.08f,
                0.07f,
                -22f,
                38f,
                3.2f,
                4.2f,
                0.65f,
                0.2f,
                new Vector3(0.35f, 0.75f, 0f),
                0.15f,
                0.5f,
                new Vector3(0.15f, 0.2f, 0.16f),
                48f);
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
    /// 挂在第三人称相机物体上，负责切换镜头预设，并在魂类模式下把目标锁定输入接到相机朝向。
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

        [Header("高速动作视角")]
        [Tooltip("Fast Action 模式使用的偏高、偏远和较快旋转参数。首次升级组件时会从当前 ThirdPersonCameraController 自动捕获。")]
        [SerializeField]
        private PlayerViewSettings fastActionSettings = PlayerViewSettings.CreateFastActionDefault();

        [Header("魂类视角")]
        [Tooltip("Souls Like 模式使用的近距、低机位和较慢旋转参数。")]
        [SerializeField]
        private PlayerViewSettings soulsLikeSettings = PlayerViewSettings.CreateSoulsLikeDefault();

        [Tooltip("硬锁时镜头平滑转向目标所用的阻尼时间。越小跟随越紧，越大越柔和。")]
        [SerializeField, Min(0.01f)]
        private float hardLockRotationSmoothTime = 0.12f;

        [SerializeField, HideInInspector]
        private bool fastActionSettingsInitialized;

        private ThirdPersonCameraController _cameraController;
        private PlayerViewMode _appliedMode;
        private bool _initialized;

        /// <summary>当前选择的视角模式。</summary>
        public PlayerViewMode ViewMode => viewMode;

        /// <summary>当前是否正在魂类模式中硬锁目标。</summary>
        public bool IsHardLocked => viewMode == PlayerViewMode.SoulsLike
            && playerTargeting != null
            && playerTargeting.IsHardLocked;

        /// <summary>当前硬锁目标；没有硬锁时返回 null。</summary>
        public Transform LockedTarget => IsHardLocked ? playerTargeting.HardLockedTarget : null;

        private void Reset()
        {
            _cameraController = GetComponent<ThirdPersonCameraController>();
            fastActionSettings = _cameraController != null
                ? _cameraController.CaptureViewSettings()
                : PlayerViewSettings.CreateFastActionDefault();
            soulsLikeSettings = PlayerViewSettings.CreateSoulsLikeDefault();
            hardLockRotationSmoothTime = 0.12f;
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

            if (playerInputReader != null && playerInputReader.ConsumeTargetLockPressed())
            {
                if (viewMode == PlayerViewMode.SoulsLike)
                {
                    playerTargeting?.ToggleHardLock();
                }
            }

            RefreshCameraLockTarget();
        }

        private void OnDisable()
        {
            if (!_initialized)
            {
                return;
            }

            playerTargeting?.ClearHardLock();
            _cameraController.ClearLockTarget();
            _cameraController.ApplyViewSettings(fastActionSettings, false);
        }

        private void OnValidate()
        {
            EnsureFastActionSettingsInitialized();
            fastActionSettings = fastActionSettings.Sanitized();
            soulsLikeSettings = soulsLikeSettings.Sanitized();
            hardLockRotationSmoothTime = Mathf.Max(0.01f, hardLockRotationSmoothTime);

            if (Application.isPlaying && _initialized && isActiveAndEnabled)
            {
                ApplyMode(viewMode, false);
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

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _cameraController = GetComponent<ThirdPersonCameraController>();
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
        }

        private void ApplyMode(PlayerViewMode mode, bool snapDistance)
        {
            _appliedMode = mode;

            if (mode == PlayerViewMode.FastAction)
            {
                playerTargeting?.ClearHardLock();
                _cameraController.ClearLockTarget();
                _cameraController.ApplyViewSettings(fastActionSettings, snapDistance);
                return;
            }

            _cameraController.ApplyViewSettings(soulsLikeSettings, snapDistance);
            RefreshCameraLockTarget();
        }

        private void RefreshCameraLockTarget()
        {
            if (viewMode != PlayerViewMode.SoulsLike || playerTargeting == null || !playerTargeting.IsHardLocked)
            {
                _cameraController.ClearLockTarget();
                return;
            }

            _cameraController.SetLockTarget(
                playerTargeting.CurrentLockPoint,
                hardLockRotationSmoothTime);
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
