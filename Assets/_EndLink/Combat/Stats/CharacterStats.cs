using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// CharacterStats 使用的角色类型。
    /// Auto 会从当前物体或父物体上的 ICharacterStatsTypeProvider 自动识别。
    /// </summary>
    public enum CharacterStatsType
    {
        Auto = 0,
        Player = 1,
        Ally = 2,
        Enemy = 3
    }

    /// <summary>
    /// 角色战斗数值的统一入口。
    /// 玩家、队友和敌人可以共用该组件，生命值仍由 CharacterHealth 或敌人生命组件负责。
    /// 第一版只承载攻击力，后续可以在这里继续接入成长、装备、Buff 和 Debuff 修正。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterStats : MonoBehaviour
    {
        [Header("角色类型")]
        [Tooltip("Auto 会从玩家状态机、队友状态机或 EnemyActor 自动识别。也可以手动指定类型覆盖自动结果。")]
        [SerializeField]
        private CharacterStatsType statsType = CharacterStatsType.Auto;

        [Header("攻击属性")]
        [Tooltip("角色未经任何临时修正的基础攻击力。")]
        [SerializeField, Min(0f)]
        private float baseAttackPower = 10f;

        /// <summary>Inspector 中选择的类型模式。</summary>
        public CharacterStatsType ConfiguredType => statsType;

        /// <summary>
        /// 当前实际使用的角色类型。
        /// 手动类型直接生效；Auto 会查询当前物体及父物体上的类型提供者。
        /// 未找到类型提供者时返回 Auto，表示尚未识别。
        /// </summary>
        public CharacterStatsType ResolvedType => statsType == CharacterStatsType.Auto
            ? DetectStatsType()
            : statsType;

        /// <summary>
        /// 角色的基础攻击力。
        /// 后续升级和永久成长可以修改该数值或它的数据来源。
        /// </summary>
        public float BaseAttackPower => Mathf.Max(0f, baseAttackPower);

        /// <summary>
        /// 参与伤害计算的最终攻击力。
        /// 第一版等于基础攻击力；后续可在这里汇总装备、Buff、Debuff 等临时修正。
        /// </summary>
        public float AttackPower => BaseAttackPower;

        /// <summary>
        /// 从当前物体和父物体上的角色身份组件识别数值类型。
        /// 该查询用于初始化和 Inspector 展示，不应放入每帧热路径。
        /// </summary>
        public CharacterStatsType DetectStatsType()
        {
            MonoBehaviour[] behaviours = GetComponentsInParent<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is not ICharacterStatsTypeProvider provider)
                {
                    continue;
                }

                CharacterStatsType providedType = provider.StatsType;
                if (providedType != CharacterStatsType.Auto)
                {
                    return providedType;
                }
            }

            return CharacterStatsType.Auto;
        }

        private void OnValidate()
        {
            baseAttackPower = Mathf.Max(0f, baseAttackPower);
        }
    }
}
