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
            if (Context.ConsumeDodgePressed() && Context.CanStartDodge)
            {
                Context.StateMachine.ChangeState(PlayerStateId.Dodge);
                return;
            }

            if (Context.ConsumeJumpPressed() && Context.TryJump())
            {
                Vector2 jumpMoveInput = Context.HasMoveInput ? Context.InputReader.MoveInput : Vector2.zero;
                Context.Controller.TickMovement(jumpMoveInput, Context.InputReader.SprintHeld, deltaTime);

                if (Context.HasMoveInput)
                {
                    Context.StateMachine.ChangeState(PlayerStateId.Move);
                }

                return;
            }

            if (Context.ConsumeSkillRequested() && Context.CanStartSkill)
            {
                Context.StateMachine.ChangeState(PlayerStateId.Skill);
                return;
            }

            if (Context.TryConsumeBufferedAttack())
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
