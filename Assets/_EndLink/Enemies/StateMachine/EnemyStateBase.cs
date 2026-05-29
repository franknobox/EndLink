namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人状态基类。
    /// 只保存共享上下文和默认生命周期，具体状态只写自己关心的逻辑。
    /// </summary>
    public abstract class EnemyStateBase : IEnemyState
    {
        protected EnemyStateBase(EnemyStateContext context)
        {
            Context = context;
        }

        /// <inheritdoc />
        public abstract EnemyStateId StateId { get; }

        /// <summary>敌人状态共享上下文。</summary>
        protected EnemyStateContext Context { get; }

        /// <inheritdoc />
        public virtual void Enter()
        {
        }

        /// <inheritdoc />
        public abstract void Tick(float deltaTime);

        /// <inheritdoc />
        public virtual void Exit()
        {
        }
    }
}
