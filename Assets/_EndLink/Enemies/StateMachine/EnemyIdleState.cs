namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人待机状态。
    /// 当前不执行行为，后续可接巡逻、休眠或感知逻辑。
    /// </summary>
    public sealed class EnemyIdleState : EnemyStateBase
    {
        public EnemyIdleState(EnemyStateContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public override EnemyStateId StateId => EnemyStateId.Idle;

        /// <inheritdoc />
        public override void Enter()
        {
            Context.Motor?.Stop();
        }

        /// <inheritdoc />
        public override void Tick(float deltaTime)
        {
        }
    }
}
