using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 战斗伤害类型。
    /// 当前只作为伤害管线的数据通道，具体防御、抗性和公式修正后续由 DamageCalculator 处理。
    /// </summary>
    public enum CombatDamageType
    {
        /// <summary>结构伤害。偏物理、近战、冲击、武器和结构破坏。</summary>
        StructuralDamage = 0,

        /// <summary>运行伤害。偏协议、能量、异常数据、标签反应和系统侵蚀。</summary>
        RuntimeDamage = 1
    }

    /// <summary>
    /// 一次伤害计算的输入上下文。
    /// Hitbox、标签反应、环境伤害等入口都应先转成该上下文，再交给 DamageCalculator。
    /// </summary>
    public readonly struct DamageContext
    {
        public DamageContext(
            GameObject source,
            GameObject target,
            CombatActionDefinition actionDefinition,
            CharacterStats sourceStats,
            HitboxHitInfo hitInfo,
            bool hasHitInfo,
            float baseDamage,
            CombatDamageType damageType,
            CombatTagDefinition combatTag,
            Vector3 hitPoint,
            Vector3 hitDirection)
        {
            Source = source;
            Target = target;
            ActionDefinition = actionDefinition;
            SourceStats = sourceStats;
            HitInfo = hitInfo;
            HasHitInfo = hasHitInfo;
            BaseDamage = Mathf.Max(0f, baseDamage);
            DamageType = damageType;
            CombatTag = combatTag;
            HitPoint = hitPoint;
            HitDirection = NormalizePlanarDirection(hitDirection);
        }

        /// <summary>伤害来源，通常是攻击者或触发反应的系统对象。</summary>
        public GameObject Source { get; }

        /// <summary>伤害目标。</summary>
        public GameObject Target { get; }

        /// <summary>关联动作配置。标签反应或环境伤害可以为空。</summary>
        public CombatActionDefinition ActionDefinition { get; }

        /// <summary>攻击来源的数值组件。为空时 DamageCalculator 会按兼容路径回退查找。</summary>
        public CharacterStats SourceStats { get; }

        /// <summary>关联 Hitbox 命中信息。</summary>
        public HitboxHitInfo HitInfo { get; }

        /// <summary>当前上下文是否携带有效 Hitbox 命中信息。</summary>
        public bool HasHitInfo { get; }

        /// <summary>
        /// 伤害上下文携带的固定伤害部分。
        /// 如果关联动作配置，DamageCalculator 还会叠加攻击者数值与动作倍率。
        /// </summary>
        public float BaseDamage { get; }

        /// <summary>伤害类型。</summary>
        public CombatDamageType DamageType { get; }

        /// <summary>关联战斗标签。</summary>
        public CombatTagDefinition CombatTag { get; }

        /// <summary>命中点或伤害发生点。</summary>
        public Vector3 HitPoint { get; }

        /// <summary>从来源指向目标的水平伤害方向。</summary>
        public Vector3 HitDirection { get; }

        /// <summary>由 Hitbox 命中信息构造伤害上下文。</summary>
        public static DamageContext FromHit(HitboxHitInfo hitInfo, GameObject target)
        {
            return new DamageContext(
                hitInfo.Owner,
                target,
                hitInfo.ActionDefinition,
                hitInfo.SourceStats,
                hitInfo,
                true,
                hitInfo.DamageAmount,
                hitInfo.DamageType,
                hitInfo.CombatTagToApply,
                hitInfo.HitPoint,
                hitInfo.HitDirection);
        }

        /// <summary>构造非 Hitbox 来源的直接伤害上下文。</summary>
        public static DamageContext Direct(
            GameObject source,
            GameObject target,
            float baseDamage,
            CombatDamageType damageType,
            CombatTagDefinition combatTag)
        {
            Vector3 hitDirection = Vector3.zero;
            if (source != null && target != null)
            {
                hitDirection = target.transform.position - source.transform.position;
            }

            Vector3 hitPoint = target != null ? target.transform.position : Vector3.zero;

            return new DamageContext(
                source,
                target,
                null,
                null,
                default,
                false,
                baseDamage,
                damageType,
                combatTag,
                hitPoint,
                hitDirection);
        }

        private static Vector3 NormalizePlanarDirection(Vector3 direction)
        {
            Vector3 planarDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            return planarDirection.sqrMagnitude > 0.0001f
                ? planarDirection.normalized
                : Vector3.zero;
        }
    }
}
