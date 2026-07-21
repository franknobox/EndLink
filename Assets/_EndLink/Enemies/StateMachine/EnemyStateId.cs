namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人大状态标识。
    /// 后续行为树只接入 Combat 状态内部，不改变这些外层状态的职责。
    /// </summary>
    public enum EnemyStateId
    {
        None = 0,
        Idle = 1,
        Alert = 2,
        Combat = 3,
        Hit = 4,
        Dead = 5,
        Return = 6,
        Stagger = 7
    }
}
