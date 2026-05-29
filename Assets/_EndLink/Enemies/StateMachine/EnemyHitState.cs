namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人受击状态。
    /// 它是外层强制打断状态，不放进 Combat 行为树，方便后续加入硬直、霸体和击倒规则。
    /// </summary>
    public sealed class EnemyHitState : EnemyStateBase
    {
        private float _elapsedTime;

        public EnemyHitState(EnemyStateContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public override EnemyStateId StateId => EnemyStateId.Hit;

        /// <inheritdoc />
        public override void Enter()
        {
            _elapsedTime = 0f;
            Context.Motor?.Stop();
        }

        /// <inheritdoc />
        public override void Tick(float deltaTime)
        {
            _elapsedTime += deltaTime;

            if (_elapsedTime < Context.HitDuration)
            {
                return;
            }

            Context.StateMachine.ChangeState(Context.HasValidTarget ? EnemyStateId.Combat : EnemyStateId.Idle);
        }
    }
}
