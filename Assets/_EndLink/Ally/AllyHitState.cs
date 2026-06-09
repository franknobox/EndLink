namespace EndLink.Ally
{
    /// <summary>
    /// 队友受击状态。
    /// 当前用于短暂打断跟随、助战或通用动作；结束后的恢复目标由 AllyStateMachine 决定。
    /// </summary>
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

            Context.StateMachine.CompleteHit();
        }
    }
}
