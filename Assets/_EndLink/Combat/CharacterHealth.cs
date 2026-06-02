using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace EndLink.Combat
{
    /// <summary>
    /// 通用角色生命组件。
    /// 只负责生命值、受击、死亡、目标有效性、基础受击反馈和战斗事件播报。
    /// 不直接切换玩家、队友或敌人的状态机，具体角色通过各自的桥接脚本订阅事件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterHealth : MonoBehaviour, IHitReceiver, IDamageable, ICombatTarget
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("生命值")]
        [Tooltip("最大生命值。")]
        [SerializeField, Min(1)]
        private int maxHealth = 100;

        [Tooltip("组件启用时是否自动恢复满血。方便反复进入 Play Mode 或重置训练场。")]
        [SerializeField]
        private bool resetHealthOnEnable = true;

        [Header("目标有效性")]
        [Tooltip("死亡后是否不再作为锁定、AI 搜索和 Hitbox 命中的有效目标。")]
        [SerializeField]
        private bool untargetableOnDeath = true;

        [Tooltip("死亡后是否禁用自身和子物体上的非 Trigger Collider。默认关闭，避免影响死亡表现观察。")]
        [SerializeField]
        private bool disableCollidersOnDeath;

        [Header("受击反馈")]
        [Tooltip("受击闪色的 MeshRenderer。为空时会自动查找自身或子物体。")]
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

        [Header("调试")]
        [Tooltip("受击、治疗和死亡时是否打印 Debug.Log。")]
        [SerializeField]
        private bool logHealthChanges = true;

        [Tooltip("是否把当前血量显示到 GameObject 名字上。仅用于白模调试。")]
        [SerializeField]
        private bool showHealthInName;

        [Header("事件")]
        [Tooltip("生命值变化时触发。参数依次为：当前生命值、最大生命值。")]
        [SerializeField]
        private CharacterHealthChangedEvent onHealthChanged = new();

        [Tooltip("受到有效伤害时触发。参数依次为：实际伤害值、命中标签。")]
        [SerializeField]
        private CharacterHealthDamagedEvent onDamaged = new();

        [Tooltip("获得有效治疗时触发。参数为：实际治疗值。")]
        [SerializeField]
        private CharacterHealedEvent onHealed = new();

        [Tooltip("生命值首次降到 0 时触发。")]
        [SerializeField]
        private UnityEvent onDead = new();

        private Collider[] _ownedColliders = Array.Empty<Collider>();
        private MaterialPropertyBlock _propertyBlock;
        private Color _originalColor = Color.white;
        private Coroutine _flashCoroutine;
        private string _originalName;
        private int _currentHealth;
        private int _ownedColliderCount;
        private bool _isDead;
        private bool _isTargetable = true;
        private bool _componentsCached;
        private float _temporaryInvincibleUntilTime;

        /// <summary>最大生命值。</summary>
        public int MaxHealth => maxHealth;

        /// <summary>当前生命值。</summary>
        public int CurrentHealth => _currentHealth;

        /// <summary>是否已经死亡。</summary>
        public bool IsDead => _isDead;

        /// <summary>当前是否可作为战斗目标。</summary>
        public bool IsTargetable => _isTargetable;

        /// <summary>当前是否处于临时免伤窗口。</summary>
        public bool IsTemporaryInvincible => Time.time < _temporaryInvincibleUntilTime;

        /// <summary>用于锁定、AI 和距离计算的目标 Transform。</summary>
        public Transform TargetTransform => transform;

        /// <summary>生命值变化 UnityEvent。</summary>
        public CharacterHealthChangedEvent OnHealthChanged => onHealthChanged;

        /// <summary>受击 UnityEvent。</summary>
        public CharacterHealthDamagedEvent OnDamaged => onDamaged;

        /// <summary>治疗 UnityEvent。</summary>
        public CharacterHealedEvent OnHealed => onHealed;

        /// <summary>死亡 UnityEvent。</summary>
        public UnityEvent OnDead => onDead;

        /// <summary>生命值变化的代码事件，桥接脚本用它拿到完整上下文。</summary>
        public event Action<CharacterHealthChangeInfo> HealthChanged;

        /// <summary>受击的代码事件，桥接脚本用它拿到攻击来源。</summary>
        public event Action<CharacterHealthDamageInfo> Damaged;

        /// <summary>治疗的代码事件。</summary>
        public event Action<CharacterHealthHealInfo> Healed;

        /// <summary>死亡的代码事件。</summary>
        public event Action<CharacterHealthDeathInfo> Died;

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
            ApplyDamage(Mathf.RoundToInt(hitInfo.DamageAmount), hitInfo.CombatTagToApply, hitInfo.Owner);
        }

        /// <summary>
        /// 接收简单伤害接口。
        /// </summary>
        public void TakeDamage(int damage, CombatTagDefinition tag)
        {
            ApplyDamage(damage, tag, null);
        }

        /// <summary>
        /// 设置临时免伤窗口。
        /// 主要供闪避、出生保护或后续特殊状态使用；不会改变目标有效性，也不会阻止治疗。
        /// </summary>
        public void SetTemporaryInvincible(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            _temporaryInvincibleUntilTime = Mathf.Max(_temporaryInvincibleUntilTime, Time.time + duration);
        }

        /// <summary>
        /// 直接施加伤害。返回实际扣除的生命值。
        /// </summary>
        public int ApplyDamage(int damage, CombatTagDefinition tag, GameObject source)
        {
            if (_isDead || !_isTargetable || IsTemporaryInvincible)
            {
                return 0;
            }

            int appliedDamage = Mathf.Max(0, damage);
            if (appliedDamage <= 0)
            {
                return 0;
            }

            int previousHealth = _currentHealth;
            _currentHealth = Mathf.Max(0, _currentHealth - appliedDamage);
            int actualDamage = previousHealth - _currentHealth;

            if (actualDamage <= 0)
            {
                return 0;
            }

            if (logHealthChanges)
            {
                Debug.Log(
                    $"{name} took {actualDamage} damage, tag: {GetTagLogText(tag)}, hp: {_currentHealth}/{maxHealth}",
                    this);
            }

            CharacterHealthDamageInfo damageInfo = new CharacterHealthDamageInfo(this, actualDamage, tag, source);
            onDamaged.Invoke(actualDamage, tag);
            Damaged?.Invoke(damageInfo);
            CombatEventsBus.RaiseDamaged(source, gameObject, actualDamage, tag);
            NotifyHealthChanged(source);

            if (_currentHealth <= 0)
            {
                Die(source);
                return actualDamage;
            }

            PlayHitFlash();
            return actualDamage;
        }

        /// <summary>
        /// 恢复生命值。当前不处理复活；死亡后治疗会被忽略。
        /// </summary>
        public int Heal(int amount)
        {
            if (_isDead)
            {
                return 0;
            }

            int appliedHeal = Mathf.Max(0, amount);
            if (appliedHeal <= 0 || _currentHealth >= maxHealth)
            {
                return 0;
            }

            int previousHealth = _currentHealth;
            _currentHealth = Mathf.Min(maxHealth, _currentHealth + appliedHeal);
            int actualHeal = _currentHealth - previousHealth;

            if (logHealthChanges)
            {
                Debug.Log($"{name} healed {actualHeal}, hp: {_currentHealth}/{maxHealth}", this);
            }

            CharacterHealthHealInfo healInfo = new CharacterHealthHealInfo(this, actualHeal);
            onHealed.Invoke(actualHeal);
            Healed?.Invoke(healInfo);
            NotifyHealthChanged(null);
            return actualHeal;
        }

        /// <summary>
        /// 重置生命值、死亡状态和目标有效性。
        /// </summary>
        public void ResetHealth()
        {
            CacheComponents();

            _currentHealth = maxHealth;
            _isDead = false;
            _isTargetable = true;
            _temporaryInvincibleUntilTime = 0f;

            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }

            SetOwnedCollidersEnabled(true);
            SetBaseColor(_originalColor);
            NotifyHealthChanged(null);
        }

        /// <summary>
        /// 手动设置目标有效性。后续锁定和 AI 搜索会读取该值。
        /// </summary>
        public void SetTargetable(bool isTargetable)
        {
            _isTargetable = isTargetable && !_isDead;
        }

        private void Die(GameObject source)
        {
            if (_isDead)
            {
                return;
            }

            _isDead = true;

            if (untargetableOnDeath)
            {
                _isTargetable = false;
            }

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

            if (logHealthChanges)
            {
                Debug.Log($"{name} died.", this);
            }

            CharacterHealthDeathInfo deathInfo = new CharacterHealthDeathInfo(this, source);
            onDead.Invoke();
            Died?.Invoke(deathInfo);
            CombatEventsBus.RaiseDead(source, gameObject);
        }

        private void NotifyHealthChanged(GameObject source)
        {
            UpdateDebugDisplay();
            CharacterHealthChangeInfo changeInfo = new CharacterHealthChangeInfo(this, _currentHealth, maxHealth, source);
            onHealthChanged.Invoke(_currentHealth, maxHealth);
            HealthChanged?.Invoke(changeInfo);
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

    /// <summary>生命值变化上下文。</summary>
    public readonly struct CharacterHealthChangeInfo
    {
        public CharacterHealthChangeInfo(CharacterHealth health, int currentHealth, int maxHealth, GameObject source)
        {
            Health = health;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            Source = source;
        }

        public CharacterHealth Health { get; }

        public int CurrentHealth { get; }

        public int MaxHealth { get; }

        public GameObject Source { get; }
    }

    /// <summary>受击上下文。</summary>
    public readonly struct CharacterHealthDamageInfo
    {
        public CharacterHealthDamageInfo(CharacterHealth health, int damage, CombatTagDefinition tag, GameObject source)
        {
            Health = health;
            Damage = damage;
            Tag = tag;
            Source = source;
        }

        public CharacterHealth Health { get; }

        public int Damage { get; }

        public CombatTagDefinition Tag { get; }

        public GameObject Source { get; }
    }

    /// <summary>治疗上下文。</summary>
    public readonly struct CharacterHealthHealInfo
    {
        public CharacterHealthHealInfo(CharacterHealth health, int healAmount)
        {
            Health = health;
            HealAmount = healAmount;
        }

        public CharacterHealth Health { get; }

        public int HealAmount { get; }
    }

    /// <summary>死亡上下文。</summary>
    public readonly struct CharacterHealthDeathInfo
    {
        public CharacterHealthDeathInfo(CharacterHealth health, GameObject source)
        {
            Health = health;
            Source = source;
        }

        public CharacterHealth Health { get; }

        public GameObject Source { get; }
    }

    /// <summary>通用生命值变化事件。参数依次为：当前生命值、最大生命值。</summary>
    [Serializable]
    public sealed class CharacterHealthChangedEvent : UnityEvent<int, int>
    {
    }

    /// <summary>通用受击事件。参数依次为：实际伤害值、命中标签。</summary>
    [Serializable]
    public sealed class CharacterHealthDamagedEvent : UnityEvent<int, CombatTagDefinition>
    {
    }

    /// <summary>通用治疗事件。参数为：实际治疗值。</summary>
    [Serializable]
    public sealed class CharacterHealedEvent : UnityEvent<int>
    {
    }
}
