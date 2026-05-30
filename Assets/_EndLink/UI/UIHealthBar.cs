using EndLink.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EndLink.UI
{
    /// <summary>
    /// 通用生命条 UI。
    /// 只依赖 CharacterHealth，不关心目标是玩家、队友还是敌人。
    /// 可用于 HUD 状态栏，也可用于后续敌人头顶血条。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIHealthBar : MonoBehaviour
    {
        [Header("生命来源")]
        [Tooltip("要显示的通用生命组件。为空时可从父物体或场景中自动查找。")]
        [SerializeField]
        private CharacterHealth health;

        [Tooltip("当 Health 为空时，是否从父物体向上查找 CharacterHealth。适合把血条挂在角色子物体上。")]
        [SerializeField]
        private bool autoFindInParent = true;

        [Tooltip("当 Health 为空且父物体没有生命组件时，是否在场景中查找第一个 CharacterHealth。HUD 调试时可用。")]
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
        [Tooltip("是否在 Update 中自动刷新。常规 HUD 建议开启；如果完全依赖事件刷新也可以关闭。")]
        [SerializeField]
        private bool autoRefresh = true;

        /// <summary>当前绑定的生命组件。</summary>
        public CharacterHealth Health => health;

        /// <summary>当前血量比例。</summary>
        public float NormalizedValue { get; private set; }

        private void Awake()
        {
            CacheReferences();
            BindHealth(health);
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshNow();
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
            RefreshNow();
        }

        private void Update()
        {
            if (autoRefresh)
            {
                RefreshNow();
            }
        }

        /// <summary>
        /// 绑定新的生命来源。
        /// 外部 HUD、目标锁定或头顶血条生成器可以通过它动态切换显示对象。
        /// </summary>
        public void BindHealth(CharacterHealth nextHealth)
        {
            if (health == nextHealth)
            {
                RefreshNow();
                return;
            }

            Unsubscribe();
            health = nextHealth;
            Subscribe();
            RefreshNow();
        }

        /// <summary>
        /// 清空生命来源并刷新显示。
        /// </summary>
        public void ClearHealth()
        {
            BindHealth(null);
        }

        /// <summary>
        /// 立即刷新血条填充、文本和显隐。
        /// </summary>
        public void RefreshNow()
        {
            CacheVisualReferences();

            if (health == null)
            {
                TryAutoFindHealth();
            }

            int currentHealth = health != null ? health.CurrentHealth : 0;
            int maxHealth = health != null ? Mathf.Max(1, health.MaxHealth) : 1;
            bool isDead = health != null && health.IsDead;

            NormalizedValue = health != null ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
            ApplyFill(NormalizedValue);
            ApplyText(currentHealth, maxHealth);
            ApplyVisibility(ResolveShouldShow(isDead));
        }

        private void CacheReferences()
        {
            CacheVisualReferences();

            if (health == null)
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

            if (autoFindInParent)
            {
                foundHealth = GetComponentInParent<CharacterHealth>();
            }

            if (foundHealth == null && autoFindInScene)
            {
                foundHealth = FindFirstObjectByType<CharacterHealth>();
            }

            if (foundHealth != null)
            {
                BindHealth(foundHealth);
            }
        }

        private void Subscribe()
        {
            if (health == null)
            {
                return;
            }

            health.HealthChanged += HandleHealthChanged;
            health.Died += HandleDied;
        }

        private void Unsubscribe()
        {
            if (health == null)
            {
                return;
            }

            health.HealthChanged -= HandleHealthChanged;
            health.Died -= HandleDied;
        }

        private void HandleHealthChanged(CharacterHealthChangeInfo changeInfo)
        {
            RefreshNow();
        }

        private void HandleDied(CharacterHealthDeathInfo deathInfo)
        {
            RefreshNow();
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

            if (!showValueText || health == null)
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
            if (health == null)
            {
                return !hideWhenNoHealth;
            }

            if (hideWhenDead && isDead)
            {
                return false;
            }

            if (hideWhenFull && health.CurrentHealth >= health.MaxHealth)
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
