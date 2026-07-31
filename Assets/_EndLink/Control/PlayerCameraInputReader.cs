using UnityEngine;
using UnityEngine.InputSystem;

namespace EndLink.Core
{
    /// <summary>
    /// 玩家相机输入读取器。
    /// 只负责读取 Look 输入，供自由视角旋转和硬锁目标方向切换复用，
    /// 不处理相机旋转、目标选择、自动距离变化或 Cinemachine 参数。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCameraInputReader : MonoBehaviour
    {
        /// <summary>
        /// 当前视角旋转输入。
        /// 鼠标通常是每帧 delta，手柄右摇杆通常是 -1 到 1 的持续输入。
        /// </summary>
        public Vector2 LookInput { get; private set; }

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
