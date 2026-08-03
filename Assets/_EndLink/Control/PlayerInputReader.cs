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

        /// <summary>
        /// 当前是否按住冲刺输入。默认绑定为 Left Shift。
        /// </summary>
        public bool SprintHeld { get; private set; }

        /// <summary>
        /// 当前是否按住防御输入。默认绑定为鼠标右键，手柄为 LB / L1。
        /// </summary>
        public bool GuardHeld { get; private set; }

        /// <summary>
        /// 当前是否按住射击瞄准输入。默认绑定为鼠标右键，手柄为 LT / L2。
        /// 是否真正进入瞄准由武器形态和玩家状态决定。
        /// </summary>
        public bool AimHeld { get; private set; }

        private InputSystem_Actions _inputActions;
        private InputActionMap _playerActionMap;
        private InputAction _moveAction;
        private InputAction _attackAction;
        private InputAction _sprintAction;
        private InputAction _dodgeAction;
        private InputAction _guardAction;
        private InputAction _aimAction;
        private InputAction _targetLockAction;
        private InputAction _previousAction;
        private InputAction _nextAction;
        private InputAction _jumpAction;
        private InputAction _interactAction;
        private InputAction _playerSkillAction;
        private InputAction _allySlotASkillAction;
        private InputAction _allySlotBSkillAction;
        private InputAction _previousWeaponFormAction;
        private InputAction _nextWeaponFormAction;
        private InputAction _partyUltimateAction;
        private bool _initialized;
        private bool _attackPressed;
        private bool _dodgePressed;
        private bool _targetLockPressed;
        private bool _previousPressed;
        private bool _nextPressed;
        private bool _jumpPressed;
        private bool _interactPressed;
        private bool _playerSkillPressed;
        private bool _allySlotASkillPressed;
        private bool _allySlotBSkillPressed;
        private bool _previousWeaponFormPressed;
        private bool _nextWeaponFormPressed;
        private bool _partyUltimatePressed;

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
            MoveInput = Vector2.zero;
            ResetPressedInputs();

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

            _moveAction.performed -= OnMoveChanged;
            _moveAction.canceled -= OnMoveCanceled;
            _attackAction.performed -= OnAttackPerformed;
            _sprintAction.started -= OnSprintStartedOrPerformed;
            _sprintAction.performed -= OnSprintStartedOrPerformed;
            _sprintAction.canceled -= OnSprintCanceled;
            _dodgeAction.performed -= OnDodgePerformed;
            _guardAction.started -= OnGuardStartedOrPerformed;
            _guardAction.performed -= OnGuardStartedOrPerformed;
            _guardAction.canceled -= OnGuardCanceled;
            _aimAction.started -= OnAimStartedOrPerformed;
            _aimAction.performed -= OnAimStartedOrPerformed;
            _aimAction.canceled -= OnAimCanceled;
            _targetLockAction.performed -= OnTargetLockPerformed;
            _previousAction.performed -= OnPreviousPerformed;
            _nextAction.performed -= OnNextPerformed;
            _jumpAction.performed -= OnJumpPerformed;
            _interactAction.performed -= OnInteractPerformed;
            _playerSkillAction.performed -= OnPlayerSkillPerformed;
            _allySlotASkillAction.performed -= OnAllySlotASkillPerformed;
            _allySlotBSkillAction.performed -= OnAllySlotBSkillPerformed;
            _previousWeaponFormAction.performed -= OnPreviousWeaponFormPerformed;
            _nextWeaponFormAction.performed -= OnNextWeaponFormPerformed;
            _partyUltimateAction.performed -= OnPartyUltimatePerformed;

            _inputActions.Dispose();
            _initialized = false;
        }

        /// <summary>
        /// 消费一次攻击输入。
        /// 返回 true 后会立即清空，避免同一次输入被多个状态重复处理。
        /// </summary>
        public bool ConsumeAttackPressed()
        {
            return ConsumePressed(ref _attackPressed);
        }

        /// <summary>
        /// 消费一次闪避输入，第一版默认键位为 Left Ctrl。
        /// 返回 true 后会立即清空，避免同一次输入被多个状态重复处理。
        /// </summary>
        public bool ConsumeDodgePressed()
        {
            return ConsumePressed(ref _dodgePressed);
        }

        /// <summary>
        /// 消费一次目标锁定输入，默认键位为鼠标中键，手柄为右摇杆按下。
        /// 这里只缓存锁定意图，实际软锁或硬锁规则由视角与索敌系统决定。
        /// </summary>
        public bool ConsumeTargetLockPressed()
        {
            return ConsumePressed(ref _targetLockPressed);
        }

        /// <summary>
        /// 消费一次“上一个”输入。
        /// 当前硬锁切换已经改用 Look 方向输入；该入口作为 Input Actions 的通用预留保留。
        /// </summary>
        public bool ConsumePreviousPressed()
        {
            return ConsumePressed(ref _previousPressed);
        }

        /// <summary>
        /// 消费一次“下一个”输入。
        /// 当前硬锁切换已经改用 Look 方向输入；该入口作为 Input Actions 的通用预留保留。
        /// </summary>
        public bool ConsumeNextPressed()
        {
            return ConsumePressed(ref _nextPressed);
        }

        /// <summary>
        /// 消费一次跳跃输入，第一版默认键位为 Space，手柄为 buttonSouth。
        /// 返回 true 后会立即清空，避免同一次输入被多个状态重复处理。
        /// </summary>
        public bool ConsumeJumpPressed()
        {
            return ConsumePressed(ref _jumpPressed);
        }

        /// <summary>
        /// 消费一次世界交互输入。
        /// 当前 Input Actions 中已存在 Player/Interact，具体按键由 inputactions 资产管理。
        /// </summary>
        public bool ConsumeInteractPressed()
        {
            return ConsumePressed(ref _interactPressed);
        }

        /// <summary>
        /// 消费一次旧主控主动技能输入。当前没有默认绑定，Q 已用于上一武器形态。
        /// </summary>
        public bool ConsumePlayerSkillPressed()
        {
            return ConsumePressed(ref _playerSkillPressed);
        }

        /// <summary>
        /// 消费一次队友 A 主动技能输入。当前默认不绑定键盘。
        /// </summary>
        public bool ConsumeAllySlotASkillPressed()
        {
            return ConsumePressed(ref _allySlotASkillPressed);
        }

        /// <summary>
        /// 消费一次队友 B 主动技能输入。当前默认不绑定键盘。
        /// </summary>
        public bool ConsumeAllySlotBSkillPressed()
        {
            return ConsumePressed(ref _allySlotBSkillPressed);
        }

        /// <summary>
        /// 消费一次切换到上一武器形态的输入，默认键位 Q，手柄为 D-Pad Up。
        /// </summary>
        public bool ConsumePreviousWeaponFormPressed()
        {
            return ConsumePressed(ref _previousWeaponFormPressed);
        }

        /// <summary>
        /// 消费一次切换到下一武器形态的输入，默认键位 E，手柄为 D-Pad Down。
        /// </summary>
        public bool ConsumeNextWeaponFormPressed()
        {
            return ConsumePressed(ref _nextWeaponFormPressed);
        }

        /// <summary>
        /// 消费一次全队终链奥义输入，默认键位 V。
        /// </summary>
        public bool ConsumePartyUltimatePressed()
        {
            return ConsumePressed(ref _partyUltimatePressed);
        }

        /// <summary>
        /// 覆盖旧主控/队友主动技能和终链奥义的键盘绑定。
        /// 该方法只改运行时 InputAction 实例，不写回 inputactions 资产。
        /// </summary>
        public void ApplyPartyCombatKeyboardBindings(
            Key playerSkillKey,
            Key allySlotASkillKey,
            Key allySlotBSkillKey,
            Key partyUltimateKey)
        {
            EnsureInitialized();
            ApplyKeyboardBindingOverride(_playerSkillAction, playerSkillKey);
            ApplyKeyboardBindingOverride(_allySlotASkillAction, allySlotASkillKey);
            ApplyKeyboardBindingOverride(_allySlotBSkillAction, allySlotBSkillKey);
            ApplyKeyboardBindingOverride(_partyUltimateAction, partyUltimateKey);
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

        private void OnSprintStartedOrPerformed(InputAction.CallbackContext context)
        {
            SprintHeld = context.ReadValueAsButton();
        }

        private void OnSprintCanceled(InputAction.CallbackContext context)
        {
            SprintHeld = false;
        }

        private void OnDodgePerformed(InputAction.CallbackContext context)
        {
            SetPressedIfButton(context, ref _dodgePressed);
        }

        private void OnGuardStartedOrPerformed(InputAction.CallbackContext context)
        {
            GuardHeld = context.ReadValueAsButton();
        }

        private void OnGuardCanceled(InputAction.CallbackContext context)
        {
            GuardHeld = false;
        }

        private void OnAimStartedOrPerformed(InputAction.CallbackContext context)
        {
            AimHeld = context.ReadValueAsButton();
        }

        private void OnAimCanceled(InputAction.CallbackContext context)
        {
            AimHeld = false;
        }

        private void OnTargetLockPerformed(InputAction.CallbackContext context)
        {
            SetPressedIfButton(context, ref _targetLockPressed);
        }

        private void OnPreviousPerformed(InputAction.CallbackContext context)
        {
            SetPressedIfButton(context, ref _previousPressed);
        }

        private void OnNextPerformed(InputAction.CallbackContext context)
        {
            SetPressedIfButton(context, ref _nextPressed);
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            SetPressedIfButton(context, ref _jumpPressed);
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            SetPressedIfButton(context, ref _interactPressed);
        }

        private void OnPlayerSkillPerformed(InputAction.CallbackContext context)
        {
            SetPressedIfButton(context, ref _playerSkillPressed);
        }

        private void OnAllySlotASkillPerformed(InputAction.CallbackContext context)
        {
            SetPressedIfButton(context, ref _allySlotASkillPressed);
        }

        private void OnAllySlotBSkillPerformed(InputAction.CallbackContext context)
        {
            SetPressedIfButton(context, ref _allySlotBSkillPressed);
        }

        private void OnPreviousWeaponFormPerformed(InputAction.CallbackContext context)
        {
            SetPressedIfButton(context, ref _previousWeaponFormPressed);
        }

        private void OnNextWeaponFormPerformed(InputAction.CallbackContext context)
        {
            SetPressedIfButton(context, ref _nextWeaponFormPressed);
        }

        private void OnPartyUltimatePerformed(InputAction.CallbackContext context)
        {
            SetPressedIfButton(context, ref _partyUltimatePressed);
        }

        private static void SetPressedIfButton(InputAction.CallbackContext context, ref bool pressedFlag)
        {
            if (context.ReadValueAsButton())
            {
                pressedFlag = true;
            }
        }

        private static bool ConsumePressed(ref bool pressedFlag)
        {
            if (!pressedFlag)
            {
                return false;
            }

            pressedFlag = false;
            return true;
        }

        private void ResetPressedInputs()
        {
            SprintHeld = false;
            GuardHeld = false;
            AimHeld = false;
            _attackPressed = false;
            _dodgePressed = false;
            _targetLockPressed = false;
            _previousPressed = false;
            _nextPressed = false;
            _jumpPressed = false;
            _interactPressed = false;
            _playerSkillPressed = false;
            _allySlotASkillPressed = false;
            _allySlotBSkillPressed = false;
            _previousWeaponFormPressed = false;
            _nextWeaponFormPressed = false;
            _partyUltimatePressed = false;
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _inputActions = new InputSystem_Actions();
            _playerActionMap = _inputActions.asset.FindActionMap("Player", true);
            _moveAction = _inputActions.asset.FindAction("Player/Move", true);
            _attackAction = _inputActions.asset.FindAction("Player/Attack", true);
            _sprintAction = _inputActions.asset.FindAction("Player/Sprint", true);
            _dodgeAction = _inputActions.asset.FindAction("Player/Dodge", true);
            _guardAction = _inputActions.asset.FindAction("Player/Guard", true);
            _aimAction = _inputActions.asset.FindAction("Player/Aim", true);
            _targetLockAction = _inputActions.asset.FindAction("Player/TargetLock", true);
            _previousAction = _inputActions.asset.FindAction("Player/Previous", true);
            _nextAction = _inputActions.asset.FindAction("Player/Next", true);
            _jumpAction = _inputActions.asset.FindAction("Player/Jump", true);
            _interactAction = _inputActions.asset.FindAction("Player/Interact", true);
            _playerSkillAction = _inputActions.asset.FindAction("Player/PlayerSkill", true);
            _allySlotASkillAction = _inputActions.asset.FindAction("Player/AllySlotASkill", true);
            _allySlotBSkillAction = _inputActions.asset.FindAction("Player/AllySlotBSkill", true);
            _previousWeaponFormAction = _inputActions.asset.FindAction("Player/PreviousWeaponForm", true);
            _nextWeaponFormAction = _inputActions.asset.FindAction("Player/NextWeaponForm", true);
            _partyUltimateAction = _inputActions.asset.FindAction("Player/PartyUltimate", true);
            _moveAction.performed += OnMoveChanged;
            _moveAction.canceled += OnMoveCanceled;
            _attackAction.performed += OnAttackPerformed;
            _sprintAction.started += OnSprintStartedOrPerformed;
            _sprintAction.performed += OnSprintStartedOrPerformed;
            _sprintAction.canceled += OnSprintCanceled;
            _dodgeAction.performed += OnDodgePerformed;
            _guardAction.started += OnGuardStartedOrPerformed;
            _guardAction.performed += OnGuardStartedOrPerformed;
            _guardAction.canceled += OnGuardCanceled;
            _aimAction.started += OnAimStartedOrPerformed;
            _aimAction.performed += OnAimStartedOrPerformed;
            _aimAction.canceled += OnAimCanceled;
            _targetLockAction.performed += OnTargetLockPerformed;
            _previousAction.performed += OnPreviousPerformed;
            _nextAction.performed += OnNextPerformed;
            _jumpAction.performed += OnJumpPerformed;
            _interactAction.performed += OnInteractPerformed;
            _playerSkillAction.performed += OnPlayerSkillPerformed;
            _allySlotASkillAction.performed += OnAllySlotASkillPerformed;
            _allySlotBSkillAction.performed += OnAllySlotBSkillPerformed;
            _previousWeaponFormAction.performed += OnPreviousWeaponFormPerformed;
            _nextWeaponFormAction.performed += OnNextWeaponFormPerformed;
            _partyUltimateAction.performed += OnPartyUltimatePerformed;
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

        private static void ApplyKeyboardBindingOverride(InputAction action, Key key)
        {
            if (action == null || key == Key.None)
            {
                return;
            }

            action.ApplyBindingOverride(GetKeyboardPath(key));
        }

        private static string GetKeyboardPath(Key key)
        {
            return key switch
            {
                Key.Digit0 => "<Keyboard>/0",
                Key.Digit1 => "<Keyboard>/1",
                Key.Digit2 => "<Keyboard>/2",
                Key.Digit3 => "<Keyboard>/3",
                Key.Digit4 => "<Keyboard>/4",
                Key.Digit5 => "<Keyboard>/5",
                Key.Digit6 => "<Keyboard>/6",
                Key.Digit7 => "<Keyboard>/7",
                Key.Digit8 => "<Keyboard>/8",
                Key.Digit9 => "<Keyboard>/9",
                _ => $"<Keyboard>/{key.ToString().ToLowerInvariant()}"
            };
        }
    }
}
