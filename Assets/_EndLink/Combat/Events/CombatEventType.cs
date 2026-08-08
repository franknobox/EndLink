namespace EndLink.Combat
{
    /// <summary>
    /// 战斗事件类型。
    /// 这里描述“发生了什么”，不描述由谁处理。具体响应由订阅者决定。
    /// </summary>
    public enum CombatEventType
    {
        /// <summary>战斗动作开始，例如普攻、技能、连携攻击或终链奥义开始释放。</summary>
        ActionStarted = 0,

        /// <summary>Hitbox 成功命中目标。</summary>
        HitLanded = 1,

        /// <summary>目标受到有效伤害。</summary>
        Damaged = 2,

        /// <summary>目标死亡。</summary>
        Dead = 3,

        /// <summary>目标获得战斗标签。</summary>
        TagAdded = 4,

        /// <summary>目标移除战斗标签。</summary>
        TagRemoved = 5,

        /// <summary>目标的限时战斗标签过期。</summary>
        TagExpired = 6,

        /// <summary>协议反应成功触发，例如 A + B 执行一组反应效果。</summary>
        ReactionTriggered = 7,

        /// <summary>Hitbox 接触经过受击方规则处理后得到最终命中结果。</summary>
        HitResolved = 8
    }
}
