using System.Collections.Generic;
using EndLink.Combat;
using UnityEngine;

namespace EndLink.Ally
{
    /// <summary>
    /// 队友有限状态机。
    /// 当前负责 Idle、Follow、Assist、Hit、Dead 的状态切换；不读取玩家输入，也不直接监听战斗事件。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AllyCombatDriver))]
    public sealed class AllyStateMachine : MonoBehaviour
    {
        [Header("初始状态")]
        [Tooltip("队友启用后的初始状态。没有跟随目标时建议使用 Idle，有跟随目标时会自动进入 Follow。")]
        [SerializeField]
        private AllyStateId initialState = AllyStateId.Idle;

        [Header("跟随")]
        [Tooltip("队友跟随目标。当前 Follow 状态只持有目标引用，下一步接跟随移动时会使用它。")]
        [SerializeField]
        private Transform followTarget;

        [Header("助战状态")]
        [Tooltip("助战状态的最短持续时间。最终持续时间会取该值和当前 Assist Action 总时长中的较大值。")]
        [SerializeField, Min(0.01f)]
        private float assistDuration = 0.45f;

        [Header("受击状态")]
        [Tooltip("队友受击硬直的基础持续时间。")]
        [SerializeField, Min(0.01f)]
        private float hitDuration = 0.3f;

        private readonly Dictionary<AllyStateId, IAllyState> _states = new();
        private IAllyState _currentState;
        private AllyCombatDriver _combatDriver;
        private Transform _currentAssistTarget;

        /// <summary>当前状态标识，方便 Inspector 和调试工具观察。</summary>
        public AllyStateId CurrentStateId => _currentState?.StateId ?? AllyStateId.None;

        /// <summary>当前绑定的队友战斗执行器。</summary>
        public AllyCombatDriver CombatDriver => _combatDriver;

        /// <summary>当前跟随目标。</summary>
        public Transform FollowTarget => followTarget;

        /// <summary>当前助战目标。</summary>
        public Transform CurrentAssistTarget => _currentAssistTarget;

        /// <summary>队友受击状态持续时间。</summary>
        public float HitDuration => hitDuration;

        /// <summary>当前助战状态持续时间，至少覆盖动作配置中的前摇、有效时间和后摇。</summary>
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

            AllyStateContext context = new AllyStateContext(
                this,
                transform,
                _combatDriver);

            RegisterState(new AllyIdleState(context));
            RegisterState(new AllyFollowState(context));
            RegisterState(new AllyAssistState(context));
            RegisterState(new AllyHitState(context));
            RegisterState(new AllyDeadState(context));
        }

        private void Start()
        {
            ChangeState(initialState);
        }

        private void Update()
        {
            _currentState?.Tick(Time.deltaTime);
        }

        private void OnValidate()
        {
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

            _currentState?.Exit();
            _currentState = nextState;
            _currentState.Enter();
        }

        /// <summary>
        /// 设置队友跟随目标。
        /// 当前只负责保存引用和在 Idle/Follow 之间切换；实际移动下一步由跟随组件处理。
        /// </summary>
        public void SetFollowTarget(Transform target)
        {
            followTarget = target;

            if (CurrentStateId == AllyStateId.Dead || CurrentStateId == AllyStateId.Assist || CurrentStateId == AllyStateId.Hit)
            {
                return;
            }

            ChangeState(followTarget != null ? AllyStateId.Follow : AllyStateId.Idle);
        }

        /// <summary>
        /// 请求进入助战状态。
        /// 决策来自 AllyBrain 或后续连携规则系统；状态机只判断当前状态是否允许响应。
        /// </summary>
        public bool RequestAssist(Transform target)
        {
            if (target == null || CurrentStateId == AllyStateId.Dead || CurrentStateId == AllyStateId.Hit)
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
        /// 请求进入受击状态。
        /// 受击可以打断 Follow 和 Assist，但不能覆盖 Dead。
        /// </summary>
        public void RequestHit()
        {
            if (CurrentStateId == AllyStateId.Dead)
            {
                return;
            }

            ChangeState(AllyStateId.Hit);
        }

        /// <summary>
        /// 请求进入死亡状态。
        /// 死亡是队友状态机最高优先级的终止状态。
        /// </summary>
        public void RequestDead()
        {
            ChangeState(AllyStateId.Dead);
        }

        private void RegisterState(IAllyState state)
        {
            _states[state.StateId] = state;
        }
    }
}
