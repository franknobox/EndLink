namespace EndLink.Ally
{
    /// <summary>
    /// 闃熷弸姝讳骸鐘舵€併€?    /// 褰撳墠鏄粓姝㈢姸鎬侊紝涓嶅啀鍝嶅簲璺熼殢銆佸姪鎴樺拰鍙楀嚮璇锋眰銆?    /// </summary>
    public sealed class AllyDeadState : AllyStateBase
    {
        public AllyDeadState(AllyStateContext context) : base(context)
        {
        }

        public override AllyStateId StateId => AllyStateId.Dead;

        public override void Tick(float deltaTime)
        {
        }
    }
}

