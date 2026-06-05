using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 统一伤害计算入口。
    /// 第一版只把基础伤害取整并产出 DamageResult，后续在这里接入角色数值、动作倍率、暴击、抗性和标签修正。
    /// </summary>
    public static class DamageCalculator
    {
        /// <summary>计算最终伤害结果。</summary>
        public static DamageResult Calculate(DamageContext context)
        {
            int finalDamage = Mathf.RoundToInt(context.BaseDamage);
            return new DamageResult(context, finalDamage);
        }
    }
}
