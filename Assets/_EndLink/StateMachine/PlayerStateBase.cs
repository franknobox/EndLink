namespace EndLink.Core
{
    /// <summary>
    /// 玩家状态基类。
    /// 放置通用依赖和默认生命周期，具体状态只覆盖自己需要的逻辑。
    /// </summary>
    public abstract class PlayerStateBase : IPlayerState
    {
        public const float MoveInputDeadZoneSqr = 0.0001f;

        protected PlayerStateBase(PlayerStateContext context)
        {
            Context = context;
        }

        public abstract PlayerStateId StateId { get; }

        protected PlayerStateContext Context { get; }

        public virtual void Enter()
        {
        }

        public abstract void Tick(float deltaTime);

        public virtual void Exit()
        {
        }
    }
}
