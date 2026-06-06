using System.Collections;
using EndLink.Combat;
using UnityEngine;
using UnityEngine.Events;

namespace EndLink.Enemies
{
    /// <summary>
    /// 正式敌人的生命与受击组件。
    /// 复用木桩敌人的血量、受伤、死亡、事件广播和白模调试反馈逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviour, IHitReceiver, IDamageable, ICombatTargetLifeState
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("血量")]
        [Tooltip("敌人最大生命值。")]
        [SerializeField, Min(1)]
        private int maxHealth = 100;

        [Tooltip("启用物体时是否自动恢复满血。方便反复进入 Play Mode 或重置战斗。")]
        [SerializeField]
        private bool resetHealthOnEnable = true;

        [Header("死亡处理")]
        [Tooltip("死亡后是否禁用自身及子物体上的非 Trigger Collider。第一版默认关闭，避免影响死亡表现观察。")]
        [SerializeField]
        private bool disableCollidersOnDeath;

        [Header("受击反馈")]
        [Tooltip("受击时闪烁的 MeshRenderer。为空时会自动查找自身或子物体。")]
        [SerializeField]
        private MeshRenderer feedbackRenderer;

        [Tooltip("受击时瞬间切换的颜色。")]
        [SerializeField]
        private Color hitColor = new Color(1f, 0.55f, 0.55f, 1f);

        [Tooltip("受击颜色保持时间。")]
        [SerializeField, Min(0.01f)]
        private float hitFlashDuration = 0.1f;

        [Tooltip("死亡后显示的颜色。")]
        [SerializeField]
        private Color deadColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        [Header("调试显示")]
        [Tooltip("受伤、死亡时是否打印 Debug.Log。")]
        [SerializeField]
        private bool logHits = true;

        [Tooltip("是否把当前血量显示到 GameObject 名字上。")]
        [SerializeField]
        private bool showHealthInName = true;

        [Header("事件")]
        [Tooltip("受到有效伤害时触发。参数依次为：实际伤害值、命中标签。")]
        [SerializeField]
        private EnemyHealthDamagedEvent onDamaged = new();

        [Tooltip("生命值首次降到 0 时触发。")]
        [SerializeField]
        private UnityEvent onDead = new();

        private Collider[] _ownedColliders = System.Array.Empty<Collider>();
        private MaterialPropertyBlock _propertyBlock;
        private Color _originalColor = Color.white;
        private Coroutine _flashCoroutine;
        private string _originalName;
        private int _currentHealth;
        private int _ownedColliderCount;
        private bool _isDead;
        private bool _componentsCached;

        /// <summary>最大生命值。</summary>
        public int MaxHealth => maxHealth;

        /// <summary>当前生命值。</summary>
        public int CurrentHealth => _currentHealth;

        /// <summary>是否已经死亡。</summary>
        public bool IsDead => _isDead;

        /// <summary>供 CombatTarget 读取的存活状态。</summary>
        public bool IsAlive => !_isDead;

        /// <summary>受伤事件。</summary>
        public EnemyHealthDamagedEvent OnDamaged => onDamaged;

        /// <summary>死亡事件。</summary>
        public UnityEvent OnDead => onDead;

        private void Awake()
        {
            CacheComponents();
            ResetHealth();
        }

        private void OnEnable()
        {
            if (resetHealthOnEnable)
            {
                ResetHealth();
            }
        }

        private void Reset()
        {
            feedbackRenderer = GetComponentInChildren<MeshRenderer>();
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            hitFlashDuration = Mathf.Max(0.01f, hitFlashDuration);
        }

        /// <summary>
        /// 接收 Hitbox 的完整命中信息，并转为生命伤害处理。
        /// </summary>
        public void ReceiveHit(HitboxHitInfo hitInfo)
        {
            DamageContext context = DamageContext.FromHit(hitInfo, gameObject);
            int appliedDamage = ApplyDamage(DamageCalculator.Calculate(context));
            if (appliedDamage > 0)
            {
                CombatKnockback.TryApply(gameObject, hitInfo.HitDirection, hitInfo.KnockbackForce);
            }
        }

        /// <summary>
        /// 兼容旧调用：接收简单伤害接口，默认按结构伤害处理。
        /// </summary>
        public void TakeDamage(int damage, CombatTagDefinition tag)
        {
            ApplyDamage(damage, CombatDamageType.StructuralDamage, tag, null);
        }

        /// <summary>
        /// 接收简单伤害接口。
        /// </summary>
        public void TakeDamage(int damage, CombatDamageType damageType, CombatTagDefinition tag)
        {
            ApplyDamage(damage, damageType, tag, null);
        }

        /// <summary>
        /// 重置生命值和死亡状态。
        /// </summary>
        public void ResetHealth()
        {
            CacheComponents();

            _currentHealth = maxHealth;
            _isDead = false;

            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }

            RestoreColliders();
            SetBaseColor(_originalColor);
            UpdateDebugDisplay();
        }

        private void CacheComponents()
        {
            if (_componentsCached)
            {
                return;
            }

            if (feedbackRenderer == null)
            {
                feedbackRenderer = GetComponentInChildren<MeshRenderer>();
            }

            _propertyBlock = new MaterialPropertyBlock();
            _originalColor = GetOriginalBaseColor();
            _originalName = gameObject.name;
            _ownedColliders = GetComponentsInChildren<Collider>(false);
            _ownedColliderCount = _ownedColliders.Length;
            _componentsCached = true;
        }

        private void ApplyDamage(int damage, CombatDamageType damageType, CombatTagDefinition tag, GameObject source)
        {
            DamageContext context = DamageContext.Direct(source, gameObject, damage, damageType, tag);
            ApplyDamage(DamageCalculator.Calculate(context));
        }

        private int ApplyDamage(DamageResult damageResult)
        {
            if (_isDead)
            {
                return 0;
            }

            int appliedDamage = Mathf.Max(0, damageResult.FinalDamage);
            if (appliedDamage <= 0)
            {
                return 0;
            }

            _currentHealth = Mathf.Max(0, _currentHealth - appliedDamage);

            if (logHits)
            {
                Debug.Log(
                    $"Enemy took {appliedDamage} {damageResult.DamageType} damage, tag: {GetTagLogText(damageResult.CombatTag)}, hp: {_currentHealth}/{maxHealth}",
                    this);
            }

            onDamaged.Invoke(appliedDamage, damageResult.CombatTag);
            CombatEventsBus.RaiseDamaged(
                damageResult.Source,
                gameObject,
                appliedDamage,
                damageResult.DamageType,
                damageResult.CombatTag);
            UpdateDebugDisplay();

            if (_currentHealth <= 0)
            {
                Die(damageResult.Source);
                return appliedDamage;
            }

            PlayHitFlash();
            return appliedDamage;
        }

        private void Die(GameObject source)
        {
            if (_isDead)
            {
                return;
            }

            _isDead = true;

            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }

            if (disableCollidersOnDeath)
            {
                SetOwnedCollidersEnabled(false);
            }

            SetBaseColor(deadColor);
            UpdateDebugDisplay();

            if (logHits)
            {
                Debug.Log("Enemy died.", this);
            }

            onDead.Invoke();
            CombatEventsBus.RaiseDead(source, gameObject);
        }

        private IEnumerator FlashHitColor()
        {
            SetBaseColor(hitColor);
            yield return new WaitForSeconds(hitFlashDuration);

            if (!_isDead)
            {
                SetBaseColor(_originalColor);
            }

            _flashCoroutine = null;
        }

        private void PlayHitFlash()
        {
            if (feedbackRenderer == null)
            {
                return;
            }

            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
            }

            _flashCoroutine = StartCoroutine(FlashHitColor());
        }

        private void RestoreColliders()
        {
            SetOwnedCollidersEnabled(true);
        }

        private void SetOwnedCollidersEnabled(bool enabled)
        {
            for (int i = 0; i < _ownedColliderCount; i++)
            {
                Collider ownedCollider = _ownedColliders[i];
                if (ownedCollider != null && !ownedCollider.isTrigger)
                {
                    ownedCollider.enabled = enabled;
                }
            }
        }

        private void UpdateDebugDisplay()
        {
            if (!showHealthInName || string.IsNullOrEmpty(_originalName))
            {
                return;
            }

            string stateText = _isDead ? "Dead" : $"{_currentHealth}/{maxHealth}";
            gameObject.name = $"{_originalName} [{stateText}]";
        }

        private Color GetOriginalBaseColor()
        {
            Material sharedMaterial = feedbackRenderer != null ? feedbackRenderer.sharedMaterial : null;

            if (sharedMaterial != null && sharedMaterial.HasProperty(BaseColorId))
            {
                return sharedMaterial.GetColor(BaseColorId);
            }

            return Color.white;
        }

        private void SetBaseColor(Color color)
        {
            if (feedbackRenderer == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            feedbackRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, color);
            feedbackRenderer.SetPropertyBlock(_propertyBlock);
        }

        private static string GetTagLogText(CombatTagDefinition tag)
        {
            return tag != null ? tag.TagId : "None";
        }
    }

    /// <summary>
    /// 正式敌人受伤事件。
    /// 参数依次为：实际伤害值、命中战斗标签。
    /// </summary>
    [System.Serializable]
    public sealed class EnemyHealthDamagedEvent : UnityEvent<int, CombatTagDefinition>
    {
    }
}
