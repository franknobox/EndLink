using UnityEngine;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人脱战归位状态。
    /// 清除战斗目标并使用当前移动能力返回 Home，抵达后恢复 Idle。
    /// </summary>
    public sealed class EnemyReturnState : EnemyStateBase
    {
        public EnemyReturnState(EnemyStateContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public override EnemyStateId StateId => EnemyStateId.Return;

        /// <inheritdoc />
        public override void Enter()
        {
            Context.StateMachine.SetTarget(null);
            Context.CombatDriver?.CancelCurrentAction();
        }

        /// <inheritdoc />
        public override void Exit()
        {
            Context.Motor?.Stop();
        }

        /// <inheritdoc />
        public override void Tick(float deltaTime)
        {
            if (Context.Motor == null || HasReachedHome())
            {
                CompleteReturn();
                return;
            }

            Context.Motor.MoveTo(
                Context.HomePosition,
                Context.ReturnStopDistance,
                deltaTime);
        }

        private bool HasReachedHome()
        {
            Vector3 offset = Context.HomePosition - Context.Transform.position;
            offset.y = 0f;
            float stopDistance = Mathf.Max(0f, Context.ReturnStopDistance);
            return offset.sqrMagnitude <= stopDistance * stopDistance;
        }

        private void CompleteReturn()
        {
            Context.Motor?.Stop();
            Context.StateMachine.ChangeState(EnemyStateId.Idle);
        }
    }
}
