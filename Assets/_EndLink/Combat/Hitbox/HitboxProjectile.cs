using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 直线飞行型 Hitbox。
    /// 继承 HitboxBase 以复用目标过滤、伤害、标签、命中去重、生命周期和 CombatEventsBus 播报。
    /// 适合胶囊白模阶段的远程技能、飞弹、剑气等测试表现。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitboxProjectile : HitboxBase
    {
        [Header("飞行")]
        [Tooltip("飞行速度，单位米/秒。Projectile 会沿自身 Z 轴正方向移动。")]
        [SerializeField, Min(0f)]
        private float speed = 12f;

        [Tooltip("最大飞行距离。小于等于 0 表示不按距离销毁，时间生命周期仅由 Projectile 自身的 Lifetime 控制。")]
        [SerializeField, Min(0f)]
        private float maxDistance = 12f;

        [Tooltip("成功命中目标后是否立即销毁。关闭后可用于穿透型远程 Hitbox。")]
        [SerializeField]
        private bool destroyOnHit = true;

        private Vector3 _spawnPosition;

        /// <summary>飞行速度，单位米/秒。</summary>
        public float Speed => speed;

        /// <summary>最大飞行距离，小于等于 0 表示不按距离销毁。</summary>
        public float MaxDistance => maxDistance;

        /// <summary>成功命中目标后是否立即销毁。</summary>
        public bool DestroyOnHit => destroyOnHit;

        protected override void OnEnable()
        {
            base.OnEnable();
            _spawnPosition = transform.position;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            speed = Mathf.Max(0f, speed);
            maxDistance = Mathf.Max(0f, maxDistance);
        }

        /// <summary>
        /// 远程弹体的寿命由 prefab 自身配置决定，不使用动作 ActiveTime 覆盖。
        /// startup 只负责控制“何时发射”，发射后的飞行寿命与距离规则独立处理。
        /// </summary>
        protected override float ResolveRuntimeLifetimeOverride(CombatActionDefinition actionDefinition)
        {
            return -1f;
        }

        protected override void Update()
        {
            base.Update();

            float deltaTime = Time.deltaTime;

            if (deltaTime <= 0f)
            {
                return;
            }

            transform.position = CalculateNextPosition(transform.position, transform.forward, speed, deltaTime);

            if (ShouldExpireByDistance(_spawnPosition, transform.position, maxDistance))
            {
                Destroy(gameObject);
            }
        }

        protected override void OnTriggerEnter(Collider other)
        {
            bool shouldDestroyAfterHit = destroyOnHit && CanHit(other) && TryGetHitReceiver(other, out _);

            base.OnTriggerEnter(other);

            if (shouldDestroyAfterHit || (destroyOnHit && ObjectInteractionAcceptedOnLastTrigger))
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 根据当前位置、前方、速度和 deltaTime 计算下一帧位置。
        /// 独立成静态方法，方便测试并避免把移动公式散落在 Update 中。
        /// </summary>
        public static Vector3 CalculateNextPosition(Vector3 currentPosition, Vector3 forward, float speed, float deltaTime)
        {
            Vector3 moveDirection = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            return currentPosition + moveDirection * (Mathf.Max(0f, speed) * Mathf.Max(0f, deltaTime));
        }

        /// <summary>
        /// 判断是否已经达到最大飞行距离。
        /// maxDistance 小于等于 0 时表示关闭距离销毁。
        /// </summary>
        public static bool ShouldExpireByDistance(Vector3 spawnPosition, Vector3 currentPosition, float maxDistance)
        {
            if (maxDistance <= 0f)
            {
                return false;
            }

            return (currentPosition - spawnPosition).sqrMagnitude >= maxDistance * maxDistance;
        }
    }
}
