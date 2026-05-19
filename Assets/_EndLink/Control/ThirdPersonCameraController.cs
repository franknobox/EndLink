using Unity.Cinemachine;
using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 第三人称相机控制器。
    /// 负责把玩家相机输入转换为 CameraTarget 的旋转，以及 CinemachineThirdPersonFollow 的缩放距离。
    /// 实际跟随、缓动、越肩构图仍交给 Cinemachine 处理。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCameraInputReader))]
    [RequireComponent(typeof(CinemachineCamera))]
    [RequireComponent(typeof(CinemachineThirdPersonFollow))]
    public sealed class ThirdPersonCameraController : MonoBehaviour
    {
        [Header("目标")]
        [Tooltip("玩家根节点。CameraTarget 会跟随这个对象的位置，但不会直接旋转玩家本体。")]
        [SerializeField]
        private Transform followTarget;

        [Tooltip("相机旋转中心。建议放在玩家子物体 CameraTarget 上，高度在胸口到头部之间。")]
        [SerializeField]
        private Transform cameraTarget;

        [Tooltip("CameraTarget 相对玩家根节点的世界空间偏移。Y 越大，视角中心越高。")]
        [SerializeField]
        private Vector3 targetWorldOffset = new Vector3(0f, 1.65f, 0f);

        [Header("旋转")]
        [Tooltip("手柄右摇杆水平旋转速度，单位为度/秒。数值越大，左右转视角越快。")]
        [SerializeField, Min(0f)]
        private float gamepadYawSpeed = 190f;

        [Tooltip("手柄右摇杆垂直旋转速度，单位为度/秒。数值越大，上下抬压镜头越快。")]
        [SerializeField, Min(0f)]
        private float gamepadPitchSpeed = 120f;

        [Tooltip("鼠标水平灵敏度。数值越大，鼠标左右移动时视角转得越快。")]
        [SerializeField, Min(0f)]
        private float mouseYawSensitivity = 0.12f;

        [Tooltip("鼠标垂直灵敏度。数值越大，鼠标上下移动时视角抬压越快。")]
        [SerializeField, Min(0f)]
        private float mousePitchSensitivity = 0.1f;

        [Tooltip("相机最低俯仰角。数值越小，越允许镜头向下看或压低到角色身后。")]
        [SerializeField]
        private float minPitch = -30f;

        [Tooltip("相机最高俯仰角。数值越大，越允许镜头抬高形成更俯视的战场视角。")]
        [SerializeField]
        private float maxPitch = 55f;

        [Tooltip("是否反转垂直视角输入。开启后，向上推摇杆/移动鼠标会压低或抬高的方向相反。")]
        [SerializeField]
        private bool invertY;

        [Header("缩放")]
        [Tooltip("默认相机距离。进入场景时镜头会从这个距离开始，主要决定初始远近。")]
        [SerializeField]
        private float defaultDistance = 6.5f;

        [Tooltip("允许缩放到的最近距离。数值越小，滚轮拉近时越贴近角色。")]
        [SerializeField]
        private float minDistance = 5f;

        [Tooltip("允许缩放到的最远距离。数值越大，滚轮拉远时视野越开阔。")]
        [SerializeField]
        private float maxDistance = 8f;

        [Tooltip("滚轮缩放速度。数值越大，每次滚轮改变的相机距离越多。")]
        [SerializeField, Min(0f)]
        private float zoomSpeed = 10f;

        [Tooltip("缩放平滑时间。数值越大，镜头远近变化越柔和但响应更慢。")]
        [SerializeField, Min(0.01f)]
        private float zoomSmoothTime = 0.1f;

        [Header("Cinemachine 越肩构图")]
        [Tooltip("越肩支点偏移。X 影响左右越肩偏移，Y 影响镜头支点高度，Z 通常保持 0。")]
        [SerializeField]
        private Vector3 shoulderOffset = new Vector3(0.8f, 1.45f, 0f);

        [Tooltip("肩部到相机手臂的垂直长度。数值越大，镜头越偏高，战场可见范围通常更开阔。")]
        [SerializeField]
        private float verticalArmLength = 0f;

        [Tooltip("相机靠哪一侧肩膀。0 为左肩，1 为右肩，0.5 为居中。")]
        [SerializeField, Range(0f, 1f)]
        private float cameraSide = 1f;

        [Tooltip("Cinemachine 跟随阻尼。X/Y/Z 分别影响本地轴向跟随滞后，数值越大越柔但越拖。")]
        [SerializeField]
        private Vector3 damping = new Vector3(0.22f, 0.28f, 0.24f);

        [Tooltip("相机垂直视场角。数值越大画面越广、透视感越强；数值越小画面越窄、目标更近。")]
        [SerializeField, Range(1f, 179f)]
        private float fieldOfView = 52f;

        [Header("鼠标")]
        [Tooltip("启用该相机时是否锁定并隐藏鼠标。第三人称自由视角通常开启，调试 UI 时可关闭。")]
        [SerializeField]
        private bool lockCursorOnEnable = true;

        private PlayerCameraInputReader _inputReader;
        private CinemachineCamera _cinemachineCamera;
        private CinemachineThirdPersonFollow _thirdPersonFollow;

        private float _yaw;
        private float _pitch;
        private float _targetDistance;
        private float _currentDistance;
        private float _zoomSmoothVelocity;
        private bool _createdRuntimeCameraTarget;

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
        /// 根据缩放输入计算目标相机距离。
        /// zoomInput 为正时拉近镜头，为负时拉远镜头。
        /// </summary>
        public static float CalculateZoomDistance(float currentDistance, float zoomInput, float minDistance, float maxDistance)
        {
            if (minDistance > maxDistance)
            {
                (minDistance, maxDistance) = (maxDistance, minDistance);
            }

            return Mathf.Clamp(currentDistance - zoomInput, minDistance, maxDistance);
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

            Vector3 eulerAngles = cameraTarget != null ? cameraTarget.rotation.eulerAngles : transform.rotation.eulerAngles;
            _yaw = eulerAngles.y;
            _pitch = NormalizePitch(eulerAngles.x);

            _targetDistance = Mathf.Clamp(defaultDistance, minDistance, maxDistance);
            _currentDistance = _targetDistance;

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
            UpdateZoom(Time.deltaTime);
            ApplyCinemachineSettings();
        }

        private void OnValidate()
        {
            minDistance = Mathf.Max(0.01f, minDistance);
            maxDistance = Mathf.Max(minDistance, maxDistance);
            defaultDistance = Mathf.Clamp(defaultDistance, minDistance, maxDistance);

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
            cameraTarget.position = followTarget.position + targetWorldOffset;
        }

        private void UpdateRotation(float deltaTime)
        {
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

        private void UpdateZoom(float deltaTime)
        {
            float zoomDelta = _inputReader.ZoomInput * zoomSpeed;
            _targetDistance = CalculateZoomDistance(_targetDistance, zoomDelta, minDistance, maxDistance);

            _currentDistance = Mathf.SmoothDamp(
                _currentDistance,
                _targetDistance,
                ref _zoomSmoothVelocity,
                zoomSmoothTime,
                Mathf.Infinity,
                deltaTime);
        }

        private void ApplyCinemachineSettings()
        {
            if (cameraTarget != null)
            {
                _cinemachineCamera.Target.TrackingTarget = cameraTarget;
                _cinemachineCamera.Target.CustomLookAtTarget = false;
            }

            _thirdPersonFollow.ShoulderOffset = shoulderOffset;
            _thirdPersonFollow.VerticalArmLength = verticalArmLength;
            _thirdPersonFollow.CameraSide = cameraSide;
            _thirdPersonFollow.Damping = damping;
            _thirdPersonFollow.CameraDistance = _currentDistance;

            _cinemachineCamera.Lens.FieldOfView = fieldOfView;
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
