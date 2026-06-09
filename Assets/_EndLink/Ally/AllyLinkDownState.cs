namespace EndLink.Ally
{
    /// <summary>
    /// 队友链接中断状态。
    /// 当前是队友生命归零后的终止状态，不再响应跟随、助战、动作和受击请求。
    /// </summary>
    public sealed class AllyLinkDownState : AllyStateBase
    {
        public AllyLinkDownState(AllyStateContext context) : base(context)
        {
        }

        public override AllyStateId StateId => AllyStateId.LinkDown;

        public override void Tick(float deltaTime)
        {
        }
    }
}
