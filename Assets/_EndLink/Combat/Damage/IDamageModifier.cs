namespace EndLink.Combat
{
    /// <summary>
    /// 伤害修正接口。
    /// 后续 Buff、Debuff、装备、被动、场地效果可以实现该接口参与 DamageCalculator。
    /// </summary>
    public interface IDamageModifier
    {
        /// <summary>基于当前上下文和已有结果，返回修正后的伤害结果。</summary>
        DamageResult Modify(DamageContext context, DamageResult currentResult);
    }
}
