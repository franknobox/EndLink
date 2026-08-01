using System.Collections.Generic;
using EndLink.Combat;
using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 玩家 Animator 桥接层。
    /// 只负责把玩家状态机、移动速度和武器形态同步到 Animator 参数，不读取输入，不决定状态切换。
    /// 胶囊白模阶段可以先挂着观察参数，后续接真实 Animator Controller 时不需要改状态机。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStateMachine))]
    public sealed class PlayerAnimatorDriver : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("玩家 Animator。为空时会从当前物体和子物体中自动查找。")]
        [SerializeField]
        private Animator animator;

        [Header("基础参数")]
        [Tooltip("水平移动速度 Float 参数名。为空则不写入。")]
        [SerializeField]
        private string moveSpeedParameter = "MoveSpeed";

        [Tooltip("是否正在移动 Bool 参数名。为空则不写入。")]
        [SerializeField]
        private string isMovingParameter = "IsMoving";

        [Tooltip("当前状态 ID Int 参数名。为空则不写入。")]
        [SerializeField]
        private string stateIdParameter = "StateId";

        [Tooltip("是否死亡 Bool 参数名。为空则不写入。")]
        [SerializeField]
        private string isDeadParameter = "IsDead";

        [Header("动作参数")]
        [Tooltip("动作标识 Int 参数名。写入 ActionId 字符串的 Animator Hash；为空则不写入。")]
        [SerializeField]
        private string actionIdParameter = CombatAnimatorParams.ActionId;

        [Tooltip("动作类型 Int 参数名。数值对应 CombatActionType；为空则不写入。")]
        [SerializeField]
        private string actionTypeParameter = CombatAnimatorParams.ActionType;

        [Tooltip("任意战斗动作成功开始时触发的通用 Trigger。存在该参数时，Attack/Skill 优先使用这套动作协议。")]
        [SerializeField]
        private string actionTriggerParameter = CombatAnimatorParams.ActionTrigger;

        [Tooltip("当前武器形态 Int 参数名。0=A，1=B，2=C；当前设计定位分别为标准、射击、重刃。为空则不写入。")]
        [SerializeField]
        private string weaponFormParameter = CombatAnimatorParams.WeaponForm;

        [Header("状态触发器")]
        [Tooltip("进入 Attack 状态时触发的 Trigger 参数名。为空则不触发。")]
        [SerializeField]
        private string attackTriggerParameter = "TriggerAttack";

        [Tooltip("进入 Skill 状态时触发的 Trigger 参数名。为空则不触发。")]
        [SerializeField]
        private string skillTriggerParameter = "TriggerSkill";

        [Tooltip("进入 Dodge 状态时触发的 Trigger 参数名。为空则不触发。")]
        [SerializeField]
        private string dodgeTriggerParameter = "TriggerDodge";

        [Tooltip("进入 Hit 状态时触发的 Trigger 参数名。为空则不触发。")]
        [SerializeField]
        private string hitTriggerParameter = "TriggerHit";

        [Tooltip("进入 Dead 状态时触发的 Trigger 参数名。为空则不触发。")]
        [SerializeField]
        private string deadTriggerParameter = "TriggerDead";

        [Header("调试")]
        [Tooltip("Animator 缺少某个参数时是否打印警告。白模阶段建议关闭，接正式 Animator Controller 时可打开排查参数名。")]
        [SerializeField]
        private bool warnMissingParameters;

        private readonly HashSet<int> _animatorParameterHashes = new();
        private readonly HashSet<int> _warnedMissingParameterHashes = new();
        private PlayerStateMachine _stateMachine;
        private PlayerCombatDriver _combatDriver;
        private PlayerWeaponController _weaponController;
        private CharacterController _characterController;
        private PlayerStateId _lastStateId = PlayerStateId.None;

        /// <summary>当前桥接的 Animator。</summary>
        public Animator Animator => animator;

        /// <summary>上一帧记录到的状态 ID。</summary>
        public PlayerStateId LastStateId => _lastStateId;

        private void Awake()
        {
            _stateMachine = GetComponent<PlayerStateMachine>();
            _combatDriver = GetComponent<PlayerCombatDriver>();
            TryGetComponent(out _weaponController);
            _characterController = GetComponent<CharacterController>();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            EnsureAnimationEventReceiver();
            RebuildParameterCache();
        }

        private void OnEnable()
        {
            if (_combatDriver == null)
            {
                _combatDriver = GetComponent<PlayerCombatDriver>();
            }

            if (_combatDriver != null)
            {
                _combatDriver.ActionStarted -= HandleActionStarted;
                _combatDriver.ActionStarted += HandleActionStarted;
            }
        }

        private void OnDisable()
        {
            if (_combatDriver != null)
            {
                _combatDriver.ActionStarted -= HandleActionStarted;
            }
        }

        private void OnValidate()
        {
            if (animator != null)
            {
                RebuildParameterCache();
            }
        }

        private void Update()
        {
            RefreshAnimatorParameters();
        }

        /// <summary>
        /// 设置 Animator 引用。
        /// 主要用于运行时切换模型、调试工具或编译测试。
        /// </summary>
        public void SetAnimator(Animator targetAnimator)
        {
            animator = targetAnimator;
            EnsureAnimationEventReceiver();
            RebuildParameterCache();
        }

        /// <summary>
        /// 立即刷新一次 Animator 参数。
        /// </summary>
        public void RefreshAnimatorParameters()
        {
            if (animator == null || _stateMachine == null)
            {
                return;
            }

            PlayerStateId currentStateId = _stateMachine.CurrentStateId;
            float planarSpeed = GetPlanarSpeed();

            SetFloatIfExists(moveSpeedParameter, planarSpeed);
            SetBoolIfExists(isMovingParameter, planarSpeed > 0.05f);
            SetIntegerIfExists(stateIdParameter, (int)currentStateId);
            SetBoolIfExists(isDeadParameter, currentStateId == PlayerStateId.Dead);
            if (_weaponController != null)
            {
                SetIntegerIfExists(weaponFormParameter, (int)_weaponController.CurrentForm);
            }

            if (currentStateId != _lastStateId)
            {
                TriggerStateEnter(currentStateId);
                _lastStateId = currentStateId;
            }
        }

        private void RebuildParameterCache()
        {
            _animatorParameterHashes.Clear();
            _warnedMissingParameterHashes.Clear();

            if (animator == null)
            {
                return;
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                _animatorParameterHashes.Add(parameter.nameHash);
            }
        }

        private float GetPlanarSpeed()
        {
            if (_characterController == null)
            {
                return 0f;
            }

            Vector3 velocity = _characterController.velocity;
            velocity.y = 0f;
            return velocity.magnitude;
        }

        private void TriggerStateEnter(PlayerStateId stateId)
        {
            switch (stateId)
            {
                case PlayerStateId.Attack:
                    if (!HasParameter(actionTriggerParameter))
                    {
                        SetTriggerIfExists(attackTriggerParameter);
                    }
                    break;
                case PlayerStateId.Skill:
                    if (!HasParameter(actionTriggerParameter))
                    {
                        SetTriggerIfExists(skillTriggerParameter);
                    }
                    break;
                case PlayerStateId.Dodge:
                    SetTriggerIfExists(dodgeTriggerParameter);
                    break;
                case PlayerStateId.Hit:
                    SetTriggerIfExists(hitTriggerParameter);
                    break;
                case PlayerStateId.Dead:
                    SetTriggerIfExists(deadTriggerParameter);
                    break;
            }
        }

        private void HandleActionStarted(CombatActionDefinition actionDefinition)
        {
            if (animator == null || actionDefinition == null)
            {
                return;
            }

            int actionId = string.IsNullOrWhiteSpace(actionDefinition.ActionId)
                ? 0
                : Animator.StringToHash(actionDefinition.ActionId);

            SetIntegerIfExists(actionIdParameter, actionId);
            SetIntegerIfExists(actionTypeParameter, (int)actionDefinition.ActionType);
            SetTriggerIfExists(actionTriggerParameter);
        }

        private bool HasParameter(string parameterName)
        {
            return !string.IsNullOrWhiteSpace(parameterName)
                && _animatorParameterHashes.Contains(Animator.StringToHash(parameterName));
        }

        private void EnsureAnimationEventReceiver()
        {
            if (animator == null)
            {
                return;
            }

            if (!animator.TryGetComponent(out CombatAnimationEventReceiver receiver))
            {
                receiver = animator.gameObject.AddComponent<CombatAnimationEventReceiver>();
            }

            receiver.RefreshListeners();
        }

        private void SetFloatIfExists(string parameterName, float value)
        {
            if (TryGetParameterHash(parameterName, out int hash))
            {
                animator.SetFloat(hash, value);
            }
        }

        private void SetBoolIfExists(string parameterName, bool value)
        {
            if (TryGetParameterHash(parameterName, out int hash))
            {
                animator.SetBool(hash, value);
            }
        }

        private void SetIntegerIfExists(string parameterName, int value)
        {
            if (TryGetParameterHash(parameterName, out int hash))
            {
                animator.SetInteger(hash, value);
            }
        }

        private void SetTriggerIfExists(string parameterName)
        {
            if (TryGetParameterHash(parameterName, out int hash))
            {
                animator.SetTrigger(hash);
            }
        }

        private bool TryGetParameterHash(string parameterName, out int hash)
        {
            hash = 0;

            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            hash = Animator.StringToHash(parameterName);

            if (_animatorParameterHashes.Contains(hash))
            {
                return true;
            }

            WarnMissingParameter(parameterName, hash);
            return false;
        }

        private void WarnMissingParameter(string parameterName, int hash)
        {
            if (!warnMissingParameters || _warnedMissingParameterHashes.Contains(hash))
            {
                return;
            }

            _warnedMissingParameterHashes.Add(hash);
            Debug.LogWarning($"PlayerAnimatorDriver 找不到 Animator 参数：{parameterName}", this);
        }
    }
}
