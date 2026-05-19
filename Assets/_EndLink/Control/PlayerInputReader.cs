using UnityEngine;
using UnityEngine.InputSystem;

namespace EndLink.Core
{
    /// <summary>
    /// 玩家输入读取器。
    /// 只负责把新版 Input System 的输入流缓存成业务层可读取的数据，
    /// 不处理移动、旋转、动画、状态切换或任何物理逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        /// <summary>
        /// 当前移动输入。X 表示左右，Y 表示前后。
        /// </summary>
        public Vector2 MoveInput { get; private set; }

        private InputSystem_Actions _inputActions;
        private InputSystem_Actions.PlayerActions _playerActions;
        private bool _attackPressed;

        private void Awake()
        {
            _inputActions = new InputSystem_Actions();
            _playerActions = _inputActions.Player;

            _playerActions.Move.performed += OnMoveChanged;
            _playerActions.Move.canceled += OnMoveCanceled;
            _playerActions.Attack.performed += OnAttackPerformed;
        }

        private void OnEnable()
        {
            _playerActions.Enable();
        }

        private void OnDisable()
        {
            MoveInput = Vector2.zero;
            _attackPressed = false;
            _playerActions.Disable();
        }

        private void OnDestroy()
        {
            _playerActions.Move.performed -= OnMoveChanged;
            _playerActions.Move.canceled -= OnMoveCanceled;
            _playerActions.Attack.performed -= OnAttackPerformed;
            _inputActions.Dispose();
        }

        /// <summary>
        /// 消费一次攻击输入。
        /// 返回 true 后会立即清空，避免同一次输入被多个状态重复处理。
        /// </summary>
        public bool ConsumeAttackPressed()
        {
            if (!_attackPressed)
            {
                return false;
            }

            _attackPressed = false;
            return true;
        }

        private void OnMoveChanged(InputAction.CallbackContext context)
        {
            MoveInput = context.ReadValue<Vector2>();
        }

        private void OnMoveCanceled(InputAction.CallbackContext context)
        {
            MoveInput = Vector2.zero;
        }

        private void OnAttackPerformed(InputAction.CallbackContext context)
        {
            if (context.ReadValueAsButton())
            {
                _attackPressed = true;
            }
        }
    }
}
