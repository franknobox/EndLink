using System;
using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 标签反应效果类型。
    /// 第一版只直接执行部分基础效果，其余类型先作为数据接口预留。
    /// </summary>
    public enum CombatTagReactionEffectType
    {
        /// <summary>给当前目标添加一个标签。</summary>
        ApplyTag = 0,

        /// <summary>从当前目标移除一个标签。</summary>
        RemoveTag = 1,

        /// <summary>对当前目标造成一段反应伤害。</summary>
        DealDamage = 2,

        /// <summary>把指定标签扩散到周围目标。具体搜索和扩散规则后续接入。</summary>
        SpreadTag = 3,

        /// <summary>施加控制效果。具体移动、动作锁定规则后续接入。</summary>
        ApplyControl = 4,

        /// <summary>打断当前动作。具体打断规则后续接入状态机或韧性系统。</summary>
        InterruptAction = 5,

        /// <summary>修改战斗资源，例如连携能量、极限技能量等。</summary>
        ModifyResource = 6,

        /// <summary>自定义事件入口，用于后续扩展特殊反应。</summary>
        CustomEvent = 100
    }

    /// <summary>
    /// 标签反应效果配置。
    /// 一个组合规则可以包含多个效果，例如生成新标签、造成伤害、清除源标签和触发扩散。
    /// </summary>
    [Serializable]
    public sealed class CombatTagReactionEffect
    {
        [Tooltip("反应效果类型。决定这条效果被执行时的语义。")]
        [SerializeField]
        private CombatTagReactionEffectType effectType = CombatTagReactionEffectType.ApplyTag;

        [Tooltip("效果关联标签。ApplyTag/RemoveTag/SpreadTag 会使用它；DealDamage 可用它作为伤害事件携带的标签。")]
        [SerializeField]
        private CombatTagDefinition tag;

        [Tooltip("标签持续时间。小于等于 0 时使用标签定义的默认持续时间。")]
        [SerializeField, Min(0f)]
        private float duration;

        [Tooltip("标签层数。ApplyTag/SpreadTag 会使用它。")]
        [SerializeField, Min(1)]
        private int stackCount = 1;

        [Tooltip("伤害数值。DealDamage 会使用它。")]
        [SerializeField, Min(0)]
        private int damageAmount;

        [Tooltip("伤害类型。DealDamage 会使用它；反应伤害默认按运行伤害处理。")]
        [SerializeField]
        private CombatDamageType damageType = CombatDamageType.RuntimeDamage;

        [Tooltip("效果数值。预留给控制强度、资源变化量、易伤倍率等通用参数。")]
        [SerializeField]
        private float value;

        [Tooltip("效果半径。预留给扩散、范围伤害、范围控制等效果。")]
        [SerializeField, Min(0f)]
        private float radius;

        [Tooltip("自定义效果 ID。CustomEvent 或特殊规则可以使用它识别具体效果。")]
        [SerializeField]
        private string effectId;

        /// <summary>反应效果类型。</summary>
        public CombatTagReactionEffectType EffectType => effectType;

        /// <summary>效果关联标签。</summary>
        public CombatTagDefinition Tag => tag;

        /// <summary>标签持续时间。小于等于 0 时使用标签定义的默认持续时间。</summary>
        public float Duration => Mathf.Max(0f, duration);

        /// <summary>标签层数。</summary>
        public int StackCount => Mathf.Max(1, stackCount);

        /// <summary>伤害数值。</summary>
        public int DamageAmount => Mathf.Max(0, damageAmount);

        /// <summary>伤害类型。</summary>
        public CombatDamageType DamageType => damageType;

        /// <summary>通用效果数值。</summary>
        public float Value => value;

        /// <summary>效果半径。</summary>
        public float Radius => Mathf.Max(0f, radius);

        /// <summary>自定义效果 ID。</summary>
        public string EffectId => effectId;

        /// <summary>效果配置是否具备最小合法数据。</summary>
        public bool IsValid
        {
            get
            {
                switch (effectType)
                {
                    case CombatTagReactionEffectType.ApplyTag:
                    case CombatTagReactionEffectType.RemoveTag:
                    case CombatTagReactionEffectType.SpreadTag:
                        return IsValidTag(tag);
                    case CombatTagReactionEffectType.DealDamage:
                        return DamageAmount > 0;
                    case CombatTagReactionEffectType.CustomEvent:
                        return !string.IsNullOrWhiteSpace(effectId);
                    default:
                        return true;
                }
            }
        }

        /// <summary>校正 Inspector 输入。</summary>
        public void Validate()
        {
            duration = Mathf.Max(0f, duration);
            stackCount = Mathf.Max(1, stackCount);
            damageAmount = Mathf.Max(0, damageAmount);
            radius = Mathf.Max(0f, radius);
            effectId = effectId?.Trim();
        }

        private static bool IsValidTag(CombatTagDefinition combatTag)
        {
            return combatTag != null && combatTag.IsValid;
        }
    }
}
