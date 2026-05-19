namespace EndLink.Core
{
    /// <summary>
    /// 玩家状态接口。
    /// 状态只关心进入、每帧执行、退出三个生命周期，不继承 MonoBehaviour。
    /// </summary>
    public interface IPlayerState
    {
        /// <summary>
        /// 当前状态的唯一标识。
        /// </summary>
        PlayerStateId StateId { get; }

        /// <summary>
        /// 进入状态时调用一次。
        /// </summary>
        void Enter();

        /// <summary>
        /// 当前状态每帧调用。
        /// </summary>
        void Tick(float deltaTime);

        /// <summary>
        /// 离开状态时调用一次。
        /// </summary>
        void Exit();
    }
}
