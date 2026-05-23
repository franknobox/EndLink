namespace EndLink.Ally
{
    /// <summary>
    /// 队友有限状态机状态。
    /// Assist 是助战大状态，接近、攻击和后续行为树细节都放在 Assist 内部处理。
    /// </summary>
    public enum AllyStateId
    {
        None = 0,
        Idle = 1,
        Follow = 2,
        Assist = 3,
        Hit = 4,
        Dead = 5,
        Action = 6
    }
}
