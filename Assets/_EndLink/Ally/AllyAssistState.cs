namespace EndLink.Ally
{
    /// <summary>
    /// 闃熷弸鍔╂垬鐘舵€併€?    /// 鐢ㄤ簬褰撳墠娴嬭瘯杩炴惡閾捐矾锛氱姸鎬佽繘鍏ユ椂鎵ц涓€娆″姪鎴樺姩浣滐紝绛夊緟鍔ㄤ綔绐楀彛缁撴潫鍚庡洖鍒?Follow 鎴?Idle銆?    /// </summary>
    public sealed class AllyAssistState : AllyStateBase
    {
        private float _elapsedTime;

        public AllyAssistState(AllyStateContext context) : base(context)
        {
        }

        public override AllyStateId StateId => AllyStateId.Assist;

        public override void Enter()
        {
            _elapsedTime = 0f;
            Context.CombatDriver.ExecuteAssist(Context.CurrentAssistTarget);
        }

        public override void Tick(float deltaTime)
        {
            _elapsedTime += deltaTime;

            if (_elapsedTime < Context.AssistDuration)
            {
                return;
            }

            Context.StateMachine.ChangeState(Context.FollowTarget != null ? AllyStateId.Follow : AllyStateId.Idle);
        }
    }
}

