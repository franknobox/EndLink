using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 统一伤害计算入口。
    /// 当前负责合并动作固定伤害与攻击力倍率，后续在这里继续接入防御、暴击、抗性和标签修正。
    /// </summary>
    public static class DamageCalculator
    {
        /// <summary>计算最终伤害结果。</summary>
        public static DamageResult Calculate(DamageContext context)
        {
            float calculatedDamage = context.BaseDamage;
            CombatActionDefinition actionDefinition = context.ActionDefinition;

            // 只有来源于动作配置的伤害才读取攻击力倍率。
            // 标签反应、环境伤害等没有 ActionDefinition 的直接伤害继续只使用 BaseDamage。
            if (actionDefinition != null
                && actionDefinition.AtkPowerMultiplier > 0f
                && context.Source != null)
            {
                CharacterStats sourceStats = context.SourceStats != null
                    ? context.SourceStats
                    : context.Source.GetComponentInParent<CharacterStats>();
                if (sourceStats != null)
                {
                    calculatedDamage += sourceStats.AttackPower * actionDefinition.AtkPowerMultiplier;
                }
            }

            int finalDamage = Mathf.Max(0, Mathf.RoundToInt(calculatedDamage));
            return new DamageResult(context, finalDamage);
        }
    }
}
