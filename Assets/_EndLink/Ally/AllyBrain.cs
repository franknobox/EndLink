using EndLink.Combat;
using UnityEngine;

namespace EndLink.Ally
{
    /// <summary>
    /// 队友大脑组件。
    /// 负责监听 CombatEventsBus，并根据当前规则判断是否让队友响应。
    /// 它不直接生成 Hitbox，也不直接执行动作；真正的状态切换交给 AllyStateMachine。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AllyCombatDriver))]
    [RequireComponent(typeof(AllyStateMachine))]
    public sealed class AllyBrain : MonoBehaviour
    {
        [Header("组件引用")]
        [Tooltip("队友战斗执行器。为空时会自动从同一 GameObject 上获取。")]
        [SerializeField]
        private AllyCombatDriver combatDriver;

        [Tooltip("队友状态机。为空时会自动从同一 GameObject 上获取。")]
        [SerializeField]
        private AllyStateMachine stateMachine;

        [Header("响应规则")]
        [Tooltip("是否响应 Hitbox 命中事件。当前木桩队友阶段推荐开启：主角命中木桩后，队友请求进入 Assist 状态。")]
        [SerializeField]
        private bool respondToHitLanded = true;

        [Tooltip("可选的事件来源过滤。拖入主角后，队友只响应主角造成的事件；为空时响应所有非自身来源。")]
        [SerializeField]
        private GameObject requiredSource;

        [Tooltip("是否忽略由自己发出的事件。建议保持开启，避免队友自己的命中再次触发自己出手。")]
        [SerializeField]
        private bool ignoreSelfEvents = true;

        [Tooltip("是否要求事件必须带有目标。当前助战攻击需要明确目标，因此建议保持开启。")]
        [SerializeField]
        private bool requireTarget = true;

        [Header("调试")]
        [Tooltip("响应事件、过滤事件和执行失败时是否打印 Debug.Log。")]
        [SerializeField]
        private bool logDecisions;

        private Transform _currentTarget;

        /// <summary>当前绑定的队友战斗执行器。</summary>
        public AllyCombatDriver CombatDriver
        {
            get
            {
                if (combatDriver == null)
                {
                    combatDriver = GetComponent<AllyCombatDriver>();
                }

                return combatDriver;
            }
        }

        /// <summary>当前绑定的队友状态机。</summary>
        public AllyStateMachine StateMachine
        {
            get
            {
                if (stateMachine == null)
                {
                    stateMachine = GetComponent<AllyStateMachine>();
                }

                return stateMachine;
            }
        }

        /// <summary>最近一次响应事件时锁定的目标。</summary>
        public Transform CurrentTarget => _currentTarget;

        private void Awake()
        {
            if (combatDriver == null)
            {
                combatDriver = GetComponent<AllyCombatDriver>();
            }

            if (stateMachine == null)
            {
                stateMachine = GetComponent<AllyStateMachine>();
            }
        }

        private void OnEnable()
        {
            CombatEventsBus.Raised += HandleCombatEvent;
        }

        private void OnDisable()
        {
            CombatEventsBus.Raised -= HandleCombatEvent;
        }

        private void Reset()
        {
            combatDriver = GetComponent<AllyCombatDriver>();
            stateMachine = GetComponent<AllyStateMachine>();
        }

        private void HandleCombatEvent(CombatEvent eventData)
        {
            if (!TryResolveResponseTarget(eventData, out Transform target))
            {
                return;
            }

            _currentTarget = target;

            if (logDecisions)
            {
                string targetName = target != null ? target.name : "None";
                Debug.Log($"AllyBrain responding to {eventData.EventType}, target: {targetName}", this);
            }

            bool requested = StateMachine != null && StateMachine.RequestAssist(target);

            if (!requested && logDecisions)
            {
                Debug.Log("AllyBrain decided to respond, but AllyStateMachine rejected the Assist request.", this);
            }
        }

        private bool TryResolveResponseTarget(CombatEvent eventData, out Transform target)
        {
            target = null;

            if (!respondToHitLanded || eventData.EventType != CombatEventType.HitLanded)
            {
                return false;
            }

            if (ignoreSelfEvents && eventData.Source == gameObject)
            {
                return false;
            }

            if (requiredSource != null && eventData.Source != requiredSource)
            {
                return false;
            }

            if (eventData.Target == null)
            {
                return !requireTarget;
            }

            if (eventData.Target == gameObject)
            {
                return false;
            }

            target = eventData.Target.transform;
            return target != null;
        }
    }
}
