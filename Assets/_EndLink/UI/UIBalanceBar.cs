using EndLink.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EndLink.UI
{
    /// <summary>
    /// 通用平衡条 UI。
    /// 只依赖 IBalanceSource，不关心平衡值属于主角、敌人或 Boss，也不修改任何战斗状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIBalanceBar : MonoBehaviour
    {
        private const float FullThreshold = 0.999f;

        [Header("平衡来源")]
        [Tooltip("要显示的平衡值组件。该组件必须实现 IBalanceSource；当前 EnemyBalance 已支持，后续主角平衡组件也可直接接入。")]
        [SerializeField]
        private MonoBehaviour balanceSource;

        [Tooltip("没有手动绑定来源时，是否从当前物体及父物体中查找实现 IBalanceSource 的组件。")]
        [SerializeField]
        private bool autoFindInParent = true;

        [Header("显示组件")]
        [Tooltip("平衡条填充 Image。推荐 Image Type 使用 Filled。")]
        [SerializeField]
        private Image fillImage;

        [Tooltip("可选的平衡值文本。为空时只显示进度条。")]
        [SerializeField]
        private TextMeshProUGUI valueText;

        [Tooltip("可选根 CanvasGroup。配置后可统一控制整条 UI 的显隐。")]
        [SerializeField]
        private CanvasGroup canvasGroup;

        [Header("显示规则")]
        [Tooltip("平衡值为满值时是否隐藏。普通敌人头顶平衡条可以开启，主角和 Boss HUD 通常关闭。")]
        [SerializeField]
        private bool hideWhenFull;

        [Tooltip("没有绑定有效平衡来源时是否隐藏。")]
        [SerializeField]
        private bool hideWhenNoSource = true;

        [Tooltip("是否显示当前平衡值文本。")]
        [SerializeField]
        private bool showValueText;

        [Tooltip("文本是否同时显示最大平衡值。开启时显示 60/100，关闭时只显示 60。")]
        [SerializeField]
        private bool showMaxValueInText = true;

        [Header("刷新")]
        [Tooltip("是否在 Update 中兜底刷新。正常情况下平衡值通过事件刷新，应保持关闭。")]
        [SerializeField]
        private bool autoRefresh;

        /// <summary>Inspector 中配置的平衡来源组件。</summary>
        public MonoBehaviour BalanceSourceComponent => balanceSource;

        /// <summary>当前解析到的统一平衡来源。</summary>
        public IBalanceSource BalanceSource => ResolveBalanceSource();

        /// <summary>当前是否绑定了有效平衡来源。</summary>
        public bool HasBalanceSource => ResolveBalanceSource() != null;

        /// <summary>当前显示的 0 到 1 平衡比例。</summary>
        public float NormalizedValue { get; private set; }

        private IBalanceSource _subscribedSource;
        private bool _started;

        private void Awake()
        {
            CacheVisualReferences();
            TryAutoFindBalanceSource();
        }

        private void OnEnable()
        {
            Subscribe();
            if (_started)
            {
                RefreshNow();
            }
        }

        private void Start()
        {
            _started = true;
            TryAutoFindBalanceSource();
            Subscribe();
            RefreshNow();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Reset()
        {
            CacheVisualReferences();
            TryAutoFindBalanceSource();
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

        /// <summary>
        /// 绑定新的平衡来源。
        /// 返回 false 表示传入组件没有实现 IBalanceSource，当前绑定不会改变。
        /// </summary>
        public bool BindBalanceSource(MonoBehaviour nextSource)
        {
            if (nextSource != null && !(nextSource is IBalanceSource))
            {
                return false;
            }

            if (balanceSource == nextSource)
            {
                Subscribe();
                RefreshNow();
                return true;
            }

            Unsubscribe();
            balanceSource = nextSource;
            Subscribe();
            RefreshNow();
            return true;
        }

        /// <summary>清空当前平衡来源并刷新显隐。</summary>
        public void ClearBalanceSource()
        {
            Unsubscribe();
            balanceSource = null;
            autoFindInParent = false;
            RefreshNow();
        }

        /// <summary>
        /// 配置为只有填充图的轻量进度条。
        /// 适合 HUD 生成器或运行时创建基础平衡条时调用。
        /// </summary>
        public void ConfigureSimpleBar(Image nextFillImage, CanvasGroup nextCanvasGroup = null)
        {
            fillImage = nextFillImage;
            canvasGroup = nextCanvasGroup;
            valueText = null;
            showValueText = false;
            showMaxValueInText = false;
            hideWhenFull = false;
            hideWhenNoSource = true;
            autoFindInParent = false;
            autoRefresh = false;

            if (fillImage == null)
            {
                return;
            }

            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.raycastTarget = false;
        }

        /// <summary>立即刷新填充、文本和显隐状态。</summary>
        public void RefreshNow()
        {
            CacheVisualReferences();
            TryAutoFindBalanceSource();
            Subscribe();

            IBalanceSource source = ResolveBalanceSource();
            if (source == null)
            {
                NormalizedValue = 0f;
                ApplyFill(0f);
                ApplyText(0f, 1f, false);
                ApplyVisibility(!hideWhenNoSource);
                return;
            }

            float maxBalance = Mathf.Max(0.001f, source.MaxBalance);
            float currentBalance = Mathf.Clamp(source.CurrentBalance, 0f, maxBalance);
            NormalizedValue = Mathf.Clamp01(source.NormalizedBalance);

            ApplyFill(NormalizedValue);
            ApplyText(currentBalance, maxBalance, true);
            ApplyVisibility(!hideWhenFull || NormalizedValue < FullThreshold);
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

        private void TryAutoFindBalanceSource()
        {
            if (ResolveBalanceSource() != null || !autoFindInParent)
            {
                return;
            }

            MonoBehaviour[] parentComponents = GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < parentComponents.Length; i++)
            {
                MonoBehaviour component = parentComponents[i];
                if (component is IBalanceSource)
                {
                    balanceSource = component;
                    return;
                }
            }
        }

        private IBalanceSource ResolveBalanceSource()
        {
            return balanceSource != null ? balanceSource as IBalanceSource : null;
        }

        private void Subscribe()
        {
            IBalanceSource source = ResolveBalanceSource();
            if (ReferenceEquals(source, _subscribedSource))
            {
                return;
            }

            Unsubscribe();
            if (source == null)
            {
                return;
            }

            source.BalanceChanged += HandleBalanceChanged;
            _subscribedSource = source;
        }

        private void Unsubscribe()
        {
            if (_subscribedSource == null)
            {
                return;
            }

            _subscribedSource.BalanceChanged -= HandleBalanceChanged;
            _subscribedSource = null;
        }

        private void HandleBalanceChanged(float currentBalance, float maxBalance)
        {
            RefreshNow();
        }

        private void ApplyFill(float normalizedValue)
        {
            if (fillImage != null)
            {
                fillImage.fillAmount = normalizedValue;
            }
        }

        private void ApplyText(float currentBalance, float maxBalance, bool hasSource)
        {
            if (valueText == null)
            {
                return;
            }

            if (!showValueText || !hasSource)
            {
                valueText.text = string.Empty;
                return;
            }

            valueText.text = showMaxValueInText
                ? $"{Mathf.CeilToInt(currentBalance)}/{Mathf.CeilToInt(maxBalance)}"
                : Mathf.CeilToInt(currentBalance).ToString();
        }

        private void ApplyVisibility(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
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
