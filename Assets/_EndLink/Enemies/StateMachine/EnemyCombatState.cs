using UnityEngine;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人战斗大状态。
    /// 负责 Combat 生命周期、Home 追击边界和目标丢失处理；
    /// 接近、定位、攻击和恢复由内部 EnemyCombatBehavior 推进。
    /// </summary>
    public sealed class EnemyCombatState : EnemyStateBase
    {
        private readonly EnemyCombatBehavior _combatBehavior;
        private float _lostTargetElapsed;

        public EnemyCombatState(EnemyStateContext context) : base(context)
        {
            _combatBehavior = new EnemyCombatBehavior(context);
        }

        /// <inheritdoc />
        public override EnemyStateId StateId => EnemyStateId.Combat;

        /// <inheritdoc />
        public override void Enter()
        {
            _lostTargetElapsed = 0f;
            _combatBehavior.Enter();
        }

        /// <inheritdoc />
        public override void Exit()
        {
            _combatBehavior.Exit();
        }

        /// <inheritdoc />
        public override void Tick(float deltaTime)
        {
            if (IsBeyondHomeLeash())
            {
                Context.StateMachine.RequestReturn();
                return;
            }

            if (!Context.HasValidTarget)
            {
                _combatBehavior.CancelCurrentAttempt();
                TickLostTarget(deltaTime);
                return;
            }

            _lostTargetElapsed = 0f;
            _combatBehavior.Tick(deltaTime);
        }

        private void TickLostTarget(float deltaTime)
        {
            Context.Motor?.Stop();
            _lostTargetElapsed += Mathf.Max(0f, deltaTime);

            if (_lostTargetElapsed < Context.LostTargetDelay)
            {
                return;
            }

            Context.StateMachine.SetTarget(null);
            Context.StateMachine.RequestReturn();
        }

        private bool IsBeyondHomeLeash()
        {
            if (Context.MaxChaseRadius <= 0f)
            {
                return false;
            }

            Vector3 offset = Context.Transform.position - Context.HomePosition;
            offset.y = 0f;
            return offset.sqrMagnitude > Context.MaxChaseRadius * Context.MaxChaseRadius;
        }
    }
}
