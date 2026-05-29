namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人死亡状态。
    /// 死亡是终态，不再执行行为，也不会被其他普通状态覆盖。
    /// </summary>
    public sealed class EnemyDeadState : EnemyStateBase
    {
        public EnemyDeadState(EnemyStateContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public override EnemyStateId StateId => EnemyStateId.Dead;

        /// <inheritdoc />
        public override void Enter()
        {
            Context.Motor?.Stop();
            Context.StateMachine.SetTarget(null);
        }

        /// <inheritdoc />
        public override void Tick(float deltaTime)
        {
        }
    }
}
