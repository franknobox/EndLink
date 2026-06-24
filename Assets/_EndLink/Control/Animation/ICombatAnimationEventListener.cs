namespace EndLink.Core
{
    /// <summary>
    /// 战斗动画事件监听接口。
    /// Driver、状态机或其它桥接层实现它后，可接收 CombatAnimationEventReceiver 转发的动画事件。
    /// </summary>
    public interface ICombatAnimationEventListener
    {
        /// <summary>动画通知动作判定开始，例如启用或生成 Hitbox。</summary>
        void OnActionHitboxStart();

        /// <summary>动画通知动作判定结束，例如关闭当前判定窗口。</summary>
        void OnActionHitboxEnd();

        /// <summary>动画通知当前动作已经允许取消或派生。</summary>
        void OnActionCanCancel();

        /// <summary>动画通知当前动作完全结束。</summary>
        void OnActionEnd();
    }
}
