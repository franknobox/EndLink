using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 战斗事件数据。
    /// 尽量保持扁平和通用，方便日志、连携规则、队友 AI 和 UI 订阅同一条事件流。
    /// </summary>
    public readonly struct CombatEvent
    {
        public CombatEvent(
            CombatEventType eventType,
            GameObject source = null,
            GameObject target = null,
            CombatActionDefinition actionDefinition = null,
            CombatTagDefinition combatTag = null,
            float damageAmount = 0f,
            HitboxHitInfo hitInfo = default,
            bool hasHitInfo = false)
        {
            EventType = eventType;
            Source = source;
            Target = target;
            ActionDefinition = actionDefinition;
            CombatTag = combatTag;
            DamageAmount = Mathf.Max(0f, damageAmount);
            HitInfo = hitInfo;
            HasHitInfo = hasHitInfo;
            TimeStamp = Time.time;
        }

        /// <summary>事件类型。</summary>
        public CombatEventType EventType { get; }

        /// <summary>事件来源。通常是攻击者、标签来源或触发者。</summary>
        public GameObject Source { get; }

        /// <summary>事件目标。通常是被命中、受伤、死亡或获得标签的对象。</summary>
        public GameObject Target { get; }

        /// <summary>关联的战斗动作配置。</summary>
        public CombatActionDefinition ActionDefinition { get; }

        /// <summary>关联的战斗标签。</summary>
        public CombatTagDefinition CombatTag { get; }

        /// <summary>伤害值。非伤害事件可为 0。</summary>
        public float DamageAmount { get; }

        /// <summary>关联的 Hitbox 命中信息。</summary>
        public HitboxHitInfo HitInfo { get; }

        /// <summary>当前事件是否携带有效 Hitbox 命中信息。</summary>
        public bool HasHitInfo { get; }

        /// <summary>事件发出时的 Time.time。</summary>
        public float TimeStamp { get; }
    }
}
