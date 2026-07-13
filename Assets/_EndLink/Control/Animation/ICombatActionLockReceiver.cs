namespace EndLink.Core
{
    /// <summary>
    /// 战斗动作锁定与退出接口。
    /// 用于让动画事件或动作运行时通知状态机：动作可取消、动作结束或被打断。
    /// </summary>
    public interface ICombatActionLockReceiver
    {
        /// <summary>当前动作是否仍锁定角色状态或输入。</summary>
        bool IsActionLocked { get; }

        /// <summary>通知一个战斗动作已经成功开始并进入锁定阶段。</summary>
        void NotifyActionStarted();

        /// <summary>通知当前动作已经允许取消、连段或派生。</summary>
        void NotifyActionCanCancel();

        /// <summary>通知当前动作自然结束。</summary>
        void NotifyActionEnd();

        /// <summary>通知当前动作被外部打断。</summary>
        void NotifyActionInterrupted();
    }
}
