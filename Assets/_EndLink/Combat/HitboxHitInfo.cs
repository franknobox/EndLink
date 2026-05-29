using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 一次 Hitbox 命中的完整上下文。
    /// 后续伤害系统、标签系统、击退、受击表现都应优先从这里取数据。
    /// </summary>
    public readonly struct HitboxHitInfo
    {
        public HitboxHitInfo(
            HitboxBase hitbox,
            GameObject owner,
            Collider hitCollider,
            float damageAmount,
            float knockbackForce,
            CombatTagDefinition combatTagToApply,
            float combatTagDuration,
            int combatTagStackCount,
            Vector3 hitPoint,
            Vector3 hitDirection)
        {
            Hitbox = hitbox;
            Owner = owner;
            HitCollider = hitCollider;
            DamageAmount = damageAmount;
            KnockbackForce = knockbackForce;
            CombatTagToApply = combatTagToApply;
            CombatTagDuration = Mathf.Max(0f, combatTagDuration);
            CombatTagStackCount = Mathf.Max(1, combatTagStackCount);
            HitPoint = hitPoint;
            HitDirection = hitDirection;
        }

        /// <summary>产生本次命中的 Hitbox。</summary>
        public HitboxBase Hitbox { get; }

        /// <summary>Hitbox 所属对象，通常是攻击者。</summary>
        public GameObject Owner { get; }

        /// <summary>被命中的 Collider。</summary>
        public Collider HitCollider { get; }

        /// <summary>伤害值。</summary>
        public float DamageAmount { get; }

        /// <summary>击退力。</summary>
        public float KnockbackForce { get; }

        /// <summary>命中时附加的战斗标签资产。</summary>
        public CombatTagDefinition CombatTagToApply { get; }

        /// <summary>战斗标签持续时间。小于等于 0 表示永久标签。</summary>
        public float CombatTagDuration { get; }

        /// <summary>命中时附加的战斗标签层数。</summary>
        public int CombatTagStackCount { get; }

        /// <summary>命中点。</summary>
        public Vector3 HitPoint { get; }

        /// <summary>从攻击者指向目标的水平命中方向。</summary>
        public Vector3 HitDirection { get; }
    }
}
