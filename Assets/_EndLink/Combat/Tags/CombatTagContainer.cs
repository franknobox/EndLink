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
        [Tooltip("物体启用时自动拥有的初始标签。持续时间小于等于 0 表示使用标签默认持续时间。")]
        [SerializeField]
        private List<ActiveCombatTag> initialTags = new();

        [Header("组合规则")]
        [Tooltip("标签组合规则。添加标签后会尝试匹配 A+B 并触发反应效果。")]
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

                AddTag(initialTag.Tag, initialTag.HasDuration ? initialTag.RemainingDuration : 0f, gameObject, initialTag.StackCount);
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

        /// <summary>按标签定义的默认持续时间添加标签。</summary>
        public bool AddTag(CombatTagDefinition tag)
        {
            return AddTag(tag, 0f, gameObject);
        }

        /// <summary>按标签定义的默认持续时间添加标签，并记录标签来源。</summary>
        public bool AddTag(CombatTagDefinition tag, GameObject source)
        {
            return AddTag(tag, 0f, source);
        }

        /// <summary>添加标签。duration 小于等于 0 时使用标签定义的默认持续时间。</summary>
        public bool AddTag(CombatTagDefinition tag, float duration)
        {
            return AddTag(tag, duration, gameObject);
        }

        /// <summary>添加标签并记录标签来源。duration 小于等于 0 时使用标签定义的默认持续时间。</summary>
        public bool AddTag(CombatTagDefinition tag, float duration, GameObject source)
        {
            return AddTag(tag, duration, source, 1);
        }

        /// <summary>添加标签、持续时间和层数，并记录标签来源。duration 小于等于 0 时使用标签定义的默认持续时间。</summary>
        public bool AddTag(CombatTagDefinition tag, float duration, GameObject source, int stackCount)
        {
            return AddTagInternal(tag, duration, source, stackCount, true, 0);
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

        /// <summary>尝试读取标签当前层数。</summary>
        public bool TryGetStackCount(CombatTagDefinition tag, out int stackCount)
        {
            stackCount = 0;

            int index = FindTagIndex(tag);
            if (index < 0)
            {
                return false;
            }

            stackCount = _activeTags[index].StackCount;
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
            int stackCount,
            bool evaluateCombinations,
            int combinationDepth)
        {
            if (!IsLegalTag(tag))
            {
                LogInvalidTag(tag);
                return false;
            }

            float resolvedDuration = ResolveDuration(tag, duration);
            int index = FindTagIndex(tag);

            if (index >= 0)
            {
                _activeTags[index].Refresh(resolvedDuration, stackCount, tag.MaxStackCount);
                NotifyTagRefreshed(tag);
            }
            else
            {
                ActiveCombatTag activeTag = new ActiveCombatTag(tag, resolvedDuration, stackCount);
                _activeTags.Add(activeTag);
                NotifyTagAdded(tag, source, activeTag.StackCount);
            }

            if (evaluateCombinations && combinationDepth < MaxCombinationDepth)
            {
                TryApplyCombinationRules(tag, source, combinationDepth);
            }

            return true;
        }

        private static float ResolveDuration(CombatTagDefinition tag, float duration)
        {
            if (duration > 0f)
            {
                return duration;
            }

            return tag != null ? tag.DefaultDuration : 0f;
        }

        private void TryApplyCombinationRules(CombatTagDefinition addedTag, GameObject source, int combinationDepth)
        {
            foreach (CombatTagCombinationRule rule in combinationRules)
            {
                if (rule == null || !rule.CanApply(addedTag, this))
                {
                    continue;
                }

                CombatTagDefinition reactionTag = ExecuteReactionRuleEffects(rule, source, combinationDepth);
                NotifyTagTransformed(rule.FirstTag, rule.SecondTag, reactionTag, source);
                return;
            }
        }

        private CombatTagDefinition ExecuteReactionRuleEffects(
            CombatTagCombinationRule rule,
            GameObject source,
            int combinationDepth)
        {
            CombatTagDefinition reactionTag = null;

            foreach (CombatTagReactionEffect effect in rule.ReactionEffects)
            {
                if (effect == null || !effect.IsValid)
                {
                    continue;
                }

                CombatTagDefinition effectTag = ExecuteReactionEffect(effect, source, combinationDepth);
                if (reactionTag == null && effectTag != null)
                {
                    reactionTag = effectTag;
                }
            }

            return reactionTag;
        }

        private CombatTagDefinition ExecuteReactionEffect(
            CombatTagReactionEffect effect,
            GameObject source,
            int combinationDepth)
        {
            switch (effect.EffectType)
            {
                case CombatTagReactionEffectType.ApplyTag:
                    AddTagInternal(effect.Tag, effect.Duration, source, effect.StackCount, true, combinationDepth + 1);
                    return effect.Tag;
                case CombatTagReactionEffectType.RemoveTag:
                    RemoveTag(effect.Tag, source);
                    return null;
                case CombatTagReactionEffectType.DealDamage:
                    ApplyReactionDamage(effect, source);
                    return effect.Tag;
                case CombatTagReactionEffectType.SpreadTag:
                case CombatTagReactionEffectType.ApplyControl:
                case CombatTagReactionEffectType.InterruptAction:
                case CombatTagReactionEffectType.ModifyResource:
                case CombatTagReactionEffectType.CustomEvent:
                    LogUnsupportedReactionEffect(effect);
                    return effect.Tag;
                default:
                    return null;
            }
        }

        private void ApplyReactionDamage(CombatTagReactionEffect effect, GameObject source)
        {
            if (effect.DamageAmount <= 0)
            {
                return;
            }

            CharacterHealth characterHealth = GetComponentInParent<CharacterHealth>();
            if (characterHealth != null)
            {
                characterHealth.ApplyDamage(effect.DamageAmount, effect.DamageType, effect.Tag, source);
                return;
            }

            IDamageable damageable = GetComponentInParent<IDamageable>();
            damageable?.TakeDamage(effect.DamageAmount, effect.DamageType, effect.Tag);
        }

        private void LogUnsupportedReactionEffect(CombatTagReactionEffect effect)
        {
            if (!logTagChanges)
            {
                return;
            }

            Debug.Log(
                $"CombatTagContainer reaction effect is reserved but not implemented: {effect.EffectType}.",
                this);
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

        private void NotifyTagAdded(CombatTagDefinition tag, GameObject source, int stackCount)
        {
            LogTagChange("added", tag);
            onTagAdded.Invoke(this, tag);
            CombatEventsBus.RaiseTagAdded(source, gameObject, tag, stackCount);
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
            CombatTagDefinition reactionTag,
            GameObject source)
        {
            if (logTagChanges)
            {
                string reactionTagId = reactionTag != null ? reactionTag.TagId : "None";
                Debug.Log(
                    $"CombatTagContainer transformed {firstTag.TagId} + {secondTag.TagId} => {reactionTagId}.",
                    this);
            }

            onTagTransformed.Invoke(this, firstTag, secondTag, reactionTag);
            CombatEventsBus.RaiseTagTransformed(source, gameObject, reactionTag);
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

        [Tooltip("剩余持续时间。作为初始标签配置时，小于等于 0 表示使用标签默认持续时间。运行时小于等于 0 表示永久标签。")]
        [SerializeField, Min(0f)]
        private float remainingDuration;

        [Tooltip("当前标签层数。会被标签定义的最大层数钳制。")]
        [SerializeField, Min(1)]
        private int stackCount = 1;

        public ActiveCombatTag(CombatTagDefinition tag, float duration)
            : this(tag, duration, 1)
        {
        }

        public ActiveCombatTag(CombatTagDefinition tag, float duration, int stackCount)
        {
            this.tag = tag;
            remainingDuration = Mathf.Max(0f, duration);
            this.stackCount = Mathf.Clamp(stackCount, 1, tag != null ? tag.MaxStackCount : 1);
        }

        /// <summary>标签定义。</summary>
        public CombatTagDefinition Tag => tag;

        /// <summary>剩余持续时间。</summary>
        public float RemainingDuration => remainingDuration;

        /// <summary>当前标签层数。</summary>
        public int StackCount => Mathf.Max(1, stackCount);

        /// <summary>是否有持续时间。</summary>
        public bool HasDuration => remainingDuration > 0f;

        /// <summary>刷新持续时间。duration 小于等于 0 时会变为永久标签。</summary>
        public void Refresh(float duration)
        {
            Refresh(duration, 1, tag != null ? tag.MaxStackCount : 1);
        }

        /// <summary>刷新持续时间并增加层数。</summary>
        public void Refresh(float duration, int addedStackCount, int maxStackCount)
        {
            if (duration <= 0f)
            {
                remainingDuration = 0f;
            }
            else
            {
                remainingDuration = Mathf.Max(remainingDuration, duration);
            }

            stackCount = Mathf.Clamp(StackCount + Mathf.Max(1, addedStackCount), 1, Mathf.Max(1, maxStackCount));
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
    /// 标签组合转化事件。参数依次为：标签容器、输入 A、输入 B、主要反应标签。
    /// </summary>
    [System.Serializable]
    public sealed class CombatTagTransformEvent : UnityEvent<CombatTagContainer, CombatTagDefinition, CombatTagDefinition, CombatTagDefinition>
    {
    }
}
