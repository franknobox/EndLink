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
        private InputAction _playerSkillAction;
        private InputAction _allySlotASkillAction;
        private InputAction _allySlotBSkillAction;
        private InputAction _playerLinkAttackAction;
        private InputAction _allySlotALinkAttackAction;
        private InputAction _allySlotBLinkAttackAction;
        private InputAction _partyUltimateAction;
        private bool _attackPressed;
        private bool _playerSkillPressed;
        private bool _allySlotASkillPressed;
        private bool _allySlotBSkillPressed;
        private bool _playerLinkAttackPressed;
        private bool _allySlotALinkAttackPressed;
        private bool _allySlotBLinkAttackPressed;
        private bool _partyUltimatePressed;

        private void Awake()
        {
            _inputActions = new InputSystem_Actions();
            _playerActions = _inputActions.Player;
            _playerSkillAction = _inputActions.asset.FindAction("Player/PlayerSkill", true);
            _allySlotASkillAction = _inputActions.asset.FindAction("Player/AllySlotASkill", true);
            _allySlotBSkillAction = _inputActions.asset.FindAction("Player/AllySlotBSkill", true);
            _playerLinkAttackAction = _inputActions.asset.FindAction("Player/PlayerLinkAttack", true);
            _allySlotALinkAttackAction = _inputActions.asset.FindAction("Player/AllySlotALinkAttack", true);
            _allySlotBLinkAttackAction = _inputActions.asset.FindAction("Player/AllySlotBLinkAttack", true);
            _partyUltimateAction = _inputActions.asset.FindAction("Player/PartyUltimate", true);

            _playerActions.Move.performed += OnMoveChanged;
            _playerActions.Move.canceled += OnMoveCanceled;
            _playerActions.Attack.performed += OnAttackPerformed;
            _playerSkillAction.performed += OnPlayerSkillPerformed;
            _allySlotASkillAction.performed += OnAllySlotASkillPerformed;
            _allySlotBSkillAction.performed += OnAllySlotBSkillPerformed;
            _playerLinkAttackAction.performed += OnPlayerLinkAttackPerformed;
            _allySlotALinkAttackAction.performed += OnAllySlotALinkAttackPerformed;
            _allySlotBLinkAttackAction.performed += OnAllySlotBLinkAttackPerformed;
            _partyUltimateAction.performed += OnPartyUltimatePerformed;
        }

        private void OnEnable()
        {
            _playerActions.Enable();
        }

        private void OnDisable()
        {
            MoveInput = Vector2.zero;
            ResetPressedInputs();
            _playerActions.Disable();
        }

        private void OnDestroy()
        {
            _playerActions.Move.performed -= OnMoveChanged;
            _playerActions.Move.canceled -= OnMoveCanceled;
            _playerActions.Attack.performed -= OnAttackPerformed;
            _playerSkillAction.performed -= OnPlayerSkillPerformed;
            _allySlotASkillAction.performed -= OnAllySlotASkillPerformed;
            _allySlotBSkillAction.performed -= OnAllySlotBSkillPerformed;
            _playerLinkAttackAction.performed -= OnPlayerLinkAttackPerformed;
            _allySlotALinkAttackAction.performed -= OnAllySlotALinkAttackPerformed;
            _allySlotBLinkAttackAction.performed -= OnAllySlotBLinkAttackPerformed;
            _partyUltimateAction.performed -= OnPartyUltimatePerformed;
            _inputActions.Dispose();
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
        /// 消费一次主控主动技能输入，默认键位 Q。
        /// </summary>
        public bool ConsumePlayerSkillPressed()
        {
            return ConsumePressed(ref _playerSkillPressed);
        }

        /// <summary>
        /// 消费一次队友 A 主动技能输入，默认键位 E。
        /// </summary>
        public bool ConsumeAllySlotASkillPressed()
        {
            return ConsumePressed(ref _allySlotASkillPressed);
        }

        /// <summary>
        /// 消费一次队友 B 主动技能输入，默认键位 F。
        /// </summary>
        public bool ConsumeAllySlotBSkillPressed()
        {
            return ConsumePressed(ref _allySlotBSkillPressed);
        }

        /// <summary>
        /// 消费一次主控连携请求输入，默认键位 1。
        /// 这里只读取玩家意图，是否能释放必须由连携机制判断。
        /// </summary>
        public bool ConsumePlayerLinkAttackPressed()
        {
            return ConsumePressed(ref _playerLinkAttackPressed);
        }

        /// <summary>
        /// 消费一次队友 A 连携请求输入，默认键位 2。
        /// 这里只读取玩家意图，是否能释放必须由连携机制判断。
        /// </summary>
        public bool ConsumeAllySlotALinkAttackPressed()
        {
            return ConsumePressed(ref _allySlotALinkAttackPressed);
        }

        /// <summary>
        /// 消费一次队友 B 连携请求输入，默认键位 3。
        /// 这里只读取玩家意图，是否能释放必须由连携机制判断。
        /// </summary>
        public bool ConsumeAllySlotBLinkAttackPressed()
        {
            return ConsumePressed(ref _allySlotBLinkAttackPressed);
        }

        /// <summary>
        /// 消费一次全队极限技输入，默认键位 V。
        /// </summary>
        public bool ConsumePartyUltimatePressed()
        {
            return ConsumePressed(ref _partyUltimatePressed);
        }

        /// <summary>
        /// 覆盖主控主动技能、队友主动技能和连携请求的键盘绑定。
        /// 该方法只改运行时 InputAction 实例，不写回 inputactions 资产。
        /// </summary>
        public void ApplyPartyCombatKeyboardBindings(
            Key playerSkillKey,
            Key allySlotASkillKey,
            Key allySlotBSkillKey,
            Key playerLinkAttackKey,
            Key allySlotALinkAttackKey,
            Key allySlotBLinkAttackKey)
        {
            ApplyKeyboardBindingOverride(_playerSkillAction, playerSkillKey);
            ApplyKeyboardBindingOverride(_allySlotASkillAction, allySlotASkillKey);
            ApplyKeyboardBindingOverride(_allySlotBSkillAction, allySlotBSkillKey);
            ApplyKeyboardBindingOverride(_playerLinkAttackAction, playerLinkAttackKey);
            ApplyKeyboardBindingOverride(_allySlotALinkAttackAction, allySlotALinkAttackKey);
            ApplyKeyboardBindingOverride(_allySlotBLinkAttackAction, allySlotBLinkAttackKey);
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

        private void OnPlayerLinkAttackPerformed(InputAction.CallbackContext context)
        {
            SetPressedIfButton(context, ref _playerLinkAttackPressed);
        }

        private void OnAllySlotALinkAttackPerformed(InputAction.CallbackContext context)
        {
            SetPressedIfButton(context, ref _allySlotALinkAttackPressed);
        }

        private void OnAllySlotBLinkAttackPerformed(InputAction.CallbackContext context)
        {
            SetPressedIfButton(context, ref _allySlotBLinkAttackPressed);
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
            _attackPressed = false;
            _playerSkillPressed = false;
            _allySlotASkillPressed = false;
            _allySlotBSkillPressed = false;
            _playerLinkAttackPressed = false;
            _allySlotALinkAttackPressed = false;
            _allySlotBLinkAttackPressed = false;
            _partyUltimatePressed = false;
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
