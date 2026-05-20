namespace EndLink.Ally
{
    /// <summary>
    /// 闃熷弸鍙楀嚮鐘舵€併€?    /// 褰撳墠鐢ㄤ簬鐭殏鎵撴柇鍔╂垬鎴栬窡闅忥紝鍚庣画鍙帴鍙楀嚮鍔ㄧ敾銆佺‖鐩淬€佸嚮閫€鍜屾姉鎬ц鍒欍€?    /// </summary>
    public sealed class AllyHitState : AllyStateBase
    {
        private float _elapsedTime;

        public AllyHitState(AllyStateContext context) : base(context)
        {
        }

        public override AllyStateId StateId => AllyStateId.Hit;

        public override void Enter()
        {
            _elapsedTime = 0f;
        }

        public override void Tick(float deltaTime)
        {
            _elapsedTime += deltaTime;

            if (_elapsedTime < Context.HitDuration)
            {
                return;
            }

            Context.StateMachine.ChangeState(Context.FollowTarget != null ? AllyStateId.Follow : AllyStateId.Idle);
        }
    }
}

