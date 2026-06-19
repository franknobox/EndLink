namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人警觉状态。
    /// 当前只做短暂停留，之后根据是否存在有效目标进入 Combat 或回到 Idle。
    /// </summary>
    public sealed class EnemyAlertState : EnemyStateBase
    {
        private float _elapsedTime;

        public EnemyAlertState(EnemyStateContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public override EnemyStateId StateId => EnemyStateId.Alert;

        /// <inheritdoc />
        public override void Enter()
        {
            _elapsedTime = 0f;
            Context.Motor?.Stop();
        }

        /// <inheritdoc />
        public override void Tick(float deltaTime)
        {
            if (Context.AlertTransitionExternallyControlled)
            {
                if (!Context.HasValidTarget)
                {
                    Context.StateMachine.ReturnFromAlert();
                    return;
                }

                Context.Motor?.FaceTarget(Context.CurrentTarget, deltaTime);
                return;
            }

            _elapsedTime += deltaTime;

            if (_elapsedTime < Context.AlertDuration)
            {
                return;
            }

            if (Context.HasValidTarget)
            {
                Context.StateMachine.RequestCombat(Context.CurrentTarget);
                return;
            }

            Context.StateMachine.ReturnFromAlert();
        }
    }
}
