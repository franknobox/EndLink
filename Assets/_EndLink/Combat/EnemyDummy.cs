using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace EndLink.Combat
{
    /// <summary>
    /// 训练木桩敌人。
    /// 同时实现 IHitReceiver 和 IDamageable：Hitbox 给完整命中信息，木桩内部再转成简单伤害处理。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class EnemyDummy : MonoBehaviour, IHitReceiver, IDamageable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("血量")]
        [Tooltip("木桩最大生命值。用于验证伤害、死亡事件和后续连携条件。")]
        [SerializeField, Min(1)]
        private int maxHealth = 100;

        [Tooltip("启用物体时是否自动把血量恢复到最大值。方便反复进 Play Mode 或临时禁用/启用木桩测试。")]
        [SerializeField]
        private bool resetHealthOnEnable = true;

        [Header("受击反馈")]
        [Tooltip("受击时瞬间切换的颜色。当前使用浅红不透明色，适合胶囊白模阶段。")]
        [SerializeField]
        private Color hitColor = new Color(1f, 0.55f, 0.55f, 1f);

        [Tooltip("受击颜色保持时间。")]
        [SerializeField, Min(0.01f)]
        private float hitFlashDuration = 0.1f;

        [Tooltip("死亡后显示的颜色。用于在白模阶段快速看出木桩已死亡。")]
        [SerializeField]
        private Color deadColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        [Header("调试显示")]
        [Tooltip("命中、扣血和死亡时是否打印 Debug.Log。")]
        [SerializeField]
        private bool logHits = true;

        [Tooltip("是否把当前血量显示到 GameObject 名字上。方便没有 UI 时直接在 Hierarchy 里观察。")]
        [SerializeField]
        private bool showHealthInName = true;

        [Header("事件")]
        [Tooltip("受到有效伤害时触发。参数依次为：实际伤害值、命中标签。")]
        [SerializeField]
        private EnemyDummyDamagedEvent onDamaged = new();

        [Tooltip("生命值首次降到 0 时触发。")]
        [SerializeField]
        private UnityEvent onDead = new();

        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private Color _originalColor;
        private Coroutine _flashCoroutine;
        private string _originalName;
        private int _currentHealth;
        private bool _isDead;
        private bool _componentsCached;

        /// <summary>最大生命值。</summary>
        public int MaxHealth => maxHealth;

        /// <summary>当前生命值。</summary>
        public int CurrentHealth => _currentHealth;

        /// <summary>是否已经死亡。</summary>
        public bool IsDead => _isDead;

        /// <summary>受击事件。</summary>
        public EnemyDummyDamagedEvent OnDamaged => onDamaged;

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

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            hitFlashDuration = Mathf.Max(0.01f, hitFlashDuration);
        }

        /// <summary>
        /// 接收 Hitbox 的完整命中信息，并转发给简单伤害接口。
        /// </summary>
        public void ReceiveHit(HitboxHitInfo hitInfo)
        {
            DamageContext context = DamageContext.FromHit(hitInfo, gameObject);
            ApplyDamage(DamageCalculator.Calculate(context));
        }

        /// <summary>
        /// 接收伤害，扣除血量，触发受击/死亡事件，并执行调试反馈。
        /// </summary>
        public void TakeDamage(int damage, CombatTagDefinition tag)
        {
            ApplyDamage(damage, CombatDamageType.StructuralDamage, tag, null);
        }

        public void TakeDamage(int damage, CombatDamageType damageType, CombatTagDefinition tag)
        {
            ApplyDamage(damage, damageType, tag, null);
        }

        private void ApplyDamage(int damage, CombatDamageType damageType, CombatTagDefinition tag, GameObject source)
        {
            DamageContext context = DamageContext.Direct(source, gameObject, damage, damageType, tag);
            ApplyDamage(DamageCalculator.Calculate(context));
        }

        private void ApplyDamage(DamageResult damageResult)
        {
            if (_isDead)
            {
                return;
            }

            int appliedDamage = Mathf.Max(0, damageResult.FinalDamage);
            _currentHealth = Mathf.Max(0, _currentHealth - appliedDamage);

            if (logHits)
            {
                Debug.Log(
                    $"EnemyDummy took {appliedDamage} {damageResult.DamageType} damage, tag: {GetTagLogText(damageResult.CombatTag)}, hp: {_currentHealth}/{maxHealth}",
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
                return;
            }

            PlayHitFlash();
        }

        /// <summary>
        /// 重置木桩血量和死亡状态。
        /// 主要用于调试、反复测试命中链路或后续训练场按钮调用。
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

            SetBaseColor(_originalColor);
            UpdateDebugDisplay();
        }

        private void CacheComponents()
        {
            if (_componentsCached)
            {
                return;
            }

            _meshRenderer = GetComponent<MeshRenderer>();
            _propertyBlock = new MaterialPropertyBlock();
            _originalColor = GetOriginalBaseColor();
            _originalName = gameObject.name;
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
            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
            }

            _flashCoroutine = StartCoroutine(FlashHitColor());
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

            SetBaseColor(deadColor);

            if (logHits)
            {
                Debug.Log("EnemyDummy died.", this);
            }

            onDead.Invoke();
            CombatEventsBus.RaiseDead(source, gameObject);
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
            Material sharedMaterial = _meshRenderer != null ? _meshRenderer.sharedMaterial : null;

            if (sharedMaterial != null && sharedMaterial.HasProperty(BaseColorId))
            {
                return sharedMaterial.GetColor(BaseColorId);
            }

            return Color.white;
        }

        private void SetBaseColor(Color color)
        {
            if (_meshRenderer == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            _meshRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, color);
            _meshRenderer.SetPropertyBlock(_propertyBlock);
        }

        private static string GetTagLogText(CombatTagDefinition tag)
        {
            return tag != null ? tag.TagId : "None";
        }
    }

    /// <summary>
    /// 木桩受击事件。
    /// 参数依次为：实际伤害值、命中战斗标签。
    /// </summary>
    [System.Serializable]
    public sealed class EnemyDummyDamagedEvent : UnityEvent<int, CombatTagDefinition>
    {
    }
}
