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
        [Tooltip("队友接近助战目标到该距离内时，Assist 内部切换到攻击阶段。")]
        [SerializeField, Min(0.01f)]
        private float assistAttackRange = 1.8f;

        [Tooltip("持续助战时，目标离队友超过该距离会回到 Assist 内部接近阶段。建议略大于 assistAttackRange。")]
        [SerializeField, Min(0.01f)]
        private float assistReengageRange = 2.4f;

        [Tooltip("队友与主控距离超过该值时放弃助战并回到跟随。设置为 0 表示不因距离主控过远而取消。")]
        [SerializeField, Min(0f)]
        private float assistBreakOffDistance = 12f;

        [Header("助战攻击")]
        [Tooltip("助战攻击状态的最短持续时间。最终持续时间会取该值和当前 Assist Action 总时长中的较大值。")]
        [SerializeField, Min(0.01f)]
        private float assistDuration = 0.45f;

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
        private Transform _currentAssistTarget;

        /// <summary>当前状态标识，方便 Inspector 和调试工具观察。</summary>
        public AllyStateId CurrentStateId => _currentState?.StateId ?? AllyStateId.None;

        /// <summary>当前绑定的队友战斗执行器。</summary>
        public AllyCombatDriver CombatDriver => _combatDriver;

        /// <summary>当前绑定的队友跟随移动组件。</summary>
        public AllyFollowMotor FollowMotor => _followMotor;

        /// <summary>当前跟随目标。</summary>
        public Transform FollowTarget => followTarget;

        /// <summary>当前助战目标。</summary>
        public Transform CurrentAssistTarget => _currentAssistTarget;

        /// <summary>助战接近时进入攻击的距离。</summary>
        public float AssistAttackRange => assistAttackRange;

        /// <summary>持续助战时重新接近目标的距离。</summary>
        public float AssistReengageRange => assistReengageRange;

        /// <summary>主控离队友过远时放弃助战的距离。</summary>
        public float AssistBreakOffDistance => assistBreakOffDistance;

        /// <summary>队友受击状态持续时间。</summary>
        public float HitDuration => hitDuration;

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

            AllyStateContext context = new AllyStateContext(
                this,
                transform,
                _combatDriver,
                _followMotor);

            RegisterState(new AllyIdleState(context));
            RegisterState(new AllyFollowState(context));
            RegisterState(new AllyAssistState(context));
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
            assistAttackRange = Mathf.Max(0.01f, assistAttackRange);
            assistReengageRange = Mathf.Max(assistAttackRange, assistReengageRange);
            assistBreakOffDistance = Mathf.Max(0f, assistBreakOffDistance);
            assistDuration = Mathf.Max(0.01f, assistDuration);
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
            if (target == null
                || CurrentStateId == AllyStateId.Dead
                || CurrentStateId == AllyStateId.Hit
                || CurrentStateId == AllyStateId.Assist)
            {
                return false;
            }

            if (_combatDriver == null || !_combatDriver.CanAssist)
            {
                return false;
            }

            _currentAssistTarget = target;
            ChangeState(AllyStateId.Assist);
            return CurrentStateId == AllyStateId.Assist;
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
            ChangeState(AllyStateId.Hit);
        }

        /// <summary>
        /// 请求进入死亡状态。
        /// 死亡是队友状态机最高优先级的终止状态。
        /// </summary>
        public void RequestDead()
        {
            _currentAssistTarget = null;
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
    }
}
