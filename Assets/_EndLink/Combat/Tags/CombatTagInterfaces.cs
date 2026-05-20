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
    }

    /// <summary>
    /// 战斗标签接收接口。
    /// Hitbox、状态效果、连携规则等系统可以通过该接口给目标添加或移除标签。
    /// </summary>
    public interface ICombatTagReceiver : ICombatTagReadable
    {
        /// <summary>添加永久标签。</summary>
        bool AddTag(CombatTagDefinition tag);

        /// <summary>添加带持续时间的标签。duration 小于等于 0 时视为永久标签。</summary>
        bool AddTag(CombatTagDefinition tag, float duration);

        /// <summary>移除标签。</summary>
        bool RemoveTag(CombatTagDefinition tag);
    }
}
