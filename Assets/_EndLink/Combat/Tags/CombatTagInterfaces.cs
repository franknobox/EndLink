namespace EndLink.Combat
{
    /// <summary>
    /// 战斗标签只读查询接口。
    /// 连携规则、AI、UI 等系统应优先依赖这个接口读取标签，而不是直接操作容器内部列表。
    /// </summary>
    public interface ICombatTagReadable
    {
        /// <summary>是否拥有指定标签。</summary>
        bool HasTag(CombatTagDefinition tag);

        /// <summary>尝试读取指定标签的剩余时间。永久标签返回 false。</summary>
        bool TryGetRemainingDuration(CombatTagDefinition tag, out float remainingDuration);

        /// <summary>尝试读取指定标签当前层数。</summary>
        bool TryGetStackCount(CombatTagDefinition tag, out int stackCount);
    }

    /// <summary>
    /// 战斗标签接收接口。
    /// Hitbox、状态效果、连携规则等系统可以通过该接口给目标添加或移除标签。
    /// </summary>
    public interface ICombatTagReceiver : ICombatTagReadable
    {
        /// <summary>按标签定义的默认持续时间添加标签。</summary>
        bool AddTag(CombatTagDefinition tag);

        /// <summary>按标签定义的默认持续时间添加标签，并记录标签来源。source 通常是施加该标签的攻击者或系统对象。</summary>
        bool AddTag(CombatTagDefinition tag, UnityEngine.GameObject source);

        /// <summary>添加带持续时间的标签。duration 小于等于 0 时使用标签定义的默认持续时间。</summary>
        bool AddTag(CombatTagDefinition tag, float duration);

        /// <summary>添加带持续时间的标签，并记录标签来源。duration 小于等于 0 时使用标签定义的默认持续时间。</summary>
        bool AddTag(CombatTagDefinition tag, float duration, UnityEngine.GameObject source);

        /// <summary>添加带持续时间和层数的标签，并记录标签来源。</summary>
        bool AddTag(CombatTagDefinition tag, float duration, UnityEngine.GameObject source, int stackCount);

        /// <summary>移除标签。</summary>
        bool RemoveTag(CombatTagDefinition tag);

        /// <summary>移除标签，并记录移除来源。source 通常是触发移除的攻击者、规则或系统对象。</summary>
        bool RemoveTag(CombatTagDefinition tag, UnityEngine.GameObject source);
    }
}
