using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 战斗动作类型。
    /// 描述动作本身的性质，不描述释放者来源。
    /// 例如主控和队友都可以释放 BasicAttack 或 Skill，来源信息后续由战斗事件数据携带。
    /// </summary>
    public enum CombatActionType
    {
        /// <summary>普通攻击。通常高频、低成本，用于基础输出和标签验证。</summary>
        BasicAttack = 0,

        /// <summary>普通技能。通常由玩家输入或 AI 决策释放，有冷却、时序和特殊效果。</summary>
        Skill = 1,

        /// <summary>连携攻击。通常由标签、事件或连携规则触发，不一定由玩家直接输入。</summary>
        LinkAttack = 2,

        /// <summary>大招。通常消耗高权重资源或满足特殊条件，后续可接演出和镜头。</summary>
        Ultimate = 3
    }

    /// <summary>
    /// 战斗动作配置。
    /// 用一个 ScriptableObject 描述一次普攻或简单技能所需的基础数据，
    /// 让伤害、冷却、命中盒和时序从组件字段逐步迁移到可复用的数据资产。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CombatAction_",
        menuName = "EndLink/Combat/Combat Action Definition")]
    public sealed class CombatActionDefinition : ScriptableObject
    {
        [Header("基础信息")]
        [Tooltip("动作唯一标识。建议使用英文小写加下划线，例如 player_basic_attack_01。")]
        [SerializeField]
        private string actionId = "new_combat_action";

        [Tooltip("显示名称。主要用于 Inspector、调试面板或后续 UI。")]
        [SerializeField]
        private string displayName = "New Combat Action";

        [Tooltip("动作类型。描述动作性质，不描述释放者来源。主控、队友和敌人后续可以共用同一套类型。")]
        [SerializeField]
        private CombatActionType actionType = CombatActionType.BasicAttack;

        [Header("伤害与标签")]
        [Tooltip("动作造成的基础伤害值。胶囊白模阶段先使用整数伤害。")]
        [SerializeField, Min(0)]
        private int damageAmount = 10;

        [Tooltip("动作命中时附带的击退力度。具体如何应用由受击方或后续击退系统决定。")]
        [SerializeField, Min(0f)]
        private float knockbackForce = 3f;

        [Tooltip("动作命中时施加的战斗标签资产。新逻辑应优先使用它。")]
        [SerializeField]
        private CombatTagDefinition combatTagToApply;

        [Tooltip("战斗标签持续时间。小于等于 0 表示永久标签。")]
        [SerializeField, Min(0f)]
        private float combatTagDuration;

        [Header("冷却与时序")]
        [Tooltip("动作冷却时间。冷却未结束时不应再次释放同一个动作。")]
        [SerializeField, Min(0f)]
        private float cooldown = 0.45f;

        [Tooltip("前摇时间。表示输入成立后到命中判定出现前的时间。")]
        [SerializeField, Min(0f)]
        private float startupTime = 0.1f;

        [Tooltip("有效时间。表示 Hitbox 或判定窗口持续多久。")]
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

        [Tooltip("Hitbox 自动销毁时间。通常应接近或等于有效时间。")]
        [SerializeField, Min(0.01f)]
        private float hitboxLifetime = 0.2f;

        /// <summary>
        /// 动作唯一标识。
        /// </summary>
        public string ActionId => actionId;

        /// <summary>
        /// 显示名称。
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 动作类型。
        /// </summary>
        public CombatActionType ActionType => actionType;

        /// <summary>
        /// 基础伤害值。
        /// </summary>
        public int DamageAmount => damageAmount;

        /// <summary>
        /// 击退力度。
        /// </summary>
        public float KnockbackForce => knockbackForce;

        /// <summary>
        /// 命中时施加的战斗标签资产。
        /// </summary>
        public CombatTagDefinition CombatTagToApply => combatTagToApply;

        /// <summary>
        /// 战斗标签持续时间。小于等于 0 表示永久标签。
        /// </summary>
        public float CombatTagDuration => combatTagDuration;

        /// <summary>
        /// 冷却时间。
        /// </summary>
        public float Cooldown => cooldown;

        /// <summary>
        /// 前摇时间。
        /// </summary>
        public float StartupTime => startupTime;

        /// <summary>
        /// 判定有效时间。
        /// </summary>
        public float ActiveTime => activeTime;

        /// <summary>
        /// 后摇时间。
        /// </summary>
        public float RecoveryTime => recoveryTime;

        /// <summary>
        /// Hitbox 预制体。
        /// </summary>
        public GameObject HitboxPrefab => hitboxPrefab;

        /// <summary>
        /// Hitbox 前方生成距离。
        /// </summary>
        public float HitboxSpawnDistance => hitboxSpawnDistance;

        /// <summary>
        /// Hitbox 高度偏移。
        /// </summary>
        public float HitboxSpawnHeight => hitboxSpawnHeight;

        /// <summary>
        /// Hitbox 存活时间。
        /// </summary>
        public float HitboxLifetime => hitboxLifetime;

        /// <summary>
        /// 动作总时长，等于前摇、有效时间和后摇之和。
        /// </summary>
        public float TotalDuration => startupTime + activeTime + recoveryTime;
    }
}
