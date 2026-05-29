using UnityEngine;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人战斗大状态。
    /// 第一版不执行具体行为；后续行为树会挂在这里，负责追击、站位、攻击和技能等细节。
    /// </summary>
    public sealed class EnemyCombatState : EnemyStateBase
    {
        public EnemyCombatState(EnemyStateContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public override EnemyStateId StateId => EnemyStateId.Combat;

        /// <inheritdoc />
        public override void Enter()
        {
            if (!Context.HasValidTarget)
            {
                Context.StateMachine.ChangeState(EnemyStateId.Idle);
            }
        }

        /// <inheritdoc />
        public override void Exit()
        {
            Context.Motor?.Stop();
        }

        /// <inheritdoc />
        public override void Tick(float deltaTime)
        {
            if (!Context.HasValidTarget)
            {
                Context.Motor?.Stop();
                Context.StateMachine.ChangeState(EnemyStateId.Idle);
                return;
            }

            Transform target = Context.CurrentTarget;
            if (IsTargetBeyondLeash(target))
            {
                Context.Motor?.Stop();
                Context.StateMachine.SetTarget(null);
                Context.StateMachine.ChangeState(EnemyStateId.Idle);
                return;
            }

            Context.Motor?.MoveTo(target.position, Context.CombatChaseStopDistance, deltaTime);
            Context.Motor?.FaceTarget(target, deltaTime);
        }

        private bool IsTargetBeyondLeash(Transform target)
        {
            if (target == null || Context.CombatLeashDistance <= 0f)
            {
                return false;
            }

            Vector3 offset = target.position - Context.Transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude > Context.CombatLeashDistance * Context.CombatLeashDistance;
        }
    }
}
