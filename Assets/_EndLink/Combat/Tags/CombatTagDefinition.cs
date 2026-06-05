using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 战斗标签定义。
    /// 标签只服务战斗系统，用于描述命中状态、连携条件、临时弱点等战斗语义。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CombatTag_",
        menuName = "EndLink/Combat/Combat Tag Definition")]
    public sealed class CombatTagDefinition : ScriptableObject
    {
        [Header("基础信息")]
        [Tooltip("标签唯一标识。建议使用英文小写加点号分层，例如 hit.break 或 element.fire。")]
        [SerializeField]
        private string tagId = "combat.tag";

        [Tooltip("显示名称。主要用于 Inspector、调试面板或后续 UI。")]
        [SerializeField]
        private string displayName = "Combat Tag";

        [TextArea]
        [Tooltip("标签说明。用于记录这个标签在战斗规则中的含义。")]
        [SerializeField]
        private string description;

        [Header("等级")]
        [Tooltip("标签等级。1 表示基础标签，2 及以上通常表示通过反应、组合或特殊规则生成的高级标签。")]
        [SerializeField, Min(1)]
        private int tagLevel = 1;

        [Header("持续时间")]
        [Tooltip("标签默认持续时间。小于等于 0 表示永久标签。Action 或 Hitbox 可以传入持续时间覆盖该默认值。")]
        [SerializeField, Min(0f)]
        private float defaultDuration;

        [Header("层数")]
        [Tooltip("该标签允许叠加的最大层数。1 表示不叠层，只刷新持续时间。")]
        [SerializeField, Min(1)]
        private int maxStackCount = 1;

        /// <summary>标签唯一标识。</summary>
        public string TagId => tagId;

        /// <summary>显示名称。</summary>
        public string DisplayName => displayName;

        /// <summary>标签说明。</summary>
        public string Description => description;

        /// <summary>标签等级。1 表示基础标签，2 及以上通常表示反应结果或高级标签。</summary>
        public int TagLevel => Mathf.Max(1, tagLevel);

        /// <summary>标签默认持续时间。小于等于 0 表示永久标签。</summary>
        public float DefaultDuration => Mathf.Max(0f, defaultDuration);

        /// <summary>该标签允许叠加的最大层数。</summary>
        public int MaxStackCount => Mathf.Max(1, maxStackCount);

        /// <summary>标签是否合法。当前最小规则是资产存在且 tagId 非空。</summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(tagId);

        private void OnValidate()
        {
            tagId = tagId?.Trim();
            tagLevel = Mathf.Max(1, tagLevel);
            defaultDuration = Mathf.Max(0f, defaultDuration);
            maxStackCount = Mathf.Max(1, maxStackCount);
        }
    }
}
