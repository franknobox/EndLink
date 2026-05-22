namespace EndLink.Ally
{
    /// <summary>
    /// 队友有限状态机状态。
    /// 后续如果接行为树，这些状态仍然作为具体执行阶段使用。
    /// </summary>
    public enum AllyStateId
    {
        None = 0,
        Idle = 1,
        Follow = 2,
        AssistApproach = 3,
        Assist = 4,
        Hit = 5,
        Dead = 6
    }
}
