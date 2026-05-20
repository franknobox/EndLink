namespace EndLink.Ally
{
    /// <summary>
    /// 闃熷弸寰呮満鐘舵€併€?    /// 娌℃湁璺熼殢鐩爣鏃跺仠鐣欏湪寰呮満锛涗竴鏃﹂厤缃簡璺熼殢鐩爣锛屽氨鍒囨崲鍒?Follow 鐘舵€併€?    /// </summary>
    public sealed class AllyIdleState : AllyStateBase
    {
        public AllyIdleState(AllyStateContext context) : base(context)
        {
        }

        public override AllyStateId StateId => AllyStateId.Idle;

        public override void Tick(float deltaTime)
        {
            if (Context.FollowTarget != null)
            {
                Context.StateMachine.ChangeState(AllyStateId.Follow);
            }
        }
    }
}

