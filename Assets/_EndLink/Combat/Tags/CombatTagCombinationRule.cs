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

        /// <summary>组合输入标签 A。</summary>
        public CombatTagDefinition FirstTag => firstTag;

        /// <summary>组合输入标签 B。</summary>
        public CombatTagDefinition SecondTag => secondTag;

        /// <summary>组合结果标签 C。</summary>
        public CombatTagDefinition ResultTag => resultTag;

        /// <summary>组合成功后是否移除输入标签。</summary>
        public bool RemoveSourceTags => removeSourceTags;

        /// <summary>结果标签持续时间。小于等于 0 表示永久标签。</summary>
        public float ResultDuration => resultDuration;

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

            return (ReferenceEquals(addedTag, firstTag) && readable.HasTag(secondTag))
                || (ReferenceEquals(addedTag, secondTag) && readable.HasTag(firstTag));
        }

        private static bool IsValidTag(CombatTagDefinition tag)
        {
            return tag != null && tag.IsValid;
        }
    }
}
