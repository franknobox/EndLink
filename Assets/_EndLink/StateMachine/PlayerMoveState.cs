namespace EndLink.Core
{
    /// <summary>
    /// 玩家移动状态。
    /// 有移动输入时驱动 PlayerController；输入消失后回到 Idle，由 Idle 负责继续减速。
    /// </summary>
    public sealed class PlayerMoveState : PlayerStateBase
    {
        public PlayerMoveState(PlayerStateContext context) : base(context)
        {
        }

        public override PlayerStateId StateId => PlayerStateId.Move;

        public override void Tick(float deltaTime)
        {
            if (Context.ConsumeSkillRequested() && Context.CanStartSkill)
            {
                Context.StateMachine.ChangeState(PlayerStateId.Skill);
                return;
            }

            if (Context.ConsumeAttackPressed() && Context.CanStartAttack)
            {
                Context.StateMachine.ChangeState(PlayerStateId.Attack);
                return;
            }

            if (!Context.HasMoveInput)
            {
                Context.StateMachine.ChangeState(PlayerStateId.Idle);
                return;
            }

            Context.Controller.TickMovement(Context.InputReader.MoveInput, deltaTime);
        }
    }
}
