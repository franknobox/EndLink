namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人的根类别。
    /// 只用于归纳敌人设定分类和未来配置入口，不表示如封装、继承、多态等战斗特性。
    /// </summary>
    public enum EnemyKind
    {
        /// <summary>污染区中最常见的普通敌人，通常没有完整人格或核心模型。</summary>
        AberrantProgram = 0,

        /// <summary>受污染的智能体，与玩家和队友同源，后续可拥有智能体专属机制。</summary>
        DelinkedAgent = 1,

        /// <summary>区域级失控系统单元，通常用于 Boss 或大型机制单位。</summary>
        RogueSystemUnit = 2
    }
}
