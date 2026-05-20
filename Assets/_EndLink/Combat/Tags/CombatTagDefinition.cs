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

        /// <summary>标签唯一标识。</summary>
        public string TagId => tagId;

        /// <summary>显示名称。</summary>
        public string DisplayName => displayName;

        /// <summary>标签说明。</summary>
        public string Description => description;

        /// <summary>标签是否合法。当前最小规则是资产存在且 tagId 非空。</summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(tagId);

        private void OnValidate()
        {
            tagId = tagId?.Trim();
        }
    }
}
