using System.Collections.Generic;
using EndLink.Combat;
using UnityEngine;

namespace EndLink.Ally
{
    /// <summary>
    /// 队友有限状态机。
    /// 当前负责 Idle、Follow、Assist、Hit、Dead 的大状态切换，不读取玩家输入。
    /// Assist 内部再处理接近、攻击和后续行为树细节。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AllyCombatDriver))]
    [RequireComponent(typeof(AllyFollowMotor))]
    [RequireComponent(typeof(AllyTargetSelector))]
    public sealed class AllyStateMachine : MonoBehaviour
    {
        [Header("初始状态")]
        [Tooltip("队友启用后的初始状态。没有跟随目标时建议使用 Idle，有跟随目标时会自动进入 Follow。")]
        [SerializeField]
        private AllyStateId initialState = AllyStateId.Idle;

        [Header("跟随")]
        [Tooltip("队友跟随目标，通常拖固定主控角色。实际移动由 AllyFollowMotor 执行。")]
        [SerializeField]
        private Transform followTarget;

        [Header("助战")]
        [Tooltip("助战进入攻击阶段的距离容差。实际进入攻击距离 = Assist Action 的 Effective Attack Range + 该容差。用于避免队友被碰撞或避让卡在极限距离边缘。")]
        [SerializeField, Min(0f)]
        private float assistAttackRangeTolerance = 0.2f;

        [Tooltip("助战接近目标时的内缩距离。队友不会故意停在动作极限距离，而是尝试比 Effective Attack Range 更靠近目标。")]
        [SerializeField, Min(0f)]
        private float assistApproachInnerOffset = 0.1f;

        [Tooltip("持续助战时，目标离队友超过该距离会回到 Assist 内部接近阶段。建议明显大于动作 Effective Attack Range。")]
        [SerializeField, Min(0.01f)]
        private float assistReengageRange = 2.4f;

        [Tooltip("队友与主控距离超过该值时放弃助战并回到跟随。设置为 0 表示不因距离主控过远而取消。")]
        [SerializeField, Min(0f)]
        private float assistBreakOffDistance = 12f;

        [Header("助战攻击")]
        [Tooltip("助战攻击状态的最短持续时间。最终持续时间会取该值和当前 Assist Action 总时长中的较大值。")]
        [SerializeField, Min(0.01f)]
        private float assistDuration = 0.45f;

        [Header("通用动作")]
        [Tooltip("队友通用动作状态的最短持续时间。最终持续时间会取该值和当前 Action 总时长中的较大值。")]
        [SerializeField, Min(0.01f)]
        private float actionMinDuration = 0.45f;

        [Header("受击状态")]
        [Tooltip("队友受击硬直的基础持续时间。")]
        [SerializeField, Min(0.01f)]
        private float hitDuration = 0.3f;

        [Header("调试")]
        [Tooltip("是否打印队友状态切换日志。排查助战中断、回到跟随等问题时开启。")]
        [SerializeField]
        private bool logStateChanges;

        private readonly Dictionary<AllyStateId, IAllyState> _states = new();
        private IAllyState _currentState;
        private AllyCombatDriver _combatDriver;
        private AllyFollowMotor _followMotor;
        private AllyTargetSelector _targetSelector;
        private Transform _currentAssistTarget;
        private CombatActionDefinition _currentAction;
        private Transform _currentActionTarget;
        private AllyStateId _returnStateAfterAction = AllyStateId.None;

        /// <summary>当前状态标识，方便 Inspector 和调试工具观察。</summary>
        public AllyStateId CurrentStateId => _currentState?.StateId ?? AllyStateId.None;

        /// <summary>当前绑定的队友战斗执行器。</summary>
        public AllyCombatDriver CombatDriver => _combatDriver;

        /// <summary>当前绑定的队友跟随移动组件。</summary>
        public AllyFollowMotor FollowMotor => _followMotor;

        /// <summary>当前绑定的队友目标选择器。</summary>
        public AllyTargetSelector TargetSelector => _targetSelector;

