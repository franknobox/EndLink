using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 一次伤害计算的输出结果。
    /// CharacterHealth 只消费该结果，不直接关心伤害公式如何产生。
    /// </summary>
    public readonly struct DamageResult
    {
        public DamageResult(
            DamageContext context,
            int finalDamage,
            bool isCritical = false,
            bool wasBlocked = false,
            bool wasDodged = false)
        {
            Context = context;
            Source = context.Source;
            Target = context.Target;
            FinalDamage = Mathf.Max(0, finalDamage);
            DamageType = context.DamageType;
            CombatTag = context.CombatTag;
            IsCritical = isCritical;
            WasBlocked = wasBlocked;
            WasDodged = wasDodged;
            HitPoint = context.HitPoint;
            HitDirection = context.HitDirection;
        }

        /// <summary>原始伤害上下文。</summary>
        public DamageContext Context { get; }

        /// <summary>伤害来源。</summary>
        public GameObject Source { get; }

        /// <summary>伤害目标。</summary>
        public GameObject Target { get; }

        /// <summary>最终伤害值。</summary>
        public int FinalDamage { get; }

        /// <summary>伤害类型。</summary>
        public CombatDamageType DamageType { get; }

        /// <summary>关联战斗标签。</summary>
        public CombatTagDefinition CombatTag { get; }

        /// <summary>是否暴击。当前仅预留，不参与计算。</summary>
        public bool IsCritical { get; }

        /// <summary>是否被格挡。当前仅预留，不参与计算。</summary>
        public bool WasBlocked { get; }

        /// <summary>是否被闪避。当前仅预留，不参与计算。</summary>
        public bool WasDodged { get; }

        /// <summary>命中点或伤害发生点。</summary>
        public Vector3 HitPoint { get; }

        /// <summary>从来源指向目标的水平伤害方向。</summary>
        public Vector3 HitDirection { get; }

        /// <summary>是否有有效伤害。</summary>
        public bool HasDamage => FinalDamage > 0 && !WasDodged;
    }
}
