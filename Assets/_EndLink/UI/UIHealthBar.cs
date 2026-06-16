using EndLink.Combat;
using EndLink.Enemies;
using EndLink.Party;
using TMPro;
using EndLink.Ally;
using UnityEngine;
using UnityEngine.UI;

namespace EndLink.UI
{
    /// <summary>
    /// 通用生命条 UI。
    /// 支持通用 CharacterHealth 和正式敌人 EnemyHealth。
    /// 可用于 HUD 状态栏，也可用于敌人头顶血条。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIHealthBar : MonoBehaviour
    {
        [Header("生命来源")]
        [Tooltip("要显示的通用生命组件。玩家和队友通常使用 CharacterHealth。为空时可从父物体或场景中自动查找。")]
        [SerializeField]
        private CharacterHealth health;

        [Tooltip("要显示的正式敌人生命组件。敌人头顶血条通常使用 EnemyHealth。CharacterHealth 和 EnemyHealth 同时存在时，优先使用 CharacterHealth。")]
        [SerializeField]
        private EnemyHealth enemyHealth;

        [Tooltip("当生命来源为空时，是否从父物体向上查找 CharacterHealth 或 EnemyHealth。适合把血条挂在角色子物体上。")]
        [SerializeField]
        private bool autoFindInParent = true;

        [Tooltip("当生命来源为空且父物体没有生命组件时，是否在场景中查找第一个 CharacterHealth 或 EnemyHealth。HUD 调试时可用。")]
        [SerializeField]
        private bool autoFindInScene;

        [Header("显示组件")]
        [Tooltip("血条填充 Image。推荐 Image Type 使用 Filled。")]
        [SerializeField]
        private Image fillImage;

        [Tooltip("可选血量文本。为空时只显示进度条。")]
        [SerializeField]
        private TextMeshProUGUI valueText;

        [Tooltip("可选根 CanvasGroup。配置后可以用透明度隐藏，而不是直接 SetActive。")]
        [SerializeField]
        private CanvasGroup canvasGroup;

        [Header("显示规则")]
        [Tooltip("是否在血量满时隐藏。敌人头顶血条常用；主角和队友状态栏通常关闭。")]
        [SerializeField]
        private bool hideWhenFull;

        [Tooltip("死亡时是否隐藏血条。")]
        [SerializeField]
        private bool hideWhenDead = true;

        [Tooltip("没有绑定生命来源时是否隐藏。")]
        [SerializeField]
        private bool hideWhenNoHealth = true;

        [Tooltip("是否显示血量文本。")]
        [SerializeField]
        private bool showValueText = true;

        [Tooltip("文本是否显示最大生命值。开启时显示 80/100，关闭时只显示 80。")]
        [SerializeField]
        private bool showMaxHealthInText = true;

        [Header("刷新")]
        [Tooltip("是否在 Update 中自动兜底刷新。血量变化优先通过事件刷新；只有担心外部重置血量但未发事件时才需要开启。")]
        [SerializeField]
        private bool autoRefresh;

        /// <summary>当前绑定的生命组件。</summary>
        public CharacterHealth Health => health;

        /// <summary>当前绑定的正式敌人生命组件。</summary>
        public EnemyHealth EnemyHealth => enemyHealth;

        /// <summary>当前是否绑定了任何生命来源。</summary>
        public bool HasHealthSource => health != null || enemyHealth != null;

        /// <summary>当前血量比例。</summary>
        public float NormalizedValue { get; private set; }

        private CharacterHealth _subscribedHealth;
        private EnemyHealth _subscribedEnemyHealth;
        private bool _started;

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            Subscribe();
            if (_started)
            {
                RefreshNow();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Reset()
        {
            CacheReferences();
        }

        private void OnValidate()
        {
            CacheVisualReferences();
        }

        private void Update()
        {
            if (autoRefresh)
            {
                RefreshNow();
            }
        }

        private void Start()
        {
            _started = true;
            RefreshNow();
        }

        /// <summary>
        /// 绑定新的生命来源。
        /// 外部 HUD、目标锁定或头顶血条生成器可以通过它动态切换显示对象。
        /// </summary>
        public void BindHealth(CharacterHealth nextHealth)
        {
            if (health == nextHealth && enemyHealth == null)
            {
                RefreshNow();
                return;
            }

            Unsubscribe();
            health = nextHealth;
            if (nextHealth != null)
            {
                enemyHealth = null;
            }

            Subscribe();
            RefreshNow();
        }

        /// <summary>
        /// 绑定正式敌人生命来源。
        /// 敌人头顶血条可以通过它直接显示 EnemyHealth。
        /// </summary>
        public void BindEnemyHealth(EnemyHealth nextEnemyHealth)
        {
            if (enemyHealth == nextEnemyHealth && health == null)
            {
                RefreshNow();
                return;
            }

            Unsubscribe();
            enemyHealth = nextEnemyHealth;
            if (nextEnemyHealth != null)
            {
                health = null;
            }

            Subscribe();
            RefreshNow();
        }

        /// <summary>
        /// 清空生命来源并刷新显示。
        /// </summary>
        public void ClearHealth()
        {
            Unsubscribe();
            health = null;
            enemyHealth = null;
            RefreshNow();
        }

        /// <summary>
        /// 把当前组件配置为纯进度条模式。
        /// 适用于主角/队友 HUD 这种只有填充条、不显示数字的轻量血条。
        /// </summary>
        public void ConfigureSimpleBar(Image nextFillImage, CanvasGroup nextCanvasGroup = null)
        {
            fillImage = nextFillImage;
            canvasGroup = nextCanvasGroup;
            valueText = null;
            showValueText = false;
            showMaxHealthInText = false;
            hideWhenFull = false;
            hideWhenDead = false;
            hideWhenNoHealth = true;
            autoFindInParent = false;
            autoFindInScene = false;
            autoRefresh = false;

            if (fillImage != null)
            {
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = 0;
                fillImage.raycastTarget = false;
            }
        }

        /// <summary>
        /// 立即刷新血条填充、文本和显隐。
        /// </summary>
        public void RefreshNow()
        {
            CacheVisualReferences();

            if (!HasHealthSource)
            {
                TryAutoFindHealth();
            }

            int currentHealth = ResolveCurrentHealth();
            int maxHealth = Mathf.Max(1, ResolveMaxHealth());
            bool isDead = ResolveIsDead();

            NormalizedValue = HasHealthSource ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
            ApplyFill(NormalizedValue);
            ApplyText(currentHealth, maxHealth);
            ApplyVisibility(ResolveShouldShow(isDead));
        }

        private void CacheReferences()
        {
            CacheVisualReferences();

            if (!HasHealthSource)
            {
                TryAutoFindHealth();
            }
        }

        private void CacheVisualReferences()
        {
            if (fillImage == null)
            {
                fillImage = GetComponentInChildren<Image>(true);
            }

            if (valueText == null)
            {
                valueText = GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private void TryAutoFindHealth()
        {
            CharacterHealth foundHealth = null;
            EnemyHealth foundEnemyHealth = null;

            if (autoFindInParent)
            {
                foundHealth = GetComponentInParent<CharacterHealth>();
                if (foundHealth == null)
                {
                    foundEnemyHealth = GetComponentInParent<EnemyHealth>();
                }
            }

            if (foundHealth == null && autoFindInScene)
            {
                foundHealth = FindFirstObjectByType<CharacterHealth>();
                if (foundHealth == null && foundEnemyHealth == null)
                {
                    foundEnemyHealth = FindFirstObjectByType<EnemyHealth>();
                }
            }

            if (foundHealth != null)
            {
                BindHealth(foundHealth);
                return;
            }

            if (foundEnemyHealth != null)
            {
                BindEnemyHealth(foundEnemyHealth);
                return;
            }

            TryBindPartyHealth();
        }

        private bool TryBindPartyHealth()
        {
            PartyManager partyManager = FindFirstObjectByType<PartyManager>();
            if (partyManager == null)
            {
                return false;
            }

            string objectName = gameObject.name;
            if (string.Equals(objectName, "Health_Main", System.StringComparison.Ordinal))
            {
                if (partyManager.TryGetMainCharacterHealth(out CharacterHealth mainHealth) && mainHealth != null)
                {
                    BindHealth(mainHealth);
                    return true;
                }

                return false;
            }

            if (string.Equals(objectName, "Health_AllyA", System.StringComparison.Ordinal))
            {
                return TryBindAllyHealth(partyManager.AllySlotA);
            }

            if (string.Equals(objectName, "Health_AllyB", System.StringComparison.Ordinal))
            {
                return TryBindAllyHealth(partyManager.AllySlotB);
            }

            return false;
        }

        private bool TryBindAllyHealth(PartyFormationSlot slot)
        {
            if (slot == null
                || !slot.TryGetAllyHealth(out AllyHealth allyHealth)
                || allyHealth == null
                || allyHealth.Health == null)
            {
                return false;
            }

            BindHealth(allyHealth.Health);
            return true;
        }

        private void Subscribe()
        {
            if (health != null && _subscribedHealth != health)
            {
                if (_subscribedHealth != null)
                {
                    _subscribedHealth.HealthChanged -= HandleHealthChanged;
                    _subscribedHealth.Died -= HandleDied;
                }

                health.HealthChanged += HandleHealthChanged;
                health.Died += HandleDied;
                _subscribedHealth = health;
            }

            if (enemyHealth != null && _subscribedEnemyHealth != enemyHealth)
            {
                if (_subscribedEnemyHealth != null)
                {
                    _subscribedEnemyHealth.OnDamaged.RemoveListener(HandleEnemyDamaged);
                    _subscribedEnemyHealth.OnDead.RemoveListener(HandleEnemyDead);
                }

                enemyHealth.OnDamaged.AddListener(HandleEnemyDamaged);
                enemyHealth.OnDead.AddListener(HandleEnemyDead);
                _subscribedEnemyHealth = enemyHealth;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribedHealth != null)
            {
                _subscribedHealth.HealthChanged -= HandleHealthChanged;
                _subscribedHealth.Died -= HandleDied;
                _subscribedHealth = null;
            }

            if (_subscribedEnemyHealth != null)
            {
                _subscribedEnemyHealth.OnDamaged.RemoveListener(HandleEnemyDamaged);
                _subscribedEnemyHealth.OnDead.RemoveListener(HandleEnemyDead);
                _subscribedEnemyHealth = null;
            }
        }

        private void HandleHealthChanged(CharacterHealthChangeInfo changeInfo)
        {
            RefreshNow();
        }

        private void HandleDied(CharacterHealthDeathInfo deathInfo)
        {
            RefreshNow();
        }

        private void HandleEnemyDamaged(int damageAmount, CombatTagDefinition tag)
        {
            RefreshNow();
        }

        private void HandleEnemyDead()
        {
            RefreshNow();
        }

        private int ResolveCurrentHealth()
        {
            if (health != null)
            {
                return health.CurrentHealth;
            }

            return enemyHealth != null ? enemyHealth.CurrentHealth : 0;
        }

        private int ResolveMaxHealth()
        {
            if (health != null)
            {
                return health.MaxHealth;
            }

            return enemyHealth != null ? enemyHealth.MaxHealth : 1;
        }

        private bool ResolveIsDead()
        {
            if (health != null)
            {
                return health.IsDead;
            }

            return enemyHealth != null && enemyHealth.IsDead;
        }

        private void ApplyFill(float normalizedValue)
        {
            if (fillImage == null)
            {
                return;
            }

            fillImage.fillAmount = normalizedValue;
        }

        private void ApplyText(int currentHealth, int maxHealth)
        {
            if (valueText == null)
            {
                return;
            }

            if (!showValueText || !HasHealthSource)
            {
                valueText.text = string.Empty;
                return;
            }

            valueText.text = showMaxHealthInText
                ? $"{currentHealth}/{maxHealth}"
                : currentHealth.ToString();
        }

        private bool ResolveShouldShow(bool isDead)
        {
            if (!HasHealthSource)
            {
                return !hideWhenNoHealth;
            }

            if (hideWhenDead && isDead)
            {
                return false;
            }

            if (hideWhenFull && ResolveCurrentHealth() >= ResolveMaxHealth())
            {
                return false;
            }

            return true;
        }

        private void ApplyVisibility(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
                return;
            }

            if (fillImage != null)
            {
                fillImage.enabled = visible;
            }

            if (valueText != null)
            {
                valueText.enabled = visible;
            }
        }
    }
}
