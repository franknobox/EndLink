namespace EndLink.Combat
{
    /// <summary>
    /// 命中进入生命结算前的轻量拦截接口。
    /// 格挡、弹反和后续特殊护盾可以通过它修改本次伤害倍率与击退结果。
    /// </summary>
    public interface IHitInterceptor
    {
        HitInterception InterceptHit(HitboxHitInfo hitInfo);
    }

    /// <summary>一次命中拦截结果。</summary>
    public readonly struct HitInterception
    {
        public HitInterception(bool intercepted, float damageMultiplier, bool allowKnockback)
        {
            Intercepted = intercepted;
            DamageMultiplier = UnityEngine.Mathf.Max(0f, damageMultiplier);
            AllowKnockback = allowKnockback;
        }

        public bool Intercepted { get; }

        public float DamageMultiplier { get; }

        public bool AllowKnockback { get; }

        public static HitInterception Continue => new(false, 1f, true);
    }
}
