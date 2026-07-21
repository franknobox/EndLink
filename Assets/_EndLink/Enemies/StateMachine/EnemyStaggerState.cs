namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人失衡状态。
    /// 它会中断当前动作并停止移动，在状态持续期间由 EnemyBalance 暴露处决资格。
    /// </summary>
    public sealed class EnemyStaggerState : EnemyStateBase
    {
        private float _elapsedTime;

        public EnemyStaggerState(EnemyStateContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public override EnemyStateId StateId => EnemyStateId.Stagger;

        /// <inheritdoc />
        public override void Enter()
        {
            _elapsedTime = 0f;
            Context.Balance?.ForceStagger();
            Context.CombatDriver?.CancelCurrentAction();
            Context.Motor?.Stop();
        }

        /// <inheritdoc />
        public override void Tick(float deltaTime)
        {
            if (Context.Health != null && Context.Health.IsDead)
            {
                return;
            }

            _elapsedTime += deltaTime;
            if (_elapsedTime < Context.StaggerDuration)
            {
                return;
            }

            Context.StateMachine.ChangeState(
                Context.HasValidTarget ? EnemyStateId.Combat : EnemyStateId.Idle);
        }

        /// <inheritdoc />
        public override void Exit()
        {
            if (Context.Health == null || !Context.Health.IsDead)
            {
                Context.Balance?.RecoverFromStagger();
            }
        }
    }
}
