namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人战斗大状态。
    /// 第一版不执行具体行为；后续行为树会挂在这里，负责追击、站位、攻击和技能等细节。
    /// </summary>
    public sealed class EnemyCombatState : EnemyStateBase
    {
        public EnemyCombatState(EnemyStateContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public override EnemyStateId StateId => EnemyStateId.Combat;

        /// <inheritdoc />
        public override void Tick(float deltaTime)
        {
        }
    }
}
