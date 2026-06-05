using System.Collections.Generic;
using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 战斗标签组合规则。
    /// 用于表达 A + B 触发一组反应效果，例如生成标签、造成伤害、清除标签或预留扩散效果。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CombatTagCombination_",
        menuName = "EndLink/Combat/Combat Tag Combination Rule")]
    public sealed class CombatTagCombinationRule : ScriptableObject
    {
        [Header("输入标签")]
        [Tooltip("组合输入标签 A。")]
        [SerializeField]
        private CombatTagDefinition firstTag;

        [Tooltip("组合输入标签 B。")]
        [SerializeField]
        private CombatTagDefinition secondTag;

        [Tooltip("触发规则所需的输入标签 A 层数。")]
        [SerializeField, Min(1)]
        private int requiredFirstStack = 1;

        [Tooltip("触发规则所需的输入标签 B 层数。")]
        [SerializeField, Min(1)]
        private int requiredSecondStack = 1;

        [Header("反应效果")]
        [Tooltip("组合成功后执行的反应效果列表。至少需要配置一条有效效果。")]
        [SerializeField]
        private List<CombatTagReactionEffect> reactionEffects = new();

        /// <summary>组合输入标签 A。</summary>
        public CombatTagDefinition FirstTag => firstTag;

        /// <summary>组合输入标签 B。</summary>
        public CombatTagDefinition SecondTag => secondTag;

        /// <summary>触发规则所需的输入标签 A 层数。</summary>
        public int RequiredFirstStack => Mathf.Max(1, requiredFirstStack);

        /// <summary>触发规则所需的输入标签 B 层数。</summary>
        public int RequiredSecondStack => Mathf.Max(1, requiredSecondStack);

        /// <summary>组合成功后执行的反应效果列表。</summary>
        public IReadOnlyList<CombatTagReactionEffect> ReactionEffects => reactionEffects;

        /// <summary>规则是否合法。</summary>
        public bool IsValid => IsValidTag(firstTag) && IsValidTag(secondTag) && HasValidReactionEffect();

        /// <summary>
        /// 判断新加入的标签是否能和容器已有标签触发本规则。
        /// </summary>
        public bool CanApply(CombatTagDefinition addedTag, ICombatTagReadable readable)
        {
            if (!IsValid || addedTag == null || readable == null)
            {
                return false;
            }

            bool addedTagMatches = ReferenceEquals(addedTag, firstTag) || ReferenceEquals(addedTag, secondTag);
            return addedTagMatches && HasRequiredStacks(readable);
        }

        private bool HasRequiredStacks(ICombatTagReadable readable)
        {
            if (ReferenceEquals(firstTag, secondTag))
            {
                return readable.TryGetStackCount(firstTag, out int sameTagStack)
                    && sameTagStack >= Mathf.Max(RequiredFirstStack, RequiredSecondStack);
            }

            return readable.TryGetStackCount(firstTag, out int firstStack)
                && readable.TryGetStackCount(secondTag, out int secondStack)
                && firstStack >= RequiredFirstStack
                && secondStack >= RequiredSecondStack;
        }

        private void OnValidate()
        {
            requiredFirstStack = Mathf.Max(1, requiredFirstStack);
            requiredSecondStack = Mathf.Max(1, requiredSecondStack);

            if (reactionEffects == null)
            {
                return;
            }

            foreach (CombatTagReactionEffect effect in reactionEffects)
            {
                effect?.Validate();
            }
        }

        private bool HasValidReactionEffect()
        {
            if (reactionEffects == null || reactionEffects.Count <= 0)
            {
                return false;
            }

            foreach (CombatTagReactionEffect effect in reactionEffects)
            {
                if (effect != null && effect.IsValid)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsValidTag(CombatTagDefinition tag)
        {
            return tag != null && tag.IsValid;
        }
    }
}
