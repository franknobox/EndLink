using System;
using System.Collections.Generic;
using EndLink.Combat;
using UnityEngine;

namespace EndLink.Party
{
    /// <summary>
    /// 小队连携窗口上下文。
    /// 监听协议反应事件，维护全队共享的连携技解锁窗口、反应目标集合和目标解析。
    /// 不读取输入，也不执行具体角色动作。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PartyManager))]
    public sealed class PartyLinkContext : MonoBehaviour
    {
        [Header("连携窗口")]
        [Tooltip("协议反应触发后，全队三个连携技保持可选的时间。窗口内再次触发反应会刷新为完整时长。")]
        [SerializeField, Min(0.1f)]
        private float linkWindowDuration = 4f;

        [Header("组件引用")]
        [Tooltip("固定小队管理器。用于读取主控和主控的自动软锁目标。为空时优先从当前物体获取。")]
        [SerializeField]
        private PartyManager partyManager;

        [Header("调试")]
        [Tooltip("是否打印连携窗口开启、刷新、消费和过期日志。")]
        [SerializeField]
        private bool logWindowChanges;

        private readonly List<Transform> _reactionTargets = new();
        private PlayerTargeting _playerTargeting;
        private float _windowEndTime;
        private bool _isWindowOpen;
        private bool _hasMultipleReactionTargets;

        /// <summary>连携窗口首次开启。</summary>
        public event Action WindowOpened;

        /// <summary>窗口期内再次触发反应，持续时间被刷新。</summary>
        public event Action WindowRefreshed;

        /// <summary>连携窗口被释放消费、到期或手动关闭。</summary>
        public event Action WindowClosed;

        /// <summary>当前是否存在可使用的全队连携窗口。</summary>
        public bool IsWindowOpen => _isWindowOpen && Time.time < _windowEndTime;

        /// <summary>连携窗口总时长。</summary>
        public float WindowDuration => linkWindowDuration;

        /// <summary>连携窗口剩余时间。</summary>
        public float RemainingTime => IsWindowOpen ? Mathf.Max(0f, _windowEndTime - Time.time) : 0f;

        /// <summary>连携窗口剩余比例，1 表示刚触发或刷新，0 表示已结束。</summary>
        public float RemainingNormalized => linkWindowDuration > 0f
            ? Mathf.Clamp01(RemainingTime / linkWindowDuration)
            : 0f;

        /// <summary>窗口期内记录到的反应目标数量，读取前会清理已失效目标。</summary>
        public int ReactionTargetCount
        {
            get
            {
                PruneInvalidTargets();
                return _reactionTargets.Count;
            }
        }

        private void OnEnable()
        {
            CacheReferences();
            CombatEventsBus.Raised += HandleCombatEvent;
        }

        private void OnDisable()
        {
            CombatEventsBus.Raised -= HandleCombatEvent;
            CloseWindow("component disabled");
        }

        private void Update()
        {
            if (_isWindowOpen && Time.time >= _windowEndTime)
            {
                CloseWindow("expired");
            }
        }

        private void Reset()
        {
            CacheReferences();
        }

        private void OnValidate()
        {
            linkWindowDuration = Mathf.Max(0.1f, linkWindowDuration);
        }

        /// <summary>
        /// 解析本次连携技应该攻击的目标。
        /// 单一有效反应目标时优先该目标；存在多个反应目标时优先主控软锁；
        /// 反应目标全部失效后仍可回退到当前软锁目标，不会因此关闭窗口。
        /// </summary>
        public Transform ResolveLinkTarget()
        {
            if (!IsWindowOpen)
            {
                return null;
            }

            PruneInvalidTargets();
            Transform softTarget = ResolveValidSoftTarget();

            if (_hasMultipleReactionTargets && softTarget != null)
            {
                return softTarget;
            }

            if (_reactionTargets.Count == 1)
            {
                return _reactionTargets[0];
            }

            if (_reactionTargets.Count > 1)
            {
                return softTarget != null ? softTarget : _reactionTargets[^1];
            }

            return softTarget;
        }

        /// <summary>
        /// 成功释放任意一个连携技后消费整个全队窗口。
        /// </summary>
        public bool ConsumeWindow()
        {
            if (!IsWindowOpen)
            {
                return false;
            }

            CloseWindow("consumed");
            return true;
        }

