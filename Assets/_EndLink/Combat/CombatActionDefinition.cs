using EndLink.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace EndLink.Combat
{
    /// <summary>
    /// 战斗动作类型。
    /// 描述动作本身的性质，不描述释放者来源。
    /// </summary>
    public enum CombatActionType
    {
        /// <summary>普通攻击。通常高频、低成本，用于基础输出和标签验证。</summary>
        BasicAttack = 0,

        /// <summary>普通技能。通常由玩家输入或 AI 决策释放，有冷却、时序和特殊效果。</summary>
        Skill = 1,

        /// <summary>连携攻击。必须由标签、事件或连携规则打开合法窗口后释放。</summary>
        LinkAttack = 2,

        /// <summary>终链奥义。通常消耗全队协同率或满足特殊条件，具体表现由奥义系统接入。</summary>
        Ultimate = 3
    }

    /// <summary>
    /// 战斗动作配置。
    /// 用 ScriptableObject 描述一次普通攻击、技能、连携技或终链奥义所需的基础数据。
    /// 当前第一版已接入 startup / active / recovery：
    /// startup 控制动作效果何时真正生效，active 会作为标准近战/驻留 Hitbox 的运行时有效段生命周期，
    /// projectile 等特殊类型仍可保留自己的寿命规则；recovery 控制动作结束前的剩余锁定时间。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CombatAction_",
        menuName = "EndLink/Combat/Combat Action Definition")]
    public sealed class CombatActionDefinition : ScriptableObject
    {
        /// <summary>
        /// 动作资产没有写入有效攻击距离时使用的默认值。
        /// 当前近战波默认生成在前方 1 米，1.2 米可以让 AI 停在能覆盖到目标表面的距离。
        /// </summary>
        public const float DefaultEffectiveAttackRange = 1.2f;

        [Header("基础信息")]
        [Tooltip("动作唯一标识。建议使用英文小写加下划线，例如 player_basic_attack_01。")]
        [SerializeField]
        private string actionId = "new_combat_action";

        [Tooltip("显示名称。主要用于 Inspector、调试面板和战斗 UI。")]
        [SerializeField]
        private string displayName = "New Combat Action";

        [Tooltip("动作类型。描述动作性质，不描述释放者来源。")]
        [SerializeField]
        private CombatActionType actionType = CombatActionType.BasicAttack;

        [Header("伤害与标签")]
        [Tooltip("动作直接附加的固定伤害。最终基础伤害 = Flat Damage + 释放者攻击力 × Atk Power Multiplier。")]
        [FormerlySerializedAs("damageAmount")]
        [SerializeField, Min(0f)]
        private float flatDamage = 10f;

        [Tooltip("释放者攻击力倍率。设为 0 表示该动作只使用固定伤害。")]
        [SerializeField, Min(0f)]
        private float atkPowerMultiplier;

        [Tooltip("动作伤害类型。结构伤害偏物理/武器，运行伤害偏协议/能量/异常数据。")]
        [SerializeField]
        private CombatDamageType damageType = CombatDamageType.StructuralDamage;

        [Tooltip("动作命中时附带的基础瞬时击退距离。最终位移还会乘以受击者 CharacterStats 的承受击退倍率。")]
        [SerializeField, Min(0f)]
        private float knockbackForce = 3f;

        [Tooltip("动作命中时施加的战斗标签资产。")]
        [SerializeField]
        private CombatTagDefinition combatTagToApply;

        [Tooltip("战斗标签持续时间。小于等于 0 表示使用标签定义的默认持续时间。")]
        [SerializeField, Min(0f)]
        private float combatTagDuration;

        [Tooltip("动作命中时施加的战斗标签层数。最终会被标签定义的最大层数钳制。")]
        [SerializeField, Min(1)]
        private int combatTagStackCount = 1;

        [Header("连携与奥义")]
        [Tooltip("该动作作为连携技成功释放时，为全队协同率增加的数值。只有 LinkAttack 类型会被小队奥义上下文读取。")]
        [SerializeField, Min(0f)]
        private float synergyGainOnLink;

        [Header("冷却与时序")]
        [Tooltip("动作时序来源。DataDriven 使用动作数据中的前摇/有效/后摇；AnimationEventDriven 预留给后续动画事件驱动。当前 Driver 尚未接入动画事件模式。")]
        [SerializeField]
        private CombatActionTimingSource timingSource = CombatActionTimingSource.DataDriven;

        [Tooltip("动作冷却时间。冷却未结束时不应再次释放同一个动作。")]
        [SerializeField, Min(0f)]
        private float cooldown = 0.45f;

        [Tooltip("前摇时间。表示输入成立后到命中判定出现前的时间。")]
        [SerializeField, Min(0f)]
        private float startupTime = 0.1f;

        [Tooltip("有效时间。第一版会在运行时覆盖标准近战/驻留 Hitbox 的本次生命周期，用来表达判定持续段；Projectile 等特殊类型可保留自身寿命规则。")]
        [SerializeField, Min(0.01f)]
        private float activeTime = 0.2f;

        [Tooltip("后摇时间。表示命中判定结束后到动作完全结束的时间。")]
        [SerializeField, Min(0f)]
        private float recoveryTime = 0.15f;

        [Header("Hitbox")]
        [Tooltip("动作释放时生成的 Hitbox 预制体。")]
        [SerializeField]
        private GameObject hitboxPrefab;

        [Tooltip("Hitbox 生成在释放者正前方的距离。角色本地 Z 轴正方向视为前方。")]
        [SerializeField, Min(0f)]
        private float hitboxSpawnDistance = 1f;

        [Tooltip("Hitbox 生成高度偏移。用于把近战波从脚底抬到腰部或胸口高度。")]
        [SerializeField]
        private float hitboxSpawnHeight = 1f;

        [Header("AI 距离")]
        [Tooltip("AI 判断这个动作可以命中的有效距离。队友会按自己到目标 Collider 表面的距离决定何时停止和出手。")]
        [SerializeField, Min(0.01f)]
        private float effectiveAttackRange = DefaultEffectiveAttackRange;

        /// <summary>动作唯一标识。</summary>
        public string ActionId => actionId;

        /// <summary>显示名称。</summary>
        public string DisplayName => displayName;

        /// <summary>动作类型。</summary>
        public CombatActionType ActionType => actionType;

        /// <summary>动作直接附加的固定伤害。</summary>
        public float FlatDamage => Mathf.Max(0f, flatDamage);

        /// <summary>释放者攻击力倍率。</summary>
        public float AtkPowerMultiplier => Mathf.Max(0f, atkPowerMultiplier);

        /// <summary>伤害类型。</summary>
        public CombatDamageType DamageType => damageType;

        /// <summary>基础瞬时击退距离。</summary>
        public float KnockbackForce => knockbackForce;

        /// <summary>命中时施加的战斗标签资产。</summary>
        public CombatTagDefinition CombatTagToApply => combatTagToApply;

        /// <summary>战斗标签持续时间。小于等于 0 表示使用标签定义的默认持续时间。</summary>
        public float CombatTagDuration => combatTagDuration;

        /// <summary>动作命中时施加的战斗标签层数。</summary>
        public int CombatTagStackCount => Mathf.Max(1, combatTagStackCount);

        /// <summary>该连携动作成功释放后增加的全队协同率。</summary>
        public float SynergyGainOnLink => Mathf.Max(0f, synergyGainOnLink);

        /// <summary>动作时序来源。</summary>
        public CombatActionTimingSource TimingSource => timingSource;

        /// <summary>冷却时间。</summary>
        public float Cooldown => cooldown;

        /// <summary>前摇时间。</summary>
        public float StartupTime => startupTime;

        /// <summary>判定有效时间。</summary>
        public float ActiveTime => activeTime;

        /// <summary>后摇时间。</summary>
        public float RecoveryTime => recoveryTime;

        /// <summary>Hitbox 预制体。</summary>
        public GameObject HitboxPrefab => hitboxPrefab;

        /// <summary>Hitbox 前方生成距离。</summary>
        public float HitboxSpawnDistance => hitboxSpawnDistance;

        /// <summary>Hitbox 高度偏移。</summary>
        public float HitboxSpawnHeight => hitboxSpawnHeight;

        /// <summary>AI 判断该动作可以命中的有效距离。</summary>
        public float EffectiveAttackRange => effectiveAttackRange > 0f
            ? Mathf.Max(0.01f, effectiveAttackRange)
            : DefaultEffectiveAttackRange;

        /// <summary>动作总时长，等于前摇、有效时间和后摇之和。</summary>
        public float TotalDuration => startupTime + activeTime + recoveryTime;

#if UNITY_EDITOR
        /// <summary>
        /// Editor 创建数据资产时使用的轻量初始化入口。
        /// 运行时系统不应通过它修改动作数据。
        /// </summary>
        public void EditorInitialize(CombatActionType initialActionType)
        {
            EditorInitialize(initialActionType, string.Empty);
        }

        /// <summary>
        /// Editor 创建数据资产时使用的轻量初始化入口。
        /// 会把动作类型、ActionId 和显示名称初始化为创建窗口给出的资产名。
        /// </summary>
        public void EditorInitialize(CombatActionType initialActionType, string rawActionName)
        {
            actionType = initialActionType;

            if (!string.IsNullOrWhiteSpace(rawActionName))
            {
                actionId = rawActionName.Trim();
                displayName = rawActionName.Trim();
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void OnValidate()
        {
            flatDamage = Mathf.Max(0f, flatDamage);
            atkPowerMultiplier = Mathf.Max(0f, atkPowerMultiplier);
            knockbackForce = Mathf.Max(0f, knockbackForce);
            combatTagDuration = Mathf.Max(0f, combatTagDuration);
            combatTagStackCount = Mathf.Max(1, combatTagStackCount);
            synergyGainOnLink = Mathf.Max(0f, synergyGainOnLink);
            cooldown = Mathf.Max(0f, cooldown);
            startupTime = Mathf.Max(0f, startupTime);
            activeTime = Mathf.Max(0.01f, activeTime);
            recoveryTime = Mathf.Max(0f, recoveryTime);
            hitboxSpawnDistance = Mathf.Max(0f, hitboxSpawnDistance);

            if (effectiveAttackRange <= 0f)
            {
                effectiveAttackRange = DefaultEffectiveAttackRange;
            }
        }
    }
}
