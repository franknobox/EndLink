using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 战斗标签组合规则。
    /// 用于表达 A + B => C，例如后续可配置 Break + Fire => BurnBreak 一类规则。
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

        [Header("输出标签")]
        [Tooltip("组合结果标签 C。")]
        [SerializeField]
        private CombatTagDefinition resultTag;

        [Tooltip("组合成功后是否移除输入标签 A 和 B。")]
        [SerializeField]
        private bool removeSourceTags = true;

        [Tooltip("结果标签持续时间。小于等于 0 表示永久标签。")]
        [SerializeField, Min(0f)]
        private float resultDuration;

        [Tooltip("组合成功后给结果标签增加的层数。")]
        [SerializeField, Min(1)]
        private int resultStackCount = 1;

        /// <summary>组合输入标签 A。</summary>
        public CombatTagDefinition FirstTag => firstTag;

        /// <summary>组合输入标签 B。</summary>
        public CombatTagDefinition SecondTag => secondTag;

        /// <summary>触发规则所需的输入标签 A 层数。</summary>
        public int RequiredFirstStack => Mathf.Max(1, requiredFirstStack);

        /// <summary>触发规则所需的输入标签 B 层数。</summary>
        public int RequiredSecondStack => Mathf.Max(1, requiredSecondStack);

        /// <summary>组合结果标签 C。</summary>
        public CombatTagDefinition ResultTag => resultTag;

        /// <summary>组合成功后是否移除输入标签。</summary>
        public bool RemoveSourceTags => removeSourceTags;

        /// <summary>结果标签持续时间。小于等于 0 表示永久标签。</summary>
        public float ResultDuration => resultDuration;

        /// <summary>组合成功后给结果标签增加的层数。</summary>
        public int ResultStackCount => Mathf.Max(1, resultStackCount);

        /// <summary>规则是否合法。</summary>
        public bool IsValid => IsValidTag(firstTag) && IsValidTag(secondTag) && IsValidTag(resultTag);

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
            resultDuration = Mathf.Max(0f, resultDuration);
            resultStackCount = Mathf.Max(1, resultStackCount);
        }

        private static bool IsValidTag(CombatTagDefinition tag)
        {
            return tag != null && tag.IsValid;
        }
    }
}
