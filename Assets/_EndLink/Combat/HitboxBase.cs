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

        [Header("命中参数")]
        [Tooltip("本 Hitbox 命中时造成的伤害值。")]
        [SerializeField, Min(0f)]
        private float damageAmount = 10f;

        [Tooltip("本 Hitbox 命中时传递给目标的击退力。实际如何击退由目标实现 IHitReceiver 时决定。")]
        [SerializeField, Min(0f)]
        private float knockbackForce = 3f;

        [Tooltip("命中时挂载到目标的标签，例如 Break。早期使用字符串，后续可替换为统一 GameplayTag。")]
        [SerializeField]
        private string tagToApply = "Break";

        [Header("事件")]
        [Tooltip("成功命中 Enemy Layer 且目标实现 IHitReceiver 后触发。可用于挂音效、特效或调试输出。")]
        [SerializeField]
        private HitboxUnityEvent onHit = new();

        private readonly HashSet<Collider> _hitColliders = new();
        private Collider _triggerCollider;
        private int _enemyLayer;
        private GameObject _owner;

        /// <summary>本 Hitbox 的伤害值。</summary>
        public float DamageAmount => damageAmount;

        /// <summary>本 Hitbox 的击退力。</summary>
        public float KnockbackForce => knockbackForce;

        /// <summary>本 Hitbox 命中时附加的标签。</summary>
        public string TagToApply => tagToApply;

        /// <summary>成功命中事件。</summary>
        public HitboxUnityEvent OnHit => onHit;

        protected virtual void Awake()
        {
            _triggerCollider = GetComponent<Collider>();
            _triggerCollider.isTrigger = true;
            _enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
        }

        protected virtual void OnEnable()
        {
            ResetHitCache();
        }

        protected virtual void Reset()
        {
            if (TryGetComponent(out Collider hitboxCollider))
            {
                hitboxCollider.isTrigger = true;
            }
        }

        protected virtual void OnValidate()
        {
            damageAmount = Mathf.Max(0f, damageAmount);
            knockbackForce = Mathf.Max(0f, knockbackForce);

            if (TryGetComponent(out Collider hitboxCollider))
            {
                hitboxCollider.isTrigger = true;
            }
        }

        /// <summary>
        /// 初始化 Hitbox 所属对象。通常由生成它的 PlayerCombatDriver 调用。
        /// </summary>
        public virtual void Initialize(GameObject owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// 运行时配置 Hitbox 参数，方便技能数据或测试代码覆盖 prefab 默认值。
        /// </summary>
        public void Configure(float damage, float knockback, string hitTag)
        {
            damageAmount = Mathf.Max(0f, damage);
            knockbackForce = Mathf.Max(0f, knockback);
            tagToApply = hitTag;
        }

        /// <summary>
        /// 清空已命中过的 Collider 记录。
        /// 对象池复用 Hitbox 时需要在重新启用前清理命中缓存。
        /// </summary>
        public void ResetHitCache()
        {
            _hitColliders.Clear();
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

            HitboxHitInfo hitInfo = BuildHitInfo(other);
            receiver.ReceiveHit(hitInfo);
            onHit.Invoke(other);
        }

        /// <summary>
        /// 是否允许命中该 Collider。子类可覆盖以增加阵营、无敌、命中次数等规则。
        /// </summary>
        protected virtual bool CanHit(Collider other)
        {
            if (other == null || other.gameObject.layer != _enemyLayer)
            {
                return false;
            }

            return !_hitColliders.Contains(other);
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
            Vector3 targetCenter = other.bounds.center;
            Vector3 hitDirection = Vector3.ProjectOnPlane(targetCenter - transform.position, Vector3.up);

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
                damageAmount,
                knockbackForce,
                tagToApply,
                hitPoint,
                hitDirection);
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
