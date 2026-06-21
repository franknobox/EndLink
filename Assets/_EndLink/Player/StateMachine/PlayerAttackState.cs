using UnityEngine;
using EndLink.Combat;

namespace EndLink.Core
{
    /// <summary>
    /// 玩家攻击状态。
    /// 进入时通过 PlayerCombatDriver 提交普攻动作，请求成立后由 CombatActionDefinition 的 startup / active / recovery 推进实际判定。
    /// 状态持续时间当前仍保留一个最小兜底时长，并与普攻动作总时长取较大值。
    /// </summary>
    public sealed class PlayerAttackState : PlayerStateBase
    {
        private float _elapsedTime;

        public PlayerAttackState(PlayerStateContext context) : base(context)
        {
        }

        public override PlayerStateId StateId => PlayerStateId.Attack;

        public override void Enter()
        {
            _elapsedTime = 0f;
            ICombatActionExecutor executor = Context.ActionExecutor;
            executor?.TryExecute(Context.CombatDriver.BasicAttackAction);
        }

        public override void Tick(float deltaTime)
        {
            _elapsedTime += deltaTime;
            Context.ConsumeJumpPressed();

            Vector2 attackMoveInput = Context.InputReader.MoveInput * Context.AttackMoveInputScale;
            Context.Controller.TickMovement(attackMoveInput, deltaTime);
            Context.TickAttackTargetFacing(deltaTime);

            if (_elapsedTime < Context.AttackDuration)
            {
                return;
            }

            Context.StateMachine.ChangeState(Context.HasMoveInput ? PlayerStateId.Move : PlayerStateId.Idle);
        }
    }
}