        /// <summary>当前跟随目标。</summary>
        public Transform FollowTarget => followTarget;

        /// <summary>当前助战目标。</summary>
        public Transform CurrentAssistTarget => _currentAssistTarget;

        /// <summary>当前通用动作状态要执行的动作配置。</summary>
        public CombatActionDefinition CurrentAction => _currentAction;

        /// <summary>当前通用动作状态要面向和判定的目标。</summary>
        public Transform CurrentActionTarget => _currentActionTarget;

        /// <summary>当前助战动作的极限有效攻击距离，来自 Assist Action。</summary>
        public float AssistEffectiveAttackRange
        {
            get
            {
                CombatActionDefinition action = _combatDriver != null ? _combatDriver.AssistAction : null;
                return action != null ? action.EffectiveAttackRange : CombatActionDefinition.DefaultEffectiveAttackRange;
            }
        }

        /// <summary>进入攻击阶段的距离，等于动作极限距离加 AI 容差。</summary>
        public float AssistAttackEnterDistance => AssistEffectiveAttackRange + assistAttackRangeTolerance;

        /// <summary>接近目标时尝试停下的距离，略小于动作极限距离。</summary>
        public float AssistApproachStopDistance => Mathf.Max(0.01f, AssistEffectiveAttackRange - assistApproachInnerOffset);

        /// <summary>持续助战时重新接近目标的距离。</summary>
        public float AssistReengageRange => assistReengageRange;

        /// <summary>主控离队友过远时放弃助战的距离。</summary>
        public float AssistBreakOffDistance => assistBreakOffDistance;

        /// <summary>队友受击状态持续时间。</summary>
        public float HitDuration => hitDuration;

        /// <summary>当前通用动作状态持续时间。</summary>
        public float CurrentActionDuration
        {
            get
            {
                return _currentAction != null ? Mathf.Max(actionMinDuration, _currentAction.TotalDuration) : actionMinDuration;
            }
        }

        /// <summary>当前助战攻击状态持续时间，至少覆盖动作配置中的前摇、有效时间和后摇。</summary>
        public float CurrentAssistDuration
        {
            get
            {
                CombatActionDefinition action = _combatDriver != null ? _combatDriver.AssistAction : null;
                return action != null ? Mathf.Max(assistDuration, action.TotalDuration) : assistDuration;
            }
        }

        private void Awake()
        {
            _combatDriver = GetComponent<AllyCombatDriver>();
            _followMotor = GetComponent<AllyFollowMotor>();
            _targetSelector = GetComponent<AllyTargetSelector>();
            if (_targetSelector == null)
            {
                _targetSelector = gameObject.AddComponent<AllyTargetSelector>();
            }

            AllyStateContext context = new AllyStateContext(
                this,
                transform,
                _combatDriver,
                _followMotor,
                _targetSelector);

            RegisterState(new AllyIdleState(context));
            RegisterState(new AllyFollowState(context));
            RegisterState(new AllyAssistState(context));
            RegisterState(new AllyActionState(context));
            RegisterState(new AllyHitState(context));
            RegisterState(new AllyDeadState(context));
        }

        private void Start()
        {
            _followMotor.SetFollowTarget(followTarget);
            ChangeState(followTarget != null ? AllyStateId.Follow : initialState);
        }

        private void Update()
        {
            _currentState?.Tick(Time.deltaTime);
        }

        private void OnValidate()
        {
            assistAttackRangeTolerance = Mathf.Max(0f, assistAttackRangeTolerance);
            assistApproachInnerOffset = Mathf.Max(0f, assistApproachInnerOffset);
            assistReengageRange = Mathf.Max(0.01f, assistReengageRange);
            assistBreakOffDistance = Mathf.Max(0f, assistBreakOffDistance);
            assistDuration = Mathf.Max(0.01f, assistDuration);
            actionMinDuration = Mathf.Max(0.01f, actionMinDuration);
            hitDuration = Mathf.Max(0.01f, hitDuration);
        }

