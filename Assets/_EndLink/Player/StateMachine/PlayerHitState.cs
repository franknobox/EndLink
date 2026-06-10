using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 玩家受击状态。
    /// 当前用于表示短暂硬直窗口：受击期间削弱或禁止移动输入，并阻止攻击、技能等主动行为。
    /// </summary>
    public sealed class PlayerHitState : PlayerStateBase
    {
        private float _elapsedTime;

        public PlayerHitState(PlayerStateContext context) : base(context)
        {
        }

        public override PlayerStateId StateId => PlayerStateId.Hit;

        public override void Enter()
        {
            _elapsedTime = 0f;
        }

        public override void Tick(float deltaTime)
        {
            _elapsedTime += deltaTime;
            Context.ConsumeJumpPressed();

            Vector2 hitMoveInput = Context.InputReader.MoveInput * Context.HitMoveInputScale;
            Context.Controller.TickMovement(hitMoveInput, deltaTime);

            if (_elapsedTime < Context.HitDuration)
            {
                return;
            }

            Context.StateMachine.ChangeState(Context.HasMoveInput ? PlayerStateId.Move : PlayerStateId.Idle);
        }
    }
}
