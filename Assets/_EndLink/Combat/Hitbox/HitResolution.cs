namespace EndLink.Combat
{
    /// <summary>
    /// 一次 Hitbox 接触经过受击方规则处理后的最终结果。
    /// 结果描述命中是否真正成立，不等同于伤害数值是否大于 0。
    /// </summary>
    public enum HitOutcome
    {
        /// <summary>目标拒绝处理本次命中，例如目标已经死亡。</summary>
        Rejected = 0,

        /// <summary>命中正常生效，可以造成生命、平衡、击退或其他普通受击效果。</summary>
        Applied = 1,

        /// <summary>命中被格挡，但仍视为接触目标并可保留格挡伤害或标签。</summary>
        Blocked = 2,

        /// <summary>命中被弹反，不执行普通命中反馈和标签附加。</summary>
        Parried = 3,

        /// <summary>命中被闪避或临时无敌帧规避。</summary>
        Dodged = 4,

        /// <summary>目标接受了命中判断，但对本次伤害或效果免疫。</summary>
        Immune = 5
    }

    /// <summary>
    /// 受击方返回给 Hitbox 的最小结算结果。
    /// Hitbox 根据它决定事件、普通反馈和战斗标签是否继续执行。
    /// </summary>
    public readonly struct HitResolution
    {
        public HitResolution(HitOutcome outcome, int appliedDamage = 0)
        {
            Outcome = outcome;
            AppliedDamage = appliedDamage > 0 ? appliedDamage : 0;
        }

        /// <summary>本次命中的最终分类。</summary>
        public HitOutcome Outcome { get; }

        /// <summary>目标生命值实际减少量，已经包含减伤和剩余生命上限。</summary>
        public int AppliedDamage { get; }

        /// <summary>是否应继续广播兼容用的 HitLanded 事件。</summary>
        public bool CountsAsHitLanded => Outcome == HitOutcome.Applied
            || Outcome == HitOutcome.Blocked
            || Outcome == HitOutcome.Parried
            || Outcome == HitOutcome.Immune;

        /// <summary>是否播放 Action 配置的普通命中反馈。</summary>
        public bool AllowsNormalFeedback => Outcome == HitOutcome.Applied;

        /// <summary>是否允许 Hitbox 附加战斗标签。</summary>
        public bool AllowsCombatTag => Outcome == HitOutcome.Applied
            || Outcome == HitOutcome.Blocked;
    }
}
