namespace EndLink.Enemies
{
    /// <summary>
    /// 异常程序的实体形态。
    /// 只在 EnemyKind 为 AberrantProgram 时有意义。
    /// </summary>
    public enum AberrantProgramForm
    {
        /// <summary>游离态。无实体框架，不接受结构伤害。</summary>
        FreeState = 0,

        /// <summary>显壳态。有实体框架外壳，可以接受结构伤害与运行伤害。</summary>
        ManifestedShell = 1
    }
}
