using System.Collections.Generic;
using EndLink.Combat;
using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 玩家有限状态机。
    /// 负责持有状态实例、切换当前状态，并把每帧执行权交给当前状态。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerCombatDriver))]
    [RequireComponent(typeof(CombatTarget))]
    public sealed class PlayerStateMachine : MonoBehaviour, ICharacterStatsTypeProvider
    {
        [Header("初始状态")]
        [SerializeField]
        private PlayerStateId initialState = PlayerStateId.Idle;

        [Header("攻击状态")]
        [Tooltip("攻击状态的基础持续时间。胶囊白模阶段先用时间控制，接动画后可改为动画事件驱动。")]
        [SerializeField, Min(0.01f)]
        private float attackDuration = 0.45f;

        [Tooltip("攻击期间移动输入倍率。0 表示站桩攻击，0.3 表示允许轻微滑步，1 表示完全保留移动。")]
        [SerializeField, Range(0f, 1f)]
        private float attackMoveInputScale = 0f;

        [Tooltip("攻击动作期间朝当前软锁目标平滑转向的速度。0 表示攻击开始后不继续跟随目标。")]
        [SerializeField, Min(0f)]
        private float attackTrackingRotationSharpness = 8f;

        [Header("输入缓冲")]
        [Tooltip("普攻输入缓冲时间。攻击、技能或短暂冷却结束前按下攻击，可在该时间内自动衔接下一次普攻。")]
        [SerializeField, Min(0f)]
        private float attackInputBufferDuration = 0.15f;

        [Header("防御状态")]
        [Tooltip("防御期间保留的移动输入倍率。第一版默认站定防御，后续可改为带朝向锁定的防御移动。")]
        [SerializeField, Range(0f, 1f)]
        private float guardMoveInputScale;

        [Header("技能状态")]
        [Tooltip("通用技能状态的基础持续时间。胶囊白模阶段先用时间控制，接动画和技能配置后可改为数据或动画事件驱动。")]
        [SerializeField, Min(0.01f)]
        private float skillDuration = 0.65f;

        [Tooltip("技能期间移动输入倍率。0 表示站桩施法，0.3 表示允许轻微滑步，1 表示完全保留移动。")]
        [SerializeField, Range(0f, 1f)]
        private float skillMoveInputScale = 0f;

        [Header("闪避状态")]
        [Tooltip("闪避状态持续时间。胶囊白模阶段先用固定时间控制，接动画后可改为动画事件或曲线驱动。")]
        [SerializeField, Min(0.01f)]
        private float dodgeDuration = 0.25f;

        [Tooltip("一次闪避期望移动距离，单位米。")]
        [SerializeField, Min(0f)]
        private float dodgeDistance = 3f;

        [Tooltip("闪避冷却时间，防止连续狂闪。")]
        [SerializeField, Min(0f)]
        private float dodgeCooldown = 0.45f;

        [Tooltip("闪避开始后获得临时免伤的时间。设置为 0 表示不提供免伤窗口。")]
        [SerializeField, Min(0f)]
        private float dodgeInvincibleDuration = 0.18f;

        [Header("受击状态")]
        [Tooltip("受击硬直的基础持续时间。白模阶段先用时间控制，后续可由攻击数据、受击动画或韧性系统决定。")]
        [SerializeField, Min(0.01f)]
        private float hitDuration = 0.3f;

        [Tooltip("受击期间移动输入倍率。0 表示完全失控，0.3 表示允许轻微滑动，1 表示完全保留移动。")]
        [SerializeField, Range(0f, 1f)]
        private float hitMoveInputScale = 0f;

        [Header("调试")]
        [Tooltip("是否打印玩家状态切换日志。默认关闭，避免 Console 被每帧流程噪声淹没。")]
        [SerializeField]
        private bool logStateChanges;

        private readonly Dictionary<PlayerStateId, IPlayerState> _states = new();
        private IPlayerState _currentState;
        private PlayerInputReader _inputReader;
        private PlayerCombatDriver _combatDriver;
        private PlayerComboController _comboController;
        private PlayerAttackMotion _attackMotion;
        private PlayerGuardController _guardController;
        private CombatActionDefinition _currentAction;
        private Transform _currentActionTarget;
        private bool _actionRequested;
        private float _nextDodgeAllowedTime;
        private float _attackBufferExpiresAt = float.NegativeInfinity;

        /// <summary>
        /// 当前状态标识，便于调试面板或 Inspector 观察。
        /// </summary>
        public PlayerStateId CurrentStateId => _currentState?.StateId ?? PlayerStateId.None;

        /// <summary>供 CharacterStats 自动识别为玩家配置。</summary>
        public CharacterStatsType StatsType => CharacterStatsType.Player;

        /// <summary>当前通用技能状态准备执行的动作。</summary>
        public CombatActionDefinition CurrentAction => _currentAction;

        /// <summary>当前动作显式指定的目标。为空时由 PlayerTargeting 继续解析软锁目标。</summary>
        public Transform CurrentActionTarget => _currentActionTarget;

        /// <summary>
        /// 攻击状态持续时间。
        /// </summary>
        public float AttackDuration => attackDuration;

        /// <summary>
        /// 攻击状态移动输入倍率。
        /// </summary>
        public float AttackMoveInputScale => attackMoveInputScale;

        /// <summary>攻击动作期间向软锁目标平滑转向的速度。</summary>
        public float AttackTrackingRotationSharpness => attackTrackingRotationSharpness;

        /// <summary>防御期间保留的移动输入倍率。</summary>
        public float GuardMoveInputScale => guardMoveInputScale;

        /// <summary>
        /// 技能状态持续时间。
        /// </summary>
        public float SkillDuration => _currentAction != null
            ? Mathf.Max(skillDuration, _currentAction.TotalDuration)
            : skillDuration;

        /// <summary>
        /// 技能状态移动输入倍率。
        /// </summary>
        public float SkillMoveInputScale => skillMoveInputScale;

        /// <summary>
        /// 闪避状态持续时间。
        /// </summary>
        public float DodgeDuration => dodgeDuration;

        /// <summary>
        /// 一次闪避期望移动距离。
        /// </summary>
        public float DodgeDistance => dodgeDistance;

        /// <summary>
        /// 闪避冷却时间。
        /// </summary>
        public float DodgeCooldown => dodgeCooldown;

        /// <summary>
        /// 闪避开始后的临时免伤窗口。
        /// </summary>
        public float DodgeInvincibleDuration => dodgeInvincibleDuration;

        /// <summary>
        /// 当前是否允许开始闪避。
        /// </summary>
        public bool CanStartDodge => Time.time >= _nextDodgeAllowedTime;

        /// <summary>
        /// 受击状态持续时间。
        /// </summary>
        public float HitDuration => hitDuration;

        /// <summary>
        /// 受击状态移动输入倍率。
        /// </summary>
        public float HitMoveInputScale => hitMoveInputScale;

        private void Awake()
        {
            _inputReader = GetComponent<PlayerInputReader>();
            PlayerController controller = GetComponent<PlayerController>();
            _combatDriver = GetComponent<PlayerCombatDriver>();
            TryGetComponent(out PlayerTargeting targeting);
            TryGetComponent(out _comboController);
            TryGetComponent(out _attackMotion);
            TryGetComponent(out _guardController);

            PlayerStateContext context = new PlayerStateContext(
                this,
                transform,
                _inputReader,
                controller,
                _combatDriver,
                targeting,
                _comboController,
                _attackMotion,
                _guardController);

            RegisterState(new PlayerIdleState(context));
            RegisterState(new PlayerMoveState(context));
            RegisterState(new PlayerAttackState(context));
            RegisterState(new PlayerSkillState(context));
            RegisterState(new PlayerDodgeState(context));
            RegisterState(new PlayerGuardState(context));
            RegisterState(new PlayerHitState(context));
            RegisterState(new PlayerDeadState(context));
        }

        private void Start()
        {
            ChangeState(initialState);
        }

        private void Update()
        {
            CaptureAttackInput();
            _currentState?.Tick(Time.deltaTime);
        }

        private void OnDisable()
        {
            ClearAttackBuffer();
        }

        private void OnValidate()
        {
            attackDuration = Mathf.Max(0.01f, attackDuration);
            attackTrackingRotationSharpness = Mathf.Max(0f, attackTrackingRotationSharpness);
            attackInputBufferDuration = Mathf.Max(0f, attackInputBufferDuration);
            skillDuration = Mathf.Max(0.01f, skillDuration);
            dodgeDuration = Mathf.Max(0.01f, dodgeDuration);
            dodgeDistance = Mathf.Max(0f, dodgeDistance);
            dodgeCooldown = Mathf.Max(0f, dodgeCooldown);
            dodgeInvincibleDuration = Mathf.Max(0f, dodgeInvincibleDuration);
            hitDuration = Mathf.Max(0.01f, hitDuration);
        }

        /// <summary>
        /// 切换到指定状态。
        /// 如果目标状态就是当前状态，则不重复调用 Exit/Enter。
        /// </summary>
        public void ChangeState(PlayerStateId nextStateId)
        {
            if (nextStateId == PlayerStateId.None || CurrentStateId == nextStateId)
            {
                return;
            }

            if (!_states.TryGetValue(nextStateId, out IPlayerState nextState))
            {
                Debug.LogError($"未注册玩家状态：{nextStateId}", this);
                return;
            }

            PlayerStateId previousStateId = CurrentStateId;

            _currentState?.Exit();
            _currentState = nextState;
            _currentState.Enter();

            if (logStateChanges)
            {
                Debug.Log($"PlayerState: {previousStateId} -> {nextStateId}", this);
            }
        }

        /// <summary>
        /// 请求进入通用技能状态。
        /// 当前由 PartyCombatRouter 调用，也可以供 UI、调试工具或技能栏系统复用。
        /// </summary>
        public bool RequestSkill()
        {
            return RequestAction(_combatDriver != null ? _combatDriver.SkillAction : null, null);
        }

        /// <summary>
        /// 请求玩家通过通用技能状态执行指定动作。
        /// 主动技能和连携技共用该入口，由状态机统一处理硬直、状态窗口和动作执行。
        /// </summary>
        public bool RequestAction(CombatActionDefinition action, Transform target)
        {
            if (CurrentStateId != PlayerStateId.Idle && CurrentStateId != PlayerStateId.Move)
            {
                return false;
            }

            ICombatActionExecutor actionExecutor = _combatDriver;
            if (_actionRequested || actionExecutor == null || !actionExecutor.CanExecute(action))
            {
                return false;
            }

            _currentAction = action;
            _currentActionTarget = target;
            _actionRequested = true;
            return true;
        }

        /// <summary>
        /// 请求进入受击状态。
        /// 受击可以打断移动、攻击和技能，但不能覆盖死亡状态。
        /// </summary>
        public void RequestHit()
        {
            if (CurrentStateId == PlayerStateId.Dead)
            {
                return;
            }

            ClearCurrentAction();
            ChangeState(PlayerStateId.Hit);
        }

        /// <summary>
        /// 记录一次闪避开始，用于刷新冷却。
        /// </summary>
        public void MarkDodgeStarted()
        {
            _nextDodgeAllowedTime = Time.time + dodgeCooldown;
        }

        /// <summary>
        /// 请求进入死亡状态。
        /// 死亡是当前玩家状态机的最高优先级终止状态，会清理尚未消费的技能请求。
        /// </summary>
        public void RequestDead()
        {
            ClearAttackBuffer();
            ClearCurrentAction();
            ChangeState(PlayerStateId.Dead);
        }

        /// <summary>
        /// 尝试消费仍在有效期内的普攻输入。
        /// 只有动作真正可执行时才会清空缓冲，短暂冷却不会提前吃掉输入。
        /// </summary>
        internal bool TryConsumeAttackBuffer(bool canExecute)
        {
            if (Time.time > _attackBufferExpiresAt)
            {
                ClearAttackBuffer();
                return false;
            }

            if (!canExecute)
            {
                return false;
            }

            ClearAttackBuffer();
            return true;
        }

        /// <summary>
        /// 消费一次技能请求。
        /// 只允许状态上下文调用，避免多个状态重复响应同一次技能请求。
        /// </summary>
        internal bool ConsumeSkillRequest()
        {
            if (!_actionRequested)
            {
                return false;
            }

            _actionRequested = false;
            return true;
        }

        /// <summary>结束当前通用动作并回到移动或待机。</summary>
        public void CompleteAction()
        {
            ClearCurrentAction();
            ChangeState(GetDefaultLocomotionState());
        }

        private void ClearCurrentAction()
        {
            _actionRequested = false;
            _currentAction = null;
            _currentActionTarget = null;
        }

        private PlayerStateId GetDefaultLocomotionState()
        {
            return _inputReader != null
                && _inputReader.MoveInput.sqrMagnitude > PlayerStateBase.MoveInputDeadZoneSqr
                    ? PlayerStateId.Move
                    : PlayerStateId.Idle;
        }

        private void CaptureAttackInput()
        {
            if (_inputReader != null && _inputReader.ConsumeAttackPressed())
            {
                _attackBufferExpiresAt = Time.time + attackInputBufferDuration;
            }
        }

        private void ClearAttackBuffer()
        {
            _attackBufferExpiresAt = float.NegativeInfinity;
        }

        private void RegisterState(IPlayerState state)
        {
            _states[state.StateId] = state;
        }
    }
}
