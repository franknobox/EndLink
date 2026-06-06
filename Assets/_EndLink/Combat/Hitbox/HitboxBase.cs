using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace EndLink.Combat
{
    /// <summary>
    /// 通用 Hitbox 基类。
    /// 负责 Trigger 检测、Enemy Layer 过滤、重复命中去重、构造命中信息并通知目标。
    /// 大多数后续 Hitbox 可以直接使用它，特殊形态可以继承并重写虚方法。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class HitboxBase : MonoBehaviour
    {
        private const string EnemyLayerName = "Enemy";

        [Header("目标过滤")]
        [Tooltip("允许命中的目标 Layer。默认使用 Enemy Layer；后续敌人攻击玩家或特殊 Hitbox 可以在预制体或子类中改写。")]
        [SerializeField]
        private LayerMask targetLayerMask;

        [Header("命中参数")]
        [Tooltip("本 Hitbox 命中时造成的伤害值。")]
        [SerializeField, Min(0f)]
        private float damageAmount = 10f;

        [Tooltip("本 Hitbox 命中时造成的伤害类型。结构伤害偏物理/武器，运行伤害偏协议/能量/异常数据。")]
        [SerializeField]
        private CombatDamageType damageType = CombatDamageType.StructuralDamage;

        [Tooltip("本 Hitbox 命中时传递给目标的基础瞬时击退距离。最终位移还会乘以受击者 CharacterStats 的承受击退倍率。")]
        [SerializeField, Min(0f)]
        private float knockbackForce = 3f;

        [Tooltip("命中时施加到目标 CombatTagContainer 的战斗标签资产。新逻辑应优先使用它。")]
        [SerializeField]
        private CombatTagDefinition combatTagToApply;

        [Tooltip("战斗标签持续时间。小于等于 0 表示使用标签定义的默认持续时间。")]
        [SerializeField, Min(0f)]
        private float combatTagDuration;

        [Tooltip("命中时施加的战斗标签层数。最终会被标签定义的最大层数钳制。")]
        [SerializeField, Min(1)]
        private int combatTagStackCount = 1;

        [Header("生命周期")]
        [Tooltip("Hitbox 自动销毁时间。小于等于 0 表示不由 HitboxBase 按时间销毁。")]
        [InspectorName("Lifetime")]
        [SerializeField, Min(0f)]
        private float lifetime = 0.2f;

        [Header("事件")]
        [Tooltip("成功命中 Enemy Layer 且目标实现 IHitReceiver 后触发。可用于挂音效、特效或调试输出。")]
        [SerializeField]
        private HitboxUnityEvent onHit = new();

        private readonly HashSet<Collider> _hitColliders = new();
        private readonly HashSet<Transform> _hitTargets = new();
        private Collider _triggerCollider;
        private GameObject _owner;
        private CombatActionDefinition _actionDefinition;
        private float _enabledTime;

        /// <summary>本 Hitbox 的伤害值。</summary>
        public float DamageAmount => damageAmount;

        /// <summary>本 Hitbox 的伤害类型。</summary>
        public CombatDamageType DamageType => damageType;

        /// <summary>本 Hitbox 的基础瞬时击退距离。</summary>
        public float KnockbackForce => knockbackForce;

        /// <summary>本 Hitbox 命中时附加的战斗标签资产。</summary>
        public CombatTagDefinition CombatTagToApply => combatTagToApply;

        /// <summary>本 Hitbox 命中时附加的战斗标签持续时间。小于等于 0 表示使用标签定义的默认持续时间。</summary>
        public float CombatTagDuration => combatTagDuration;

        /// <summary>本 Hitbox 命中时附加的战斗标签层数。</summary>
        public int CombatTagStackCount => Mathf.Max(1, combatTagStackCount);

        /// <summary>Hitbox 自动销毁时间。小于等于 0 表示关闭基类时间销毁。</summary>
        public float Lifetime => lifetime;

        /// <summary>允许命中的目标 Layer。</summary>
        public LayerMask TargetLayerMask => targetLayerMask;

        /// <summary>成功命中事件。</summary>
        public HitboxUnityEvent OnHit => onHit;

        protected virtual void Awake()
        {
            _triggerCollider = GetComponent<Collider>();
            _triggerCollider.isTrigger = true;
            EnsureTargetLayerMask();
        }

        protected virtual void OnEnable()
        {
            ResetHitCache();
            _enabledTime = Time.time;
        }

        protected virtual void Update()
        {
            if (ShouldExpireByLifetime(_enabledTime, Time.time, lifetime))
            {
                Destroy(gameObject);
            }
        }

        protected virtual void Reset()
        {
            if (TryGetComponent(out Collider hitboxCollider))
            {
                hitboxCollider.isTrigger = true;
            }

            targetLayerMask = GetDefaultTargetLayerMask();
        }

        protected virtual void OnValidate()
        {
            damageAmount = Mathf.Max(0f, damageAmount);
            knockbackForce = Mathf.Max(0f, knockbackForce);
            combatTagDuration = Mathf.Max(0f, combatTagDuration);
            combatTagStackCount = Mathf.Max(1, combatTagStackCount);
            lifetime = Mathf.Max(0f, lifetime);

            if (TryGetComponent(out Collider hitboxCollider))
            {
                hitboxCollider.isTrigger = true;
            }

            EnsureTargetLayerMask();
        }

        /// <summary>
        /// 初始化 Hitbox 所属对象。通常由生成它的 PlayerCombatDriver 调用。
        /// </summary>
        public virtual void Initialize(GameObject owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// 运行时配置 Hitbox 参数，使用新的 CombatTagDefinition 标签通道。
        /// </summary>
        public void Configure(
            float damage,
            CombatDamageType type,
            float knockback,
            CombatTagDefinition combatTag,
            float tagDuration,
            int tagStackCount = 1,
            CombatActionDefinition actionDefinition = null)
        {
            damageAmount = Mathf.Max(0f, damage);
            damageType = type;
            knockbackForce = Mathf.Max(0f, knockback);
            combatTagToApply = combatTag;
            combatTagDuration = Mathf.Max(0f, tagDuration);
            combatTagStackCount = Mathf.Max(1, tagStackCount);
            _actionDefinition = actionDefinition;
        }

        /// <summary>
        /// 清空已命中过的 Collider 记录。
        /// 对象池复用 Hitbox 时需要在重新启用前清理命中缓存。
        /// </summary>
        public void ResetHitCache()
        {
            _hitColliders.Clear();
            _hitTargets.Clear();
        }

        /// <summary>
        /// 判断 Hitbox 是否已经达到生命周期。
        /// lifetime 小于等于 0 时表示关闭时间销毁。
        /// </summary>
        public static bool ShouldExpireByLifetime(float startTime, float currentTime, float lifetime)
        {
            return lifetime > 0f && currentTime - startTime >= lifetime;
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (!CanHit(other))
            {
                return;
            }

            if (!TryGetHitReceiver(other, out IHitReceiver receiver))
            {
                return;
            }

            _hitColliders.Add(other);
            if (CombatTargetUtility.TryResolve(other, out ICombatTarget combatTarget))
            {
                _hitTargets.Add(combatTarget.RootTransform);
            }

            HitboxHitInfo hitInfo = BuildHitInfo(other);
            CombatEventsBus.RaiseHitLanded(_owner, ResolveHitTarget(receiver, other), hitInfo);
            receiver.ReceiveHit(hitInfo);
            ApplyCombatTag(other);
            onHit.Invoke(other);
        }

        /// <summary>
        /// 是否允许命中该 Collider。子类可覆盖以增加阵营、无敌、命中次数等规则。
        /// </summary>
        protected virtual bool CanHit(Collider other)
        {
            if (other == null || !IsTargetLayerAllowed(other))
            {
                return false;
            }

            if (!IsCombatTargetable(other))
            {
                return false;
            }

            if (_hitColliders.Contains(other))
            {
                return false;
            }

            return !CombatTargetUtility.TryResolve(other, out ICombatTarget combatTarget)
                || !_hitTargets.Contains(combatTarget.RootTransform);
        }

        /// <summary>
        /// 从 Collider 上查找受击接口。默认向父节点查找，适配敌人多 Collider 结构。
        /// </summary>
        protected virtual bool TryGetHitReceiver(Collider other, out IHitReceiver receiver)
        {
            receiver = other.GetComponentInParent<IHitReceiver>();
            return receiver != null;
        }

        /// <summary>
        /// 构造命中信息。子类如需补充特殊数据，可覆盖此方法。
        /// </summary>
        protected virtual HitboxHitInfo BuildHitInfo(Collider other)
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 targetPoint = other.bounds.center;

            if (CombatTargetUtility.TryResolve(other, out ICombatTarget combatTarget))
            {
                hitPoint = combatTarget.GetClosestPoint(transform.position);
                targetPoint = combatTarget.LockPoint != null
                    ? combatTarget.LockPoint.position
                    : combatTarget.RootTransform.position;
            }

            Vector3 hitDirection = Vector3.ProjectOnPlane(
                targetPoint - transform.position,
                Vector3.up);

            if (hitDirection.sqrMagnitude <= 0.0001f)
            {
                hitDirection = transform.forward;
            }
            else
            {
                hitDirection.Normalize();
            }

            return new HitboxHitInfo(
                this,
                _owner,
                other,
                _actionDefinition,
                damageAmount,
                damageType,
                knockbackForce,
                combatTagToApply,
                combatTagDuration,
                combatTagStackCount,
                hitPoint,
                hitDirection);
        }

        private void ApplyCombatTag(Collider other)
        {
            if (combatTagToApply == null)
            {
                return;
            }

            ICombatTagReceiver tagReceiver = other.GetComponentInParent<ICombatTagReceiver>();
            tagReceiver?.AddTag(combatTagToApply, combatTagDuration, _owner, combatTagStackCount);
        }

        /// <summary>
        /// 判断 Collider 所在 Layer 是否允许命中。子类可覆盖以实现阵营、友伤或特殊目标规则。
        /// </summary>
        protected virtual bool IsTargetLayerAllowed(Collider other)
        {
            return other != null && ((1 << other.gameObject.layer) & targetLayerMask.value) != 0;
        }

        private void EnsureTargetLayerMask()
        {
            if (targetLayerMask.value == 0)
            {
                targetLayerMask = GetDefaultTargetLayerMask();
            }
        }

        private static LayerMask GetDefaultTargetLayerMask()
        {
            int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
            return enemyLayer >= 0 ? 1 << enemyLayer : 0;
        }

        private static GameObject ResolveHitTarget(IHitReceiver receiver, Collider fallbackCollider)
        {
            if (CombatTargetUtility.TryResolve(fallbackCollider, out ICombatTarget combatTarget)
                && combatTarget.RootTransform != null)
            {
                return combatTarget.RootTransform.gameObject;
            }

            if (receiver is Component receiverComponent)
            {
                return receiverComponent.gameObject;
            }

            return fallbackCollider != null ? fallbackCollider.gameObject : null;
        }

        private static bool IsCombatTargetable(Collider other)
        {
            return !CombatTargetUtility.TryResolve(other, out ICombatTarget combatTarget)
                || combatTarget.IsTargetable;
        }
    }

    /// <summary>
    /// Hitbox 命中 UnityEvent。
    /// 使用 Collider 参数，方便在 Inspector 中把命中的目标传给音效、特效或调试组件。
    /// </summary>
    [System.Serializable]
    public sealed class HitboxUnityEvent : UnityEvent<Collider>
    {
    }
}
