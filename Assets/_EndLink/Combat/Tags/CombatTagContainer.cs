using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace EndLink.Combat
{
    /// <summary>
    /// 战斗标签容器。
    /// 挂在可被战斗规则查询的目标身上，负责保存多标签、持续时间、增删事件和组合转化。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatTagContainer : MonoBehaviour, ICombatTagReceiver
    {
        private const int MaxCombinationDepth = 4;

        [Header("初始标签")]
        [Tooltip("物体启用时自动拥有的初始标签。持续时间小于等于 0 表示永久标签。")]
        [SerializeField]
        private List<ActiveCombatTag> initialTags = new();

        [Header("组合规则")]
        [Tooltip("标签组合规则。添加标签后会尝试匹配 A+B=>C。")]
        [SerializeField]
        private List<CombatTagCombinationRule> combinationRules = new();

        [Header("调试")]
        [Tooltip("添加、移除、过期、刷新和组合转化标签时是否打印 Debug.Log。")]
        [SerializeField]
        private bool logTagChanges;

        [Header("事件")]
        [SerializeField]
        private CombatTagEvent onTagAdded = new();

        [SerializeField]
        private CombatTagEvent onTagRemoved = new();

        [SerializeField]
        private CombatTagEvent onTagExpired = new();

        [SerializeField]
        private CombatTagEvent onTagRefreshed = new();

        [SerializeField]
        private CombatTagTransformEvent onTagTransformed = new();

        private readonly List<ActiveCombatTag> _activeTags = new();

        /// <summary>当前激活标签数量。</summary>
        public int ActiveTagCount => _activeTags.Count;

        /// <summary>标签添加事件。</summary>
        public CombatTagEvent OnTagAdded => onTagAdded;

        /// <summary>标签移除事件。</summary>
        public CombatTagEvent OnTagRemoved => onTagRemoved;

        /// <summary>标签过期事件。</summary>
        public CombatTagEvent OnTagExpired => onTagExpired;

        /// <summary>标签刷新事件。</summary>
        public CombatTagEvent OnTagRefreshed => onTagRefreshed;

        /// <summary>标签组合转化事件。</summary>
        public CombatTagTransformEvent OnTagTransformed => onTagTransformed;

        private void OnEnable()
        {
            ClearTags();

            foreach (ActiveCombatTag initialTag in initialTags)
            {
                if (initialTag == null)
                {
                    continue;
                }

                AddTag(initialTag.Tag, initialTag.HasDuration ? initialTag.RemainingDuration : 0f, gameObject);
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            for (int i = _activeTags.Count - 1; i >= 0; i--)
            {
                ActiveCombatTag activeTag = _activeTags[i];

                if (!activeTag.HasDuration)
                {
                    continue;
                }

                activeTag.Tick(deltaTime);

                if (activeTag.RemainingDuration > 0f)
                {
                    continue;
                }

                _activeTags.RemoveAt(i);
                NotifyTagExpired(activeTag.Tag);
            }
        }

        /// <summary>添加永久标签。</summary>
        public bool AddTag(CombatTagDefinition tag)
        {
            return AddTag(tag, 0f, gameObject);
        }

        /// <summary>添加永久标签，并记录标签来源。</summary>
        public bool AddTag(CombatTagDefinition tag, GameObject source)
        {
            return AddTag(tag, 0f, source);
        }

        /// <summary>添加标签。duration 小于等于 0 时表示永久标签。</summary>
        public bool AddTag(CombatTagDefinition tag, float duration)
        {
            return AddTag(tag, duration, gameObject);
        }

        /// <summary>添加标签并记录标签来源。duration 小于等于 0 时表示永久标签。</summary>
        public bool AddTag(CombatTagDefinition tag, float duration, GameObject source)
        {
            return AddTagInternal(tag, duration, source, true, 0);
        }

        /// <summary>移除标签。</summary>
        public bool RemoveTag(CombatTagDefinition tag)
        {
            return RemoveTag(tag, gameObject);
        }

        /// <summary>移除标签，并记录移除来源。</summary>
        public bool RemoveTag(CombatTagDefinition tag, GameObject source)
        {
            if (!IsLegalTag(tag))
            {
                return false;
            }

            int index = FindTagIndex(tag);

            if (index < 0)
            {
                return false;
            }

            _activeTags.RemoveAt(index);
            NotifyTagRemoved(tag, source);
            return true;
        }

        /// <summary>是否拥有指定标签。</summary>
        public bool HasTag(CombatTagDefinition tag)
        {
            return IsLegalTag(tag) && FindTagIndex(tag) >= 0;
        }

        /// <summary>尝试读取标签剩余持续时间。永久标签返回 false。</summary>
        public bool TryGetRemainingDuration(CombatTagDefinition tag, out float remainingDuration)
        {
            remainingDuration = 0f;

            int index = FindTagIndex(tag);

            if (index < 0)
            {
                return false;
            }

            ActiveCombatTag activeTag = _activeTags[index];

            if (!activeTag.HasDuration)
            {
                return false;
            }

            remainingDuration = activeTag.RemainingDuration;
            return true;
        }

        /// <summary>清空所有标签。</summary>
        public void ClearTags()
        {
            _activeTags.Clear();
        }

        private bool AddTagInternal(
            CombatTagDefinition tag,
            float duration,
            GameObject source,
            bool evaluateCombinations,
            int combinationDepth)
        {
            if (!IsLegalTag(tag))
            {
                LogInvalidTag(tag);
                return false;
            }

            int index = FindTagIndex(tag);

            if (index >= 0)
            {
                _activeTags[index].Refresh(duration);
                NotifyTagRefreshed(tag);
            }
            else
            {
                _activeTags.Add(new ActiveCombatTag(tag, duration));
                NotifyTagAdded(tag, source);
            }

            if (evaluateCombinations && combinationDepth < MaxCombinationDepth)
            {
                TryApplyCombinationRules(tag, source, combinationDepth);
            }

            return true;
        }

        private void TryApplyCombinationRules(CombatTagDefinition addedTag, GameObject source, int combinationDepth)
        {
            foreach (CombatTagCombinationRule rule in combinationRules)
            {
                if (rule == null || !rule.CanApply(addedTag, this))
                {
                    continue;
                }

                if (rule.RemoveSourceTags)
                {
                    RemoveTag(rule.FirstTag, source);
                    RemoveTag(rule.SecondTag, source);
                }

                AddTagInternal(rule.ResultTag, rule.ResultDuration, source, true, combinationDepth + 1);
                NotifyTagTransformed(rule.FirstTag, rule.SecondTag, rule.ResultTag, source);
                return;
            }
        }

        private int FindTagIndex(CombatTagDefinition tag)
        {
            if (!IsLegalTag(tag))
            {
                return -1;
            }

            for (int i = 0; i < _activeTags.Count; i++)
            {
                if (ReferenceEquals(_activeTags[i].Tag, tag))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsLegalTag(CombatTagDefinition tag)
        {
            return tag != null && tag.IsValid;
        }

        private void NotifyTagAdded(CombatTagDefinition tag, GameObject source)
        {
            LogTagChange("added", tag);
            onTagAdded.Invoke(this, tag);
            CombatEventsBus.RaiseTagAdded(source, gameObject, tag);
        }

        private void NotifyTagRemoved(CombatTagDefinition tag, GameObject source)
        {
            LogTagChange("removed", tag);
            onTagRemoved.Invoke(this, tag);
            CombatEventsBus.RaiseTagRemoved(source, gameObject, tag);
        }

        private void NotifyTagExpired(CombatTagDefinition tag)
        {
            LogTagChange("expired", tag);
            onTagExpired.Invoke(this, tag);
            CombatEventsBus.RaiseTagExpired(gameObject, gameObject, tag);
        }

        private void NotifyTagRefreshed(CombatTagDefinition tag)
        {
            LogTagChange("refreshed", tag);
            onTagRefreshed.Invoke(this, tag);
        }

        private void NotifyTagTransformed(
            CombatTagDefinition firstTag,
            CombatTagDefinition secondTag,
            CombatTagDefinition resultTag,
            GameObject source)
        {
            if (logTagChanges)
            {
                Debug.Log(
                    $"CombatTagContainer transformed {firstTag.TagId} + {secondTag.TagId} => {resultTag.TagId}.",
                    this);
            }

            onTagTransformed.Invoke(this, firstTag, secondTag, resultTag);
            CombatEventsBus.RaiseTagTransformed(source, gameObject, resultTag);
        }

        private void LogInvalidTag(CombatTagDefinition tag)
        {
            if (!logTagChanges)
            {
                return;
            }

            string tagName = tag != null ? tag.name : "null";
            Debug.LogWarning($"CombatTagContainer rejected invalid tag: {tagName}", this);
        }

        private void LogTagChange(string operation, CombatTagDefinition tag)
        {
            if (!logTagChanges)
            {
                return;
            }

            Debug.Log($"CombatTagContainer {operation} tag: {tag.TagId}", this);
        }
    }

    /// <summary>
    /// 激活中的战斗标签。
    /// </summary>
    [System.Serializable]
    public sealed class ActiveCombatTag
    {
        [Tooltip("标签定义。")]
        [SerializeField]
        private CombatTagDefinition tag;

        [Tooltip("剩余持续时间。小于等于 0 表示永久标签。")]
        [SerializeField, Min(0f)]
        private float remainingDuration;

        public ActiveCombatTag(CombatTagDefinition tag, float duration)
        {
            this.tag = tag;
            remainingDuration = Mathf.Max(0f, duration);
        }

        /// <summary>标签定义。</summary>
        public CombatTagDefinition Tag => tag;

        /// <summary>剩余持续时间。</summary>
        public float RemainingDuration => remainingDuration;

        /// <summary>是否有持续时间。</summary>
        public bool HasDuration => remainingDuration > 0f;

        /// <summary>刷新持续时间。duration 小于等于 0 时会变为永久标签。</summary>
        public void Refresh(float duration)
        {
            if (duration <= 0f)
            {
                remainingDuration = 0f;
                return;
            }

            remainingDuration = Mathf.Max(remainingDuration, duration);
        }

        /// <summary>推进持续时间。</summary>
        public void Tick(float deltaTime)
        {
            if (!HasDuration)
            {
                return;
            }

            remainingDuration -= deltaTime;
        }
    }

    /// <summary>
    /// 标签事件。参数依次为：标签容器、标签定义。
    /// </summary>
    [System.Serializable]
    public sealed class CombatTagEvent : UnityEvent<CombatTagContainer, CombatTagDefinition>
    {
    }

    /// <summary>
    /// 标签组合转化事件。参数依次为：标签容器、输入 A、输入 B、输出 C。
    /// </summary>
    [System.Serializable]
    public sealed class CombatTagTransformEvent : UnityEvent<CombatTagContainer, CombatTagDefinition, CombatTagDefinition, CombatTagDefinition>
    {
    }
}
