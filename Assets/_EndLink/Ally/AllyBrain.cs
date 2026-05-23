using EndLink.Combat;
using UnityEngine;

namespace EndLink.Ally
{
    /// <summary>
    /// 队友大脑组件。
    /// 当前负责监听战斗事件并决定是否请求助战；真正接近、攻击和回归由状态机与执行器完成。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AllyCombatDriver))]
    [RequireComponent(typeof(AllyStateMachine))]
    public sealed class AllyBrain : MonoBehaviour
    {
        [Header("组件引用")]
        [Tooltip("队友战斗执行器。为空时会自动从同一 GameObject 获取。")]
        [SerializeField]
        private AllyCombatDriver combatDriver;

        [Tooltip("队友状态机。为空时会自动从同一 GameObject 获取。")]
        [SerializeField]
        private AllyStateMachine stateMachine;

        [Header("响应规则")]
        [Tooltip("是否响应 Hitbox 命中事件。当前用于主控命中敌人后触发队友自动助战。")]
        [SerializeField]
        private bool respondToHitLanded = true;

        [Tooltip("可选的事件来源过滤。拖入主角后，队友只响应主角造成的 HitLanded。")]
        [SerializeField]
        private GameObject requiredSource;

        [Tooltip("是否忽略由自己发出的事件，避免队友自己的命中再次触发自己助战。")]
        [SerializeField]
        private bool ignoreSelfEvents = true;

        [Tooltip("事件目标为空时，是否尝试搜索最近敌人作为助战目标。")]
        [SerializeField]
        private bool searchNearestEnemyWhenNoEventTarget = true;

        [Tooltip("搜索最近敌人的半径。只在事件目标为空且允许搜索时使用。")]
        [SerializeField, Min(0.1f)]
        private float targetSearchRadius = 8f;

        [Tooltip("最近敌人搜索使用的 LayerMask。建议设置为 Enemy。")]
        [SerializeField]
        private LayerMask enemyLayerMask;

        [Header("调试")]
        [Tooltip("响应事件、过滤事件和执行失败时是否打印 Debug.Log。")]
        [SerializeField]
        private bool logDecisions;

        private const int TargetSearchCapacity = 16;
        private readonly Collider[] _targetSearchResults = new Collider[TargetSearchCapacity];
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

        private void OnValidate()
        {
            targetSearchRadius = Mathf.Max(0.1f, targetSearchRadius);
        }

        private void HandleCombatEvent(CombatEvent eventData)
        {
            if (eventData.EventType == CombatEventType.Dead)
            {
                HandleTargetDead(eventData.Target);
                return;
            }

            if (!TryResolveResponseTarget(eventData, out Transform target))
            {
                return;
            }

            _currentTarget = target;

            if (logDecisions)
            {
                Debug.Log($"AllyBrain responding to {eventData.EventType}, target: {target.name}", this);
            }

            bool requested = StateMachine != null && StateMachine.RequestAssist(target);

            if (!requested && logDecisions)
            {
                Debug.Log($"AllyBrain assist rejected: {GetAssistRejectReason(target)}", this);
            }
        }

        private void HandleTargetDead(GameObject deadTarget)
        {
            if (deadTarget == null || _currentTarget == null || deadTarget.transform != _currentTarget)
            {
                return;
            }

            StateMachine.CancelAssist(_currentTarget);
            _currentTarget = null;
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

            if (eventData.Target != null && eventData.Target != gameObject)
            {
                target = eventData.Target.transform;
                return target != null && IsCombatTargetValid(target);
            }

            return searchNearestEnemyWhenNoEventTarget && TryFindNearestEnemy(out target);
        }

        private bool TryFindNearestEnemy(out Transform target)
        {
            target = null;

            if (enemyLayerMask.value == 0)
            {
                return false;
            }

            Vector3 origin = StateMachine != null && StateMachine.FollowTarget != null
                ? StateMachine.FollowTarget.position
                : transform.position;

            int hitCount = Physics.OverlapSphereNonAlloc(
                origin,
                targetSearchRadius,
                _targetSearchResults,
                enemyLayerMask,
                QueryTriggerInteraction.Ignore);

            float bestSqrDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _targetSearchResults[i];
                if (hit == null || hit.gameObject == gameObject)
                {
                    continue;
                }

                Transform candidateTarget = ResolveTargetTransform(hit);
                if (candidateTarget == null || !IsCombatTargetValid(candidateTarget))
                {
                    continue;
                }

                Vector3 toTarget = candidateTarget.position - origin;
                toTarget.y = 0f;

                float sqrDistance = toTarget.sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    target = candidateTarget;
                }
            }

            return target != null;
        }

        private static Transform ResolveTargetTransform(Collider hit)
        {
            IHitReceiver receiver = hit.GetComponentInParent<IHitReceiver>();
            if (receiver is Component receiverComponent)
            {
                return receiverComponent.transform;
            }

            return hit.transform;
        }

        private static bool IsCombatTargetValid(Transform target)
        {
            ICombatTarget combatTarget = target.GetComponentInParent<ICombatTarget>();
            return combatTarget == null || combatTarget.IsTargetable;
        }

        private string GetAssistRejectReason(Transform target)
        {
            if (target == null)
            {
                return "target is null";
            }

            if (StateMachine == null)
            {
                return "state machine is missing";
            }

            AllyStateId currentStateId = StateMachine.CurrentStateId;
            if (currentStateId == AllyStateId.Dead
                || currentStateId == AllyStateId.Hit
                || currentStateId == AllyStateId.Assist)
            {
                return $"state is {currentStateId}";
            }

            if (CombatDriver == null)
            {
                return "combat driver is missing";
            }

            if (!CombatDriver.CanAssist)
            {
                return "combat driver cannot assist, usually cooldown or action configuration";
            }

            return "unknown state machine rejection";
        }
    }
}
