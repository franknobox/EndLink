namespace EndLink.Combat
{
    /// <summary>
    /// 可受伤对象接口。
    /// 只关心伤害数值和命中标签，适合给血量系统、木桩、可破坏物使用。
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// 接收伤害和命中标签。
        /// </summary>
        void TakeDamage(int damage, string tag);
    }
}
