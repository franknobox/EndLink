namespace EndLink.Core
{
    /// <summary>
    /// 战斗动作时序来源。
    /// 用来声明一个动作的前摇、判定和后摇由数据时间驱动，还是由动画事件驱动。
    /// </summary>
    public enum CombatActionTimingSource
    {
        /// <summary>
        /// 数据驱动。
        /// 使用 CombatActionDefinition 中的 startup / active / recovery 推进动作时序。
        /// 当前所有动作默认使用该模式。
        /// </summary>
        DataDriven = 0,

        /// <summary>
        /// 动画事件驱动。
        /// 动作进入后等待动画事件通知判定开始、判定结束、可取消和动作结束。
        /// 当前只预留接口，Driver 尚未接入。
        /// </summary>
        AnimationEventDriven = 1
    }
}
