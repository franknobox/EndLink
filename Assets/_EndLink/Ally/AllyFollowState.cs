namespace EndLink.Ally
{
    /// <summary>
    /// 队友跟随状态。
    /// 状态本身只负责判断是否仍有跟随目标，并把每帧移动委托给 AllyFollowMotor。
    /// </summary>
    public sealed class AllyFollowState : AllyStateBase
    {
        public AllyFollowState(AllyStateContext context) : base(context)
        {
        }

        public override AllyStateId StateId => AllyStateId.Follow;

        public override void Tick(float deltaTime)
        {
            if (Context.FollowTarget == null)
            {
                Context.StateMachine.ChangeState(AllyStateId.Idle);
                return;
            }

            Context.FollowMotor.TickFollow(deltaTime);
        }
    }
}