        /// <summary>
        /// 切换到指定队友状态。
        /// 目标状态不存在、为 None 或等于当前状态时不会重复切换。
        /// </summary>
        public void ChangeState(AllyStateId nextStateId)
        {
            if (nextStateId == AllyStateId.None || CurrentStateId == nextStateId)
            {
                return;
            }

            if (!_states.TryGetValue(nextStateId, out IAllyState nextState))
            {
                Debug.LogError($"未注册队友状态：{nextStateId}", this);
                return;
            }

            AllyStateId previousStateId = CurrentStateId;

            _currentState?.Exit();
            _currentState = nextState;
            _currentState.Enter();

            if (logStateChanges)
            {
                Debug.Log(
                    $"AllyState: {previousStateId} -> {nextStateId} | follow={GetTransformName(followTarget)} | assist={GetTransformName(_currentAssistTarget)}",
                    this);
            }

            AllyDebugLog.Raise(
                gameObject,
                AllyDebugCategory.State,
                $"state {previousStateId} -> {nextStateId}, follow={GetTransformName(followTarget)}, assist={GetTransformName(_currentAssistTarget)}");
        }

        /// <summary>
        /// 设置队友跟随目标。
        /// 状态机保存目标引用并同步给 AllyFollowMotor，然后在 Idle / Follow 之间切换。
        /// </summary>
        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
            _followMotor.SetFollowTarget(target);

            if (CurrentStateId == AllyStateId.Dead
                || CurrentStateId == AllyStateId.Assist
                || CurrentStateId == AllyStateId.Action
                || CurrentStateId == AllyStateId.Hit)
            {
                return;
            }

            ChangeState(followTarget != null ? AllyStateId.Follow : AllyStateId.Idle);
        }

        /// <summary>
        /// 请求进入助战流程。
        /// 请求成功后进入 Assist 大状态，由 Assist 内部处理接近和攻击阶段。
        /// </summary>
        public bool RequestAssist(Transform target)
        {
            if (target == null)
            {
                LogAssistRequestRejected("target is null");
                return false;
            }

            if (CurrentStateId == AllyStateId.Dead
                || CurrentStateId == AllyStateId.Hit
                || CurrentStateId == AllyStateId.Action
                || CurrentStateId == AllyStateId.Assist)
            {
                LogAssistRequestRejected($"state is {CurrentStateId}");
                return false;
            }

            if (_combatDriver == null || !_combatDriver.HasAssistAction)
            {
                LogAssistRequestRejected("missing assist action");
                return false;
            }

            _currentAssistTarget = target;
            AllyDebugLog.Raise(
                gameObject,
                AllyDebugCategory.State,
                $"assist request accepted, target={GetTransformName(target)}");
            ChangeState(AllyStateId.Assist);
            return CurrentStateId == AllyStateId.Assist;
        }

        /// <summary>
        /// 在 Assist 内部切换当前助战目标。
        /// 用于当前目标死亡或失效后，继续攻击小队战斗上下文里的下一个目标。
        /// </summary>
        public bool TrySwitchAssistTarget(Transform target)
        {
            if (target == null || CurrentStateId != AllyStateId.Assist)
            {
                return false;
            }

            if (_combatDriver == null || !_combatDriver.HasAssistAction)
            {
                return false;
            }

            if (target == _currentAssistTarget)
            {
                return true;
            }

            Transform previousTarget = _currentAssistTarget;
            _currentAssistTarget = target;
            AllyDebugLog.Raise(
                gameObject,
                AllyDebugCategory.State,
                $"assist target switched, from={GetTransformName(previousTarget)}, to={GetTransformName(target)}");
            return true;
        }

