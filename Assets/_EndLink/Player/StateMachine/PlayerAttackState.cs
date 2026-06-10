using UnityEngine;
using EndLink.Combat;

namespace EndLink.Core
{
    /// <summary>
    /// 玩家攻击状态。
    /// 胶囊白模阶段用固定时间表示一次攻击过程，并在 Enter 中通过 PlayerCombatDriver 执行普攻动作。
    /// 之后可改为动画事件或 Hitbox 事件驱动攻击生效与状态退出。
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

            if (_elapsedTime < Context.AttackDuration)
            {
                return;
            }

            Context.StateMachine.ChangeState(Context.HasMoveInput ? PlayerStateId.Move : PlayerStateId.Idle);
        }
    }
}
