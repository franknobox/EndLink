namespace EndLink.Combat
{
    /// <summary>
    /// 可被 Hitbox 命中的对象需要实现的接口。
    /// 敌人、可破坏物、训练木桩都可以实现它来接收伤害、击退和命中标签。
    /// </summary>
    public interface IHitReceiver
    {
        /// <summary>
        /// 接收一次命中信息。
        /// </summary>
        void ReceiveHit(HitboxHitInfo hitInfo);
    }
}
