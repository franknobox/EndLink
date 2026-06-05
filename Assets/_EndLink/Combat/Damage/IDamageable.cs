namespace EndLink.Combat
{
    /// <summary>
    /// 可受伤对象接口。
    /// 只关心伤害数值和战斗标签，适合给血量系统、木桩、可破坏物使用。
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// 接收伤害、伤害类型和命中战斗标签。
        /// </summary>
        void TakeDamage(int damage, CombatDamageType damageType, CombatTagDefinition tag);
    }
}
