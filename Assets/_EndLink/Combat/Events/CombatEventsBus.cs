using System;
using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 全局战斗事件总线。
    /// 用于广播战斗事件，不保存战斗状态，不决定任何连携规则。
    /// </summary>
    public static class CombatEventsBus
    {
        /// <summary>
        /// 全局战斗事件。
        /// 订阅者需要在 OnEnable/OnDisable 中成对订阅和退订，避免静态事件持有失效对象。
        /// </summary>
        public static event Action<CombatEvent> Raised;

        /// <summary>
        /// 广播任意战斗事件。
        /// </summary>
        public static void Raise(CombatEvent eventData)
        {
            Raised?.Invoke(eventData);
        }

        /// <summary>
        /// 广播战斗动作开始事件。
        /// </summary>
        public static void RaiseActionStarted(
            GameObject source,
            GameObject target,
            CombatActionDefinition actionDefinition)
        {
            Raise(new CombatEvent(
                CombatEventType.ActionStarted,
                source,
                target,
                actionDefinition));
        }

        /// <summary>
        /// 广播 Hitbox 命中事件。
        /// </summary>
        public static void RaiseHitLanded(
            GameObject source,
            GameObject target,
            HitboxHitInfo hitInfo)
        {
            Raise(new CombatEvent(
                CombatEventType.HitLanded,
                source,
                target,
                null,
                hitInfo.CombatTagToApply,
                hitInfo.DamageAmount,
                hitInfo.DamageType,
                hitInfo.CombatTagStackCount,
                hitInfo,
                true));
        }

        /// <summary>
        /// 广播受伤事件。
        /// </summary>
        public static void RaiseDamaged(
            GameObject source,
            GameObject target,
            float damageAmount,
            CombatDamageType damageType,
            CombatTagDefinition combatTag)
        {
            Raise(new CombatEvent(
                CombatEventType.Damaged,
                source,
                target,
                null,
                combatTag,
                damageAmount,
                damageType));
        }

        /// <summary>
        /// 广播死亡事件。
        /// </summary>
        public static void RaiseDead(GameObject source, GameObject target)
        {
            Raise(new CombatEvent(CombatEventType.Dead, source, target));
        }

        /// <summary>
        /// 广播标签添加事件。
        /// </summary>
        public static void RaiseTagAdded(GameObject source, GameObject target, CombatTagDefinition combatTag, int stackCount = 0)
        {
            RaiseTagEvent(CombatEventType.TagAdded, source, target, combatTag, stackCount);
        }

        /// <summary>
        /// 广播标签移除事件。
        /// </summary>
        public static void RaiseTagRemoved(GameObject source, GameObject target, CombatTagDefinition combatTag)
        {
            RaiseTagEvent(CombatEventType.TagRemoved, source, target, combatTag);
        }

        /// <summary>
        /// 广播标签过期事件。
        /// </summary>
        public static void RaiseTagExpired(GameObject source, GameObject target, CombatTagDefinition combatTag)
        {
            RaiseTagEvent(CombatEventType.TagExpired, source, target, combatTag);
        }

        /// <summary>
        /// 广播标签组合转化事件。
        /// 当前携带主要结果标签；如果反应只造成伤害或移除标签，则结果标签可以为空。
        /// </summary>
        public static void RaiseTagTransformed(GameObject source, GameObject target, CombatTagDefinition reactionTag)
        {
            RaiseTagEvent(CombatEventType.TagTransformed, source, target, reactionTag);
        }

        private static void RaiseTagEvent(
            CombatEventType eventType,
            GameObject source,
            GameObject target,
            CombatTagDefinition combatTag,
            int stackCount = 0)
        {
            Raise(new CombatEvent(
                eventType,
                source,
                target,
                null,
                combatTag,
                0f,
                CombatDamageType.StructuralDamage,
                stackCount));
        }
    }
}
