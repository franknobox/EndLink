using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace EndLink.Combat
{
    /// <summary>
    /// 通用角色生命组件。
    /// 只负责生命值、受击、死亡、基础受击反馈和战斗事件播报。
    /// 不直接切换玩家、队友或敌人的状态机，具体角色通过各自的桥接脚本订阅事件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterHealth : MonoBehaviour, IHitReceiver, IDamageable, ICombatTargetLifeState
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("生命值")]
        [Tooltip("最大生命值。")]
        [SerializeField, Min(1)]
        private int maxHealth = 100;

        [Tooltip("组件启用时是否自动恢复满血。方便反复进入 Play Mode 或重置训练场。")]
        [SerializeField]
        private bool resetHealthOnEnable = true;

        [Header("死亡处理")]
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
        private readonly List<IHitInterceptor> _hitInterceptors = new();
        private MaterialPropertyBlock _propertyBlock;
        private Color _originalColor = Color.white;
        private Coroutine _flashCoroutine;
        private string _originalName;
        private int _currentHealth;
        private int _ownedColliderCount;
        private bool _isDead;
        private bool _componentsCached;
        private float _temporaryInvincibleUntilTime;

        /// <summary>最大生命值。</summary>
        public int MaxHealth => maxHealth;

        /// <summary>当前生命值。</summary>
        public int CurrentHealth => _currentHealth;

        /// <summary>是否已经死亡。</summary>
        public bool IsDead => _isDead;

        /// <summary>供 CombatTarget 读取的存活状态。</summary>
        public bool IsAlive => !_isDead;

        /// <summary>当前是否处于临时免伤窗口。</summary>
        public bool IsTemporaryInvincible => Time.time < _temporaryInvincibleUntilTime;

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

        /// <summary>
        /// 注册一个命中拦截器。
        /// 格挡、临时护盾和短暂无敌等运行时能力应在启用时注册；重复注册会被忽略。
        /// </summary>
        public void RegisterHitInterceptor(IHitInterceptor interceptor)
        {
            if (!IsHitInterceptorValid(interceptor) || _hitInterceptors.Contains(interceptor))
            {
                return;
            }

            _hitInterceptors.Add(interceptor);
        }

        /// <summary>
        /// 取消注册一个命中拦截器。
        /// 运行时能力应在禁用或移除时调用，避免继续参与后续命中结算。
        /// </summary>
        public void UnregisterHitInterceptor(IHitInterceptor interceptor)
        {
            if (interceptor != null)
            {
                _hitInterceptors.Remove(interceptor);
            }
        }

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
            DamageResult damageResult = DamageCalculator.Calculate(context);
            HitInterception interception = ResolveHitInterception(hitInfo);
            if (interception.Intercepted)
            {
                int interceptedDamage = Mathf.RoundToInt(damageResult.FinalDamage * interception.DamageMultiplier);
                damageResult = new DamageResult(
                    damageResult.Context,
                    interceptedDamage,
                    damageResult.IsCritical,
                    true,
                    damageResult.WasDodged);
            }

            int appliedDamage = ApplyDamage(damageResult);
            if (appliedDamage > 0 && (!interception.Intercepted || interception.AllowKnockback))
            {
                CombatKnockback.TryApply(gameObject, hitInfo.HitDirection, hitInfo.KnockbackForce);
            }
        }

        /// <summary>
        /// 接收简单伤害接口，默认按结构伤害处理。
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
        /// 设置临时免伤窗口。
        /// 主要供闪避、出生保护或特殊状态扩展使用；不会改变目标有效性，也不会阻止治疗。
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
        public int ApplyDamage(int damage, CombatDamageType damageType, CombatTagDefinition tag, GameObject source)
        {
            DamageContext context = DamageContext.Direct(source, gameObject, damage, damageType, tag);
            return ApplyDamage(DamageCalculator.Calculate(context));
        }

        /// <summary>
        /// 应用伤害管线输出结果。返回实际扣除的生命值。
        /// </summary>
        public int ApplyDamage(DamageResult damageResult)
        {
            if (_isDead || IsTemporaryInvincible)
            {
                return 0;
            }

            int appliedDamage = Mathf.Max(0, damageResult.FinalDamage);
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
                    $"{name} took {actualDamage} {damageResult.DamageType} damage, tag: {GetTagLogText(damageResult.CombatTag)}, hp: {_currentHealth}/{maxHealth}",
                    this);
            }

            CharacterHealthDamageInfo damageInfo = new CharacterHealthDamageInfo(this, actualDamage, damageResult, damageResult.Source);
            onDamaged.Invoke(actualDamage, damageResult.CombatTag);
            Damaged?.Invoke(damageInfo);
            CombatEventsBus.RaiseDamaged(
                damageResult.Source,
                gameObject,
                actualDamage,
                damageResult.DamageType,
                damageResult.CombatTag);
            NotifyHealthChanged(damageResult.Source);

            if (_currentHealth <= 0)
            {
                Die(damageResult.Source);
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
        /// 无视临时免伤和普通伤害结算，立即把当前角色置为死亡。
        /// 用于坠落出界、关卡处决等不应被闪避或格挡拦截的世界规则。
        /// </summary>
        public bool Kill(GameObject source = null)
        {
            if (_isDead)
            {
                return false;
            }

            _currentHealth = 0;
            NotifyHealthChanged(source);
            Die(source);
            return true;
        }

        /// <summary>
        /// 重置生命值和死亡状态。
        /// </summary>
        public void ResetHealth()
        {
            CacheComponents();

            _currentHealth = maxHealth;
            _isDead = false;
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
            IHitInterceptor[] initialInterceptors = GetComponents<IHitInterceptor>();
            for (int i = 0; i < initialInterceptors.Length; i++)
            {
                RegisterHitInterceptor(initialInterceptors[i]);
            }

            _ownedColliderCount = _ownedColliders.Length;
            _componentsCached = true;
        }

        private HitInterception ResolveHitInterception(HitboxHitInfo hitInfo)
        {
            for (int i = 0; i < _hitInterceptors.Count;)
            {
                IHitInterceptor interceptor = _hitInterceptors[i];
                if (!IsHitInterceptorValid(interceptor))
                {
                    _hitInterceptors.RemoveAt(i);
                    continue;
                }

                HitInterception result = interceptor.InterceptHit(hitInfo);
                if (result.Intercepted)
                {
                    return result;
                }

                i++;
            }

            return HitInterception.Continue;
        }

        private static bool IsHitInterceptorValid(IHitInterceptor interceptor)
        {
            if (interceptor == null)
            {
                return false;
            }

            if (interceptor is UnityEngine.Object unityObject && unityObject == null)
            {
                return false;
            }

            if (interceptor is Behaviour behaviour && !behaviour.isActiveAndEnabled)
            {
                return false;
            }

            return true;
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
        public CharacterHealthDamageInfo(
            CharacterHealth health,
            int damage,
            DamageResult damageResult,
            GameObject source)
        {
            Health = health;
            Damage = damage;
            DamageResult = damageResult;
            DamageType = damageResult.DamageType;
            Tag = damageResult.CombatTag;
            Source = source;
        }

        public CharacterHealth Health { get; }

        public int Damage { get; }

        public DamageResult DamageResult { get; }

        public CombatDamageType DamageType { get; }

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
