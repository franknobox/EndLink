using EndLink.Combat;
using UnityEngine;

namespace EndLink.Enemies
{
    /// <summary>
    /// 正式敌人的根入口组件。
    /// 它只负责暴露敌人身份和关键组件引用，不直接处理扣血、移动、AI 或攻击。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(CombatTagContainer))]
    [RequireComponent(typeof(CombatTarget))]
    public sealed class EnemyActor : MonoBehaviour, ICharacterStatsTypeProvider
    {
        [Header("分类")]
        [Tooltip("敌人的根类别。用于归纳敌人设定来源和后续配置入口，不表示封装、继承、多态等战斗特性。")]
        [SerializeField]
        private EnemyKind enemyKind = EnemyKind.AberrantProgram;

        [Tooltip("异常程序的实体形态。只有敌人类别为 Aberrant Program 时生效；游离态不接受结构伤害，显壳态可接受结构伤害与运行伤害。")]
        [SerializeField]
        private AberrantProgramForm aberrantProgramForm = AberrantProgramForm.ManifestedShell;

        [Header("视觉")]
        [Tooltip("视觉根节点。当前只作为后续动画/表现预留引用。")]
        [SerializeField]
        private Transform bodyRoot;

        [Header("能力组件")]
        [Tooltip("敌人移动能力组件。普通地面敌人拖 EnemyMotorBase；炮台或特殊敌人可以留空或拖自定义子类。")]
        [SerializeField]
        private EnemyMotorBase motor;

        [Tooltip("敌人战斗执行器。需要攻击能力的敌人拖 EnemyCombatDriver；纯木桩或非攻击单位可以留空。")]
        [SerializeField]
        private EnemyCombatDriver combatDriver;

        private EnemyHealth _health;
        private CombatTagContainer _tagContainer;
        private CombatTarget _combatTarget;

        /// <summary>敌人生命组件。</summary>
        public EnemyHealth Health
        {
            get
            {
                if (_health == null)
                {
                    _health = GetComponent<EnemyHealth>();
                }

                return _health;
            }
        }

        /// <summary>敌人的根类别。</summary>
        public EnemyKind EnemyKind => enemyKind;

        /// <summary>异常程序的实体形态。非异常程序敌人会忽略该值。</summary>
        public AberrantProgramForm AberrantProgramForm => aberrantProgramForm;

        /// <summary>当前敌人是否为游离态异常程序。</summary>
        public bool IsFreeAberrantProgram => enemyKind == EnemyKind.AberrantProgram
            && aberrantProgramForm == AberrantProgramForm.FreeState;

        /// <summary>判断当前敌人是否接受指定伤害类型。</summary>
        public bool CanReceiveDamageType(CombatDamageType damageType)
        {
            return !(IsFreeAberrantProgram && damageType == CombatDamageType.StructuralDamage);
        }

        /// <summary>敌人的战斗标签容器。</summary>
        public CombatTagContainer TagContainer
        {
            get
            {
                if (_tagContainer == null)
                {
                    _tagContainer = GetComponent<CombatTagContainer>();
                }

                return _tagContainer;
            }
        }

        /// <summary>敌人的移动能力组件，由 EnemyActor 统一承载配置。允许为空。</summary>
        public EnemyMotorBase Motor => motor;

        /// <summary>敌人的战斗执行器。没有攻击能力的敌人可以为空。</summary>
        public EnemyCombatDriver CombatDriver => combatDriver;

        /// <summary>敌人的统一战斗动作执行接口。没有攻击能力时为空。</summary>
        public ICombatActionExecutor ActionExecutor => combatDriver;

        /// <summary>敌人的统一战斗目标身份。</summary>
        public CombatTarget CombatTarget
        {
            get
            {
                if (_combatTarget == null)
                {
                    _combatTarget = GetComponent<CombatTarget>();
                }

                return _combatTarget;
            }
        }

        /// <summary>视觉根节点。</summary>
        public Transform BodyRoot => bodyRoot;

        /// <summary>供 CharacterStats 自动识别为敌人配置。</summary>
        public CharacterStatsType StatsType => CharacterStatsType.Enemy;

        private void Awake()
        {
            _health = GetComponent<EnemyHealth>();
            _tagContainer = GetComponent<CombatTagContainer>();
            _combatTarget = GetComponent<CombatTarget>();
        }

        private void Reset()
        {
            _health = GetComponent<EnemyHealth>();
            _tagContainer = GetComponent<CombatTagContainer>();
            _combatTarget = GetComponent<CombatTarget>();
            bodyRoot = transform;
        }
    }
}
