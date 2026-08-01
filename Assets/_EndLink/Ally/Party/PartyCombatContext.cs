using System.Collections.Generic;
using EndLink.Ally;
using EndLink.Combat;
using UnityEngine;

namespace EndLink.Party
{
    /// <summary>
    /// 小队级战斗上下文。
    /// 负责记录小队当前是否处于战斗、当前主目标和已知敌人集合。
    /// 不读取输入，不执行攻击，不驱动具体角色状态；队友 AI、战斗 UI 和连携系统优先从这里读取战斗态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyCombatContext : MonoBehaviour
    {
        [Header("脱战")]
        [Tooltip("没有有效敌人后，保持战斗态的时间。当前第一版在最后一个敌人死亡时会立即脱战；该值主要作为远离/失联脱战扩展入口。")]
        [SerializeField, Min(0f)]
        private float combatExitDelay = 3f;

        [Header("调试")]
        [Tooltip("战斗态进入、退出、目标变化时是否打印日志。")]
        [SerializeField]
        private bool logStateChanges;

        private readonly List<Transform> _knownEnemies = new();
        private readonly List<AllyStateMachine> _partyAllies = new();
        private PartyManager _partyManager;
        private Transform _currentPrimaryTarget;
        private bool _isInCombat;
        private float _lastCombatEventTime;

        /// <summary>小队当前是否处于战斗上下文中。</summary>
        public bool IsInCombat => _isInCombat;

        /// <summary>当前主目标。队友 AI 可以优先围绕它选择攻击目标。</summary>
        public Transform CurrentPrimaryTarget => _currentPrimaryTarget;

        /// <summary>当前已知敌人数量。</summary>
        public int KnownEnemyCount => _knownEnemies.Count;

        /// <summary>最后一次收到有效战斗事件的 Time.time。</summary>
        public float LastCombatEventTime => _lastCombatEventTime;

        private void OnEnable()
        {
            CacheReferences();
            CombatEventsBus.Raised += HandleCombatEvent;
        }

        private void OnDisable()
        {
            CombatEventsBus.Raised -= HandleCombatEvent;
        }

        private void OnValidate()
        {
            combatExitDelay = Mathf.Max(0f, combatExitDelay);
        }

        private void Reset()
        {
            CacheReferences();
        }

        private void Update()
        {
            PruneInvalidEnemies();

            if (_isInCombat && _knownEnemies.Count == 0 && Time.time - _lastCombatEventTime >= combatExitDelay)
            {
                ExitCombat();
            }
        }

        /// <summary>
        /// 把当前已知敌人复制到外部 List，避免暴露内部集合。
        /// </summary>
        public IReadOnlyList<Transform> GetKnownEnemies(List<Transform> results)
        {
            results.Clear();
            PruneInvalidEnemies();
            results.AddRange(_knownEnemies);
            return results;
        }

        /// <summary>
        /// 判断目标是否已经在已知敌人集合里。
        /// </summary>
        public bool ContainsKnownEnemy(Transform target)
        {
            return target != null && _knownEnemies.Contains(target);
        }

        /// <summary>
        /// 手动清空战斗上下文。
        /// 切场景、重置战斗或调试按钮可以调用。
        /// </summary>
        public void ClearCombatContext()
        {
            _knownEnemies.Clear();
            _currentPrimaryTarget = null;
            ExitCombat();
        }

        private void HandleCombatEvent(CombatEvent eventData)
        {
            if (eventData.EventType == CombatEventType.Dead)
            {
                RemoveKnownEnemy(eventData.Target != null
                    ? CombatTargetUtility.ResolveRoot(eventData.Target.transform)
                    : null);
                return;
            }

            Transform eventTarget = ResolveEnemyTarget(eventData);
            if (eventTarget == null)
            {
                return;
            }

            RegisterCombatTarget(eventTarget);
        }

        private Transform ResolveEnemyTarget(CombatEvent eventData)
        {
            switch (eventData.EventType)
            {
                case CombatEventType.ActionStarted:
                case CombatEventType.HitLanded:
                case CombatEventType.TagAdded:
                case CombatEventType.ReactionTriggered:
                    return ResolveValidEnemyTarget(eventData.Target);
                case CombatEventType.Damaged:
                    return ResolveValidEnemyTarget(eventData.Target)
                        ?? ResolveValidEnemyTarget(eventData.Source);
                default:
                    return null;
            }
        }

        private Transform ResolveValidEnemyTarget(GameObject targetObject)
        {
            if (targetObject == null)
            {
                return null;
            }

            if (!CombatTargetUtility.TryResolveTargetableRoot(
                    targetObject.transform,
                    out Transform target))
            {
                return null;
            }

            if (IsPartyMember(target))
            {
                return null;
            }

            return target;
        }

        private void RegisterCombatTarget(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return;
            }

            if (!_knownEnemies.Contains(target))
            {
                _knownEnemies.Add(target);
            }

            _currentPrimaryTarget = target;
            _lastCombatEventTime = Time.time;

            if (!_isInCombat)
            {
                _isInCombat = true;
                _partyManager?.NotifyCombatStarted();
                LogState("enter combat");
            }

            LogState($"primary target={target.name}, known={_knownEnemies.Count}");
        }

        private void RemoveKnownEnemy(Transform target)
        {
            if (target == null)
            {
                return;
            }

            _knownEnemies.Remove(target);

            if (_currentPrimaryTarget == target)
            {
                _currentPrimaryTarget = _knownEnemies.Count > 0 ? _knownEnemies[0] : null;
            }

            _lastCombatEventTime = Time.time;

            if (_knownEnemies.Count == 0)
            {
                ExitCombat();
            }
            else
            {
                LogState($"remove target={target.name}, next={_currentPrimaryTarget.name}");
            }
        }

        private void PruneInvalidEnemies()
        {
            for (int i = _knownEnemies.Count - 1; i >= 0; i--)
            {
                Transform target = _knownEnemies[i];
                if (!IsKnownEnemyStillValid(target))
                {
                    _knownEnemies.RemoveAt(i);
                }
            }

            if (_currentPrimaryTarget != null && !_knownEnemies.Contains(_currentPrimaryTarget))
            {
                _currentPrimaryTarget = _knownEnemies.Count > 0 ? _knownEnemies[0] : null;
            }
        }

        private static bool IsKnownEnemyStillValid(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return false;
            }

            return CombatTargetUtility.IsTargetable(target);
        }

        private bool IsPartyMember(Transform target)
        {
            if (target == null)
            {
                return false;
            }

            if (_partyManager == null)
            {
                return false;
            }

            if (IsSameOrChild(target, _partyManager.MainCharacter))
            {
                return true;
            }

            _partyManager.GetAllies(_partyAllies);
            for (int i = 0; i < _partyAllies.Count; i++)
            {
                AllyStateMachine ally = _partyAllies[i];
                if (ally != null && IsSameOrChild(target, ally.transform))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSameOrChild(Transform target, Transform root)
        {
            return root != null && (target == root || target.IsChildOf(root));
        }

        private void ExitCombat()
        {
            if (!_isInCombat && _currentPrimaryTarget == null)
            {
                return;
            }

            _isInCombat = false;
            _currentPrimaryTarget = null;
            _partyManager?.NotifyCombatEnded();
            LogState("exit combat");
        }

        private void LogState(string message)
        {
            if (logStateChanges)
            {
                Debug.Log($"PartyCombatContext: {message}", this);
            }
        }

        private void CacheReferences()
        {
            if (_partyManager == null)
            {
                _partyManager = GetComponent<PartyManager>();
            }
        }
    }
}
