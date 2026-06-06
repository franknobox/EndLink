namespace EndLink.Combat
{
    /// <summary>
    /// 为 CharacterStats 提供角色类型。
    /// 玩家、队友和敌人的根身份组件实现该接口，避免 CharacterStats 依赖具体角色脚本。
    /// </summary>
    public interface ICharacterStatsTypeProvider
    {
        /// <summary>当前角色对应的数值配置类型。</summary>
        CharacterStatsType StatsType { get; }
    }
}
