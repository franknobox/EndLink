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

        /// <summary>该形态的普攻连段，只允许外部读取。</summary>
        public IReadOnlyList<CombatActionDefinition> ComboActions => comboActions;

        /// <summary>连段动作数量。</summary>
        public int ComboCount => comboActions?.Count ?? 0;

        /// <summary>普攻起手动作。</summary>
        public CombatActionDefinition BasicAttackAction => GetComboAction(0);

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

        [Header("形态解锁")]
        [Tooltip("进入场景时 B 形态（射击）是否已经解锁。当前默认开启以保持 Demo 现有流程；后续新游戏可关闭，并由进度系统调用 UnlockForm 解锁。")]
        [SerializeField]
        private bool formBInitiallyUnlocked = true;

        [Tooltip("进入场景时 C 形态（重刃）是否已经解锁。当前默认开启以保持 Demo 现有流程；后续新游戏可关闭，并由进度系统调用 UnlockForm 解锁。")]
        [SerializeField]
        private bool formCInitiallyUnlocked = true;

        [Header("A 形态")]
        [Tooltip("A 形态（当前定位：标准）的独立普攻连段。")]
        [FormerlySerializedAs("standard")]
        [SerializeField]
        private PlayerWeaponActionSet formA = new();

        [Header("B 形态")]
        [Tooltip("B 形态（当前定位：射击）的独立普攻连段。")]
        [FormerlySerializedAs("shooting")]
        [SerializeField]
        private PlayerWeaponActionSet formB = new();

        [Header("C 形态")]
        [Tooltip("C 形态（当前定位：重刃）的独立普攻连段。")]
        [FormerlySerializedAs("heavyBlade")]
        [SerializeField]
        private PlayerWeaponActionSet formC = new();

        private PlayerCombatDriver _combatDriver;
        private PlayerComboController _comboController;
        private PlayerStateMachine _stateMachine;
        private PlayerInputReader _inputReader;
        private bool _formBUnlocked;
        private bool _formCUnlocked;
        private bool _unlockStateInitialized;

        /// <summary>武器形态实际发生变化后触发，参数为新的形态。</summary>
        public event Action<PlayerWeaponForm> FormChanged;

        /// <summary>某个武器形态在运行时首次解锁后触发，供 UI、提示和进度接线使用。</summary>
        public event Action<PlayerWeaponForm> FormUnlocked;

        /// <summary>当前武器形态。</summary>
        public PlayerWeaponForm CurrentForm { get; private set; }

        /// <summary>当前形态的动作组。</summary>
        public PlayerWeaponActionSet CurrentActionSet => GetActionSet(CurrentForm);

        /// <summary>当前形态的普攻连段。</summary>
        public IReadOnlyList<CombatActionDefinition> CurrentComboActions => CurrentActionSet.ComboActions;

        /// <summary>当前形态的普攻起手动作。</summary>
        public CombatActionDefinition CurrentBasicAttackAction => CurrentActionSet.BasicAttackAction;

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
            InitializeUnlockState();
            CurrentForm = IsFormUnlocked(initialForm) ? initialForm : PlayerWeaponForm.A;
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
            if (!IsValidForm(form) || !IsFormUnlocked(form))
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
            return RequestAdjacentForm(1);
        }

        /// <summary>按 A、C、B 的逆序请求切换到上一形态。</summary>
        public bool RequestPreviousForm()
        {
            return RequestAdjacentForm(-1);
        }

        /// <summary>
        /// 查询指定形态当前是否已解锁。A 形态是基础形态，始终返回 true。
        /// </summary>
        public bool IsFormUnlocked(PlayerWeaponForm form)
        {
            if (!IsValidForm(form))
            {
                return false;
            }

            InitializeUnlockState();
            return form switch
            {
                PlayerWeaponForm.B => _formBUnlocked,
                PlayerWeaponForm.C => _formCUnlocked,
                _ => true
            };
        }

        /// <summary>
        /// 运行时解锁指定形态，供后续关卡奖励、进度数据或调试工具调用。
        /// 已解锁形态重复调用会直接返回成功，但不会重复广播事件。
        /// </summary>
        public bool UnlockForm(PlayerWeaponForm form)
        {
            if (!IsValidForm(form))
            {
                return false;
            }

            InitializeUnlockState();
            if (IsFormUnlocked(form))
            {
                return true;
            }

            switch (form)
            {
                case PlayerWeaponForm.B:
                    _formBUnlocked = true;
                    break;
                case PlayerWeaponForm.C:
                    _formCUnlocked = true;
                    break;
            }

            FormUnlocked?.Invoke(form);
            return true;
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

        private void InitializeUnlockState()
        {
            if (_unlockStateInitialized)
            {
                return;
            }

            _formBUnlocked = formBInitiallyUnlocked;
            _formCUnlocked = formCInitiallyUnlocked;
            _unlockStateInitialized = true;
        }

        private bool RequestAdjacentForm(int direction)
        {
            const int formCount = 3;
            int currentIndex = (int)CurrentForm;

            for (int step = 1; step <= formCount; step++)
            {
                int candidateIndex = (currentIndex + direction * step + formCount) % formCount;
                PlayerWeaponForm candidate = (PlayerWeaponForm)candidateIndex;
                if (IsFormUnlocked(candidate))
                {
                    return RequestForm(candidate);
                }
            }

            return false;
        }

        private static bool IsValidForm(PlayerWeaponForm form)
        {
            int formValue = (int)form;
            return formValue >= (int)PlayerWeaponForm.A
                && formValue <= (int)PlayerWeaponForm.C;
        }
    }
}