        /// <summary>
        /// 请求进入通用动作状态。
        /// 用于队友主动技能、后续连携技或其他由外部命令触发的攻击动作。
        /// </summary>
        public bool RequestAction(CombatActionDefinition action, Transform target)
        {
            if (action == null)
            {
                LogActionRequestRejected("action is null");
                return false;
            }

            if (CurrentStateId == AllyStateId.Dead
                || CurrentStateId == AllyStateId.Hit
                || CurrentStateId == AllyStateId.Action)
            {
                LogActionRequestRejected($"state is {CurrentStateId}");
                return false;
            }

            _currentAction = action;
            _currentActionTarget = target;
            _returnStateAfterAction = CurrentStateId == AllyStateId.Assist ? AllyStateId.Assist : GetDefaultLocomotionState();

            AllyDebugLog.Raise(
                gameObject,
                AllyDebugCategory.State,
                $"action request accepted, action={action.ActionId}, target={GetTransformName(target)}, return={_returnStateAfterAction}");

            ChangeState(AllyStateId.Action);
            return CurrentStateId == AllyStateId.Action;
        }

        /// <summary>
        /// 完成通用动作状态，并返回动作开始前约定的大状态。
        /// </summary>
        public void CompleteAction()
        {
            AllyStateId returnState = ResolveReturnStateAfterAction();
            ClearCurrentAction();
            ChangeState(returnState);
        }

        /// <summary>
        /// 取消当前助战流程。
        /// target 为 null 时取消任意助战；否则只取消当前目标匹配的助战。
        /// </summary>
        public bool CancelAssist(Transform target = null)
        {
            if (CurrentStateId != AllyStateId.Assist)
            {
                return false;
            }

            if (target != null && target != _currentAssistTarget)
            {
                return false;
            }

            _currentAssistTarget = null;
            AllyDebugLog.Raise(
                gameObject,
                AllyDebugCategory.State,
                $"assist cancelled, target={GetTransformName(target)}, next={(followTarget != null ? AllyStateId.Follow : AllyStateId.Idle)}");
            ChangeState(followTarget != null ? AllyStateId.Follow : AllyStateId.Idle);
            return true;
        }

        /// <summary>
        /// 完成当前助战流程并回到跟随。
        /// </summary>
        public void CompleteAssist()
        {
            _currentAssistTarget = null;
            ChangeState(followTarget != null ? AllyStateId.Follow : AllyStateId.Idle);
        }

        /// <summary>
        /// 请求进入受击状态。
        /// 受击可以打断 Follow 和 Assist，但不能覆盖 Dead。
        /// </summary>
        public void RequestHit()
        {
            if (CurrentStateId == AllyStateId.Dead)
            {
                return;
            }

            _currentAssistTarget = null;
            ClearCurrentAction();
            ChangeState(AllyStateId.Hit);
        }

        /// <summary>
        /// 请求进入死亡状态。
        /// 死亡是队友状态机最高优先级的终止状态。
        /// </summary>
        public void RequestDead()
        {
            _currentAssistTarget = null;
            ClearCurrentAction();
            ChangeState(AllyStateId.Dead);
        }

        private void RegisterState(IAllyState state)
        {
            _states[state.StateId] = state;
        }

        private static string GetTransformName(Transform target)
        {
            return target != null ? target.name : "None";
        }

        private void LogAssistRequestRejected(string reason)
        {
            AllyDebugLog.Raise(
                gameObject,
                AllyDebugCategory.State,
                $"assist request rejected: {reason}");
        }

        private AllyStateId ResolveReturnStateAfterAction()
        {
            if (_returnStateAfterAction == AllyStateId.Assist
                && _currentAssistTarget != null
                && _currentAssistTarget.gameObject.activeInHierarchy)
            {
                return AllyStateId.Assist;
            }

            return GetDefaultLocomotionState();
        }

        private AllyStateId GetDefaultLocomotionState()
        {
            return followTarget != null ? AllyStateId.Follow : AllyStateId.Idle;
        }

        private void ClearCurrentAction()
        {
            _currentAction = null;
            _currentActionTarget = null;
            _returnStateAfterAction = AllyStateId.None;
        }

        private void LogActionRequestRejected(string reason)
        {
            AllyDebugLog.Raise(
                gameObject,
                AllyDebugCategory.State,
                $"action request rejected: {reason}");
        }
    }
}
