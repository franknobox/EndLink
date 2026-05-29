namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人状态接口。
    /// 每个状态只处理自己的生命周期，不直接持有组件查找逻辑。
    /// </summary>
    public interface IEnemyState
    {
        /// <summary>当前状态标识。</summary>
        EnemyStateId StateId { get; }

        /// <summary>进入状态时调用一次。</summary>
        void Enter();

        /// <summary>每帧由 EnemyStateMachine 驱动。</summary>
        void Tick(float deltaTime);

        /// <summary>离开状态时调用一次。</summary>
        void Exit();
    }
}
