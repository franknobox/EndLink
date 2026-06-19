namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人的根类别。
    /// 只用于归纳敌人设定分类和配置入口，不表示如封装、继承、多态等战斗特性。
    /// </summary>
    public enum EnemyKind
    {
        /// <summary>AP Shell：异常程序显壳态。有实体框架外壳，可以接受运行伤害与结构伤害。</summary>
        APShell = 0,

        /// <summary>D Agent：受污染的智能体，与玩家和队友同源，可拥有智能体专属机制。</summary>
        DAgent = 1,

        /// <summary>RS Unit：区域级失控系统单元，通常用于 Boss 或大型机制单位。</summary>
        RSUnit = 2,

        /// <summary>AP Free：异常程序游离态。无实体框架，不接受结构伤害。</summary>
        APFree = 3
    }
}
