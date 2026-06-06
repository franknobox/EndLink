namespace EndLink.Combat
{
    /// <summary>
    /// CombatTarget 读取角色存活状态的最小接口。
    /// 生命组件只提供存活结果，不再承担目标点、锁定和距离计算职责。
    /// </summary>
    public interface ICombatTargetLifeState
    {
        /// <summary>当前是否存活。</summary>
        bool IsAlive { get; }
    }
}
