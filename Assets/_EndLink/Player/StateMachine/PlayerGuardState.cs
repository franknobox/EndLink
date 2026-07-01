using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 玩家按住防御状态。
    /// 进入时刷新弹反窗口，松开防御键后回到移动状态；受击与死亡仍由状态机外部请求打断。
    /// </summary>
    public sealed class PlayerGuardState : PlayerStateBase
    {
        public PlayerGuardState(PlayerStateContext context) : base(context)
        {
        }

        public override PlayerStateId StateId => PlayerStateId.Guard;

        public override void Enter()
        {
            Context.GuardController?.BeginGuard();
        }

        public override void Tick(float deltaTime)
        {
            Context.ConsumeJumpPressed();

            if (Context.ConsumeDodgePressed() && Context.CanStartDodge)
            {
                Context.StateMachine.ChangeState(PlayerStateId.Dodge);
                return;
            }

            if (!Context.InputReader.GuardHeld)
            {
                Context.StateMachine.ChangeState(Context.HasMoveInput ? PlayerStateId.Move : PlayerStateId.Idle);
                return;
            }

            Vector2 guardMoveInput = Context.InputReader.MoveInput * Context.GuardMoveInputScale;
            Context.Controller.TickMovement(guardMoveInput, deltaTime);
        }

        public override void Exit()
        {
            Context.GuardController?.EndGuard();
        }
    }
}