        private void HandleCombatEvent(CombatEvent eventData)
        {
            if (eventData.EventType != CombatEventType.ReactionTriggered)
            {
                return;
            }

            if (eventData.Target == null
                || eventData.Target.GetComponentInParent<ICombatTarget>() == null
                || IsPartyMember(eventData.Target.transform))
            {
                return;
            }

            RegisterReactionTarget(eventData.Target);
            OpenOrRefreshWindow(eventData.ReactionRule);
        }

        private void OpenOrRefreshWindow(CombatTagCombinationRule reactionRule)
        {
            bool wasOpen = IsWindowOpen;
            _isWindowOpen = true;
            _windowEndTime = Time.time + linkWindowDuration;

            if (wasOpen)
            {
                WindowRefreshed?.Invoke();
                LogWindow($"refreshed, rule={GetRuleName(reactionRule)}, remaining={linkWindowDuration:F2}");
                return;
            }

            WindowOpened?.Invoke();
            LogWindow($"opened, rule={GetRuleName(reactionRule)}, duration={linkWindowDuration:F2}");
        }

        private void RegisterReactionTarget(GameObject targetObject)
        {
            Transform target = ResolveCombatTarget(targetObject);
            if (target == null || IsPartyMember(target))
            {
                return;
            }

            if (!IsWindowOpen)
            {
                _reactionTargets.Clear();
                _hasMultipleReactionTargets = false;
            }

            if (!_reactionTargets.Contains(target))
            {
                _reactionTargets.Add(target);
                _hasMultipleReactionTargets = _reactionTargets.Count > 1;
            }
        }

        private Transform ResolveValidSoftTarget()
        {
            CachePlayerTargeting();

            if (_playerTargeting == null || !_playerTargeting.HasTarget)
            {
                return null;
            }

            return IsTargetValid(_playerTargeting.CurrentTarget)
                ? _playerTargeting.CurrentTarget
                : null;
        }

        private void PruneInvalidTargets()
        {
            for (int i = _reactionTargets.Count - 1; i >= 0; i--)
            {
                if (!IsTargetValid(_reactionTargets[i]))
                {
                    _reactionTargets.RemoveAt(i);
                }
            }
        }

        private static Transform ResolveCombatTarget(GameObject targetObject)
        {
            if (targetObject == null)
            {
                return null;
            }

            ICombatTarget combatTarget = targetObject.GetComponentInParent<ICombatTarget>();
            return combatTarget != null && combatTarget.IsTargetable
                ? combatTarget.TargetTransform
                : null;
        }

        private static bool IsTargetValid(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return false;
            }

            ICombatTarget combatTarget = target.GetComponentInParent<ICombatTarget>();
            return combatTarget == null || combatTarget.IsTargetable;
        }

        private bool IsPartyMember(Transform target)
        {
            if (target == null || partyManager == null)
            {
                return false;
            }

            if (IsSameOrChild(target, partyManager.MainCharacter))
            {
                return true;
            }

            Transform allyA = partyManager.AllySlotA?.AllyTransform;
            Transform allyB = partyManager.AllySlotB?.AllyTransform;
            return IsSameOrChild(target, allyA) || IsSameOrChild(target, allyB);
        }

        private static bool IsSameOrChild(Transform target, Transform root)
        {
            return root != null && (target == root || target.IsChildOf(root));
        }

        private void CloseWindow(string reason)
        {
            if (!_isWindowOpen)
            {
                return;
            }

            _isWindowOpen = false;
            _windowEndTime = 0f;
            _reactionTargets.Clear();
            _hasMultipleReactionTargets = false;
            WindowClosed?.Invoke();
            LogWindow($"closed, reason={reason}");
        }

        private void CacheReferences()
        {
            if (partyManager == null)
            {
                partyManager = GetComponent<PartyManager>();
            }

            CachePlayerTargeting();
        }

        private void CachePlayerTargeting()
        {
            if (_playerTargeting != null)
            {
                return;
            }

            if (partyManager == null)
            {
                partyManager = GetComponent<PartyManager>();
            }

            Transform mainCharacter = partyManager != null ? partyManager.MainCharacter : null;
            if (mainCharacter != null)
            {
                mainCharacter.TryGetComponent(out _playerTargeting);
            }
        }

        private void LogWindow(string message)
        {
            if (logWindowChanges)
            {
                Debug.Log($"PartyLinkContext: {message}", this);
            }
        }

        private static string GetRuleName(CombatTagCombinationRule rule)
        {
            return rule != null ? rule.name : "None";
        }
    }
}
