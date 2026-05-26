using UnityEngine;
using UnityEngine.InputSystem;

namespace EndLink.Core
{
    /// <summary>
    /// 玩家相机输入读取器。
    /// 只负责读取相机相关输入：视角旋转 Look 和滚轮缩放 Zoom，
    /// 不处理相机旋转、距离变化或 Cinemachine 参数。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCameraInputReader : MonoBehaviour
    {
        [Header("缩放输入")]
        [Tooltip("鼠标滚轮一次标准滚动对应的原始输入量。多数鼠标在 Windows 下为 120，只有滚轮缩放过快或过慢时才需要改。")]
        [SerializeField, Min(1f)]
        private float mouseScrollUnitsPerStep = 120f;

        /// <summary>
        /// 当前视角旋转输入。
        /// 鼠标通常是每帧 delta，手柄右摇杆通常是 -1 到 1 的持续输入。
        /// </summary>
        public Vector2 LookInput { get; private set; }

        /// <summary>
        /// 当前帧缩放输入。
        /// 正数表示拉近镜头，负数表示拉远镜头；该值每帧都会重新读取。
        /// </summary>
        public float ZoomInput { get; private set; }

        /// <summary>
        /// 最近一次 Look 输入是否来自鼠标/指针类设备。
        /// 相机控制器会据此区分“鼠标像素 delta”和“手柄每秒角速度”两类手感。
        /// </summary>
        public bool IsPointerLookInput { get; private set; }

        private InputSystem_Actions _inputActions;
        private InputActionMap _playerActionMap;
        private InputAction _lookAction;
        private bool _initialized;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            _playerActionMap.Enable();
        }

        private void OnDisable()
        {
            LookInput = Vector2.zero;
            ZoomInput = 0f;

            if (_initialized)
            {
                DisableInputActions();
            }
        }

        private void OnDestroy()
        {
            if (!_initialized)
            {
                return;
            }

            _lookAction.performed -= OnLookChanged;
            _lookAction.canceled -= OnLookCanceled;
            _inputActions.Dispose();
            _initialized = false;
        }

        private void Update()
        {
            // 当前默认 Player action map 没有独立 Zoom action。
            // 这里用新版 Input System 直接读取鼠标滚轮，后续如果在 inputactions 中加入 Zoom，
            // 只需要把这段替换为生成 action 的回调即可。
            ZoomInput = 0f;

            if (Mouse.current == null)
            {
                return;
            }

            float scrollY = Mouse.current.scroll.ReadValue().y;
            ZoomInput = Mathf.Clamp(scrollY / mouseScrollUnitsPerStep, -1f, 1f);
        }

        private void OnLookChanged(InputAction.CallbackContext context)
        {
            LookInput = context.ReadValue<Vector2>();
            IsPointerLookInput = context.control.device is Pointer;
        }

        private void OnLookCanceled(InputAction.CallbackContext context)
        {
            LookInput = Vector2.zero;
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            // 继续复用 Unity 根据 InputSystem_Actions.inputactions 生成的默认包装类。
            _inputActions = new InputSystem_Actions();
            _playerActionMap = _inputActions.asset.FindActionMap("Player", true);
            _lookAction = _inputActions.asset.FindAction("Player/Look", true);

            _lookAction.performed += OnLookChanged;
            _lookAction.canceled += OnLookCanceled;
            _initialized = true;
        }

        private void DisableInputActions()
        {
            if (_inputActions == null)
            {
                return;
            }

            _inputActions.asset?.Disable();
        }
    }
}
