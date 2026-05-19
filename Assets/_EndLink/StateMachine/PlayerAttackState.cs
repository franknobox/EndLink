using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 玩家攻击状态。
    /// 胶囊白模阶段先用固定时间表示一次攻击过程。后续可以在 Enter 中触发近战波表现，
    /// 再通过动画事件或 Hitbox 事件驱动攻击生效与状态退出。
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
            Context.CombatDriver.ExecuteAttack();
        }

        public override void Tick(float deltaTime)
        {
            _elapsedTime += deltaTime;

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
