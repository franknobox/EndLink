using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 玩家死亡状态。
    /// 这是当前状态机的终止状态：不再响应移动、攻击或技能输入，只保留零输入移动 Tick，
    /// 让 CharacterController 继续处理重力和贴地。
    /// </summary>
    public sealed class PlayerDeadState : PlayerStateBase
    {
        public PlayerDeadState(PlayerStateContext context) : base(context)
        {
        }

        public override PlayerStateId StateId => PlayerStateId.Dead;

        public override void Tick(float deltaTime)
        {
            Context.Controller.TickMovement(Vector2.zero, deltaTime);
        }
    }
}
