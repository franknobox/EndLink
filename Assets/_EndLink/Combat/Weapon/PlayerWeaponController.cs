using System;
using System.Collections.Generic;
using EndLink.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace EndLink.Combat
{
    /// <summary>主角固定使用的三种武器形态。</summary>
    public enum PlayerWeaponForm
    {
        /// <summary>A 形态，当前设计定位为标准形态。</summary>
        A = 0,

        /// <summary>B 形态，当前设计定位为射击形态。</summary>
        B = 1,

        /// <summary>C 形态，当前设计定位为重刃形态。</summary>
        C = 2
    }

    /// <summary>
    /// 单个武器形态的动作组。
    /// 形态结构固定在代码中，具体伤害、时序和 Hitbox 继续由 CombatActionDefinition 资产配置。
    /// </summary>
    [Serializable]
    public sealed class PlayerWeaponActionSet
    {
        [Tooltip("该形态按顺序执行的普攻连段。第一项也是按下普攻时使用的起手动作。")]
        [SerializeField]
        private List<CombatActionDefinition> comboActions = new() { null, null, null };

        [Tooltip("该形态的主动技能动作。")]
        [SerializeField]
        private CombatActionDefinition skillAction;

        /// <summary>该形态的普攻连段，只允许外部读取。</summary>
        public IReadOnlyList<CombatActionDefinition> ComboActions => comboActions;

        /// <summary>连段动作数量。</summary>
        public int ComboCount => comboActions?.Count ?? 0;

        /// <summary>普攻起手动作。</summary>
        public CombatActionDefinition BasicAttackAction => GetComboAction(0);

        /// <summary>主动技能动作。</summary>
        public CombatActionDefinition SkillAction => skillAction;

        /// <summary>读取指定段数的普攻动作；越界或未配置时返回空。</summary>
        public CombatActionDefinition GetComboAction(int index)
        {
            return comboActions != null && index >= 0 && index < comboActions.Count
                ? comboActions[index]
                : null;
        }
    }

    /// <summary>
    /// 主角三种武器形态的唯一运行时入口。
    /// 负责保存当前形态、提供对应动作组，并消费输入层提供的上一/下一形态请求；不直接执行攻击。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCombatDriver))]
    [RequireComponent(typeof(PlayerComboController))]
    public sealed class PlayerWeaponController : MonoBehaviour
    {
        [Header("初始形态")]
        [Tooltip("进入场景时默认使用的武器形态。")]
        [SerializeField]
        private PlayerWeaponForm initialForm = PlayerWeaponForm.A;

        [Header("A 形态")]
        [Tooltip("A 形态（当前定位：标准）的独立普攻连段和主动技能。")]
        [FormerlySerializedAs("standard")]
        [SerializeField]
        private PlayerWeaponActionSet formA = new();

        [Header("B 形态")]
        [Tooltip("B 形态（当前定位：射击）的独立普攻连段和主动技能。")]
        [FormerlySerializedAs("shooting")]
        [SerializeField]
        private PlayerWeaponActionSet formB = new();

        [Header("C 形态")]
        [Tooltip("C 形态（当前定位：重刃）的独立普攻连段和主动技能。")]
        [FormerlySerializedAs("heavyBlade")]
        [SerializeField]
        private PlayerWeaponActionSet formC = new();

        private PlayerCombatDriver _combatDriver;
        private PlayerComboController _comboController;
        private PlayerStateMachine _stateMachine;
        private PlayerInputReader _inputReader;

        /// <summary>武器形态实际发生变化后触发，参数为新的形态。</summary>
        public event Action<PlayerWeaponForm> FormChanged;

        /// <summary>当前武器形态。</summary>
        public PlayerWeaponForm CurrentForm { get; private set; }

        /// <summary>当前形态的动作组。</summary>
        public PlayerWeaponActionSet CurrentActionSet => GetActionSet(CurrentForm);

        /// <summary>当前形态的普攻连段。</summary>
        public IReadOnlyList<CombatActionDefinition> CurrentComboActions => CurrentActionSet.ComboActions;

        /// <summary>当前形态的普攻起手动作。</summary>
        public CombatActionDefinition CurrentBasicAttackAction => CurrentActionSet.BasicAttackAction;

        /// <summary>当前形态的主动技能。</summary>
        public CombatActionDefinition CurrentSkillAction => CurrentActionSet.SkillAction;

        /// <summary>
        /// 当前是否允许切换形态。
        /// 第一版只允许 Idle / Move 或状态机尚未初始化时切换，避免跨形态残留连段和 Hitbox。
        /// </summary>
        public bool CanSwitchForm
        {
            get
            {
                CacheComponents();
                if (_combatDriver != null && _combatDriver.IsExecutingAction)
                {
                    return false;
                }

                if (_stateMachine == null)
                {
                    return true;
                }

                PlayerStateId stateId = _stateMachine.CurrentStateId;
                return stateId == PlayerStateId.None
                    || stateId == PlayerStateId.Idle
                    || stateId == PlayerStateId.Move;
            }
        }

        private void Awake()
        {
            EnsureActionSets();
            CacheComponents();
            CurrentForm = initialForm;
        }

        private void OnValidate()
        {
            EnsureActionSets();
        }

        private void Update()
        {
            if (_inputReader == null)
            {
                return;
            }

            bool previousRequested = _inputReader.ConsumePreviousWeaponFormPressed();
            bool nextRequested = _inputReader.ConsumeNextWeaponFormPressed();
            if (previousRequested == nextRequested)
            {
                return;
            }

            if (previousRequested)
            {
                RequestPreviousForm();
            }
            else
            {
                RequestNextForm();
            }
        }

        private void EnsureActionSets()
        {
            formA ??= new PlayerWeaponActionSet();
            formB ??= new PlayerWeaponActionSet();
            formC ??= new PlayerWeaponActionSet();
        }

        /// <summary>
        /// 请求切换到指定形态。
        /// 当前形态相同时视为请求成功但不重复广播事件。
        /// </summary>
        public bool RequestForm(PlayerWeaponForm form)
        {
            int formValue = (int)form;
            if (formValue < (int)PlayerWeaponForm.A
                || formValue > (int)PlayerWeaponForm.C)
            {
                return false;
            }

            if (CurrentForm == form)
            {
                return true;
            }

            if (!CanSwitchForm)
            {
                return false;
            }

            CurrentForm = form;
            _comboController?.ResetCombo();
            FormChanged?.Invoke(CurrentForm);
            return true;
        }

        /// <summary>按 A、B、C 的顺序请求切换到下一形态。</summary>
        public bool RequestNextForm()
        {
            PlayerWeaponForm next = CurrentForm switch
            {
                PlayerWeaponForm.A => PlayerWeaponForm.B,
                PlayerWeaponForm.B => PlayerWeaponForm.C,
                _ => PlayerWeaponForm.A
            };
            return RequestForm(next);
        }

        /// <summary>按 A、C、B 的逆序请求切换到上一形态。</summary>
        public bool RequestPreviousForm()
        {
            PlayerWeaponForm previous = CurrentForm switch
            {
                PlayerWeaponForm.A => PlayerWeaponForm.C,
                PlayerWeaponForm.B => PlayerWeaponForm.A,
                _ => PlayerWeaponForm.B
            };
            return RequestForm(previous);
        }

        /// <summary>读取任意固定形态的动作组，供 UI 和调试工具预览。</summary>
        public PlayerWeaponActionSet GetActionSet(PlayerWeaponForm form)
        {
            EnsureActionSets();
            return form switch
            {
                PlayerWeaponForm.B => formB,
                PlayerWeaponForm.C => formC,
                _ => formA
            };
        }

        private void CacheComponents()
        {
            _combatDriver ??= GetComponent<PlayerCombatDriver>();
            _comboController ??= GetComponent<PlayerComboController>();
            _stateMachine ??= GetComponent<PlayerStateMachine>();
            _inputReader ??= GetComponent<PlayerInputReader>();
        }
    }
}
