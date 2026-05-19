using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 玩家待机状态。
    /// 没有移动输入时保持在该状态，同时继续给 PlayerController 传入零输入，
    /// 让角色可以平滑减速并持续处理重力。
    /// </summary>
    public sealed class PlayerIdleState : PlayerStateBase
    {
        public PlayerIdleState(PlayerStateContext context) : base(context)
        {
        }

        public override PlayerStateId StateId => PlayerStateId.Idle;

        public override void Tick(float deltaTime)
        {
            if (Context.ConsumeAttackPressed() && Context.CanStartAttack)
            {
                Context.StateMachine.ChangeState(PlayerStateId.Attack);
                return;
            }

            if (Context.HasMoveInput)
            {
                Context.StateMachine.ChangeState(PlayerStateId.Move);
                return;
            }

            Context.Controller.TickMovement(Vector2.zero, deltaTime);
        }
    }
}
