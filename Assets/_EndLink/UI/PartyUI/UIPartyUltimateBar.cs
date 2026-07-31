using EndLink.Party;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EndLink.UI
{
    /// <summary>
    /// 终链奥义条 UI。
    /// 负责显示全队协同率、奥义键位和就绪颜色，不执行终链奥义。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIPartyUltimateBar : MonoBehaviour
    {
        [Header("小队绑定")]
        [Tooltip("小队管理器。为空时会在场景中自动查找。")]
        [SerializeField]
        private PartyManager partyManager;

        [Tooltip("终链奥义上下文。为空时会从 PartyManager 所在物体上获取。")]
        [SerializeField]
        private PartyUltimateContext ultimateContext;

        [Header("显示组件")]
        [Tooltip("奥义条填充 Image。推荐 Image Type 使用 Filled，Fill Method 使用 Horizontal。")]
        [SerializeField]
        private Image fillImage;

        [Tooltip("奥义键位文本。默认读取 PartyCombatRouter 中的 Ultimate 键位。")]
        [SerializeField]
        private TextMeshProUGUI keyLabelText;

        [Tooltip("协同率文本。为空时不显示数值。")]
        [SerializeField]
        private TextMeshProUGUI valueText;

        [Header("颜色")]
        [Tooltip("终链奥义未满时的填充颜色。")]
        [SerializeField]
        private Color chargingColor = new(0.7f, 0.72f, 0.76f, 0.95f);

        [Tooltip("终链奥义可释放时的填充颜色。")]
        [SerializeField]
        private Color readyColor = new(1f, 0.82f, 0.28f, 1f);

        [Header("刷新")]
        [Tooltip("是否每帧刷新协同率显示。协同率目前是事件变化，但逐帧兜底可以避免手动调试时 UI 不刷新。")]
        [SerializeField]
        private bool autoRefresh = true;

        /// <summary>当前显示的协同率归一化进度。</summary>
        public float SynergyNormalized { get; private set; }

        private PartyUltimateContext _subscribedUltimateContext;
        private PartyCombatRouter _subscribedRouter;

        private void Awake()
        {
            CacheReferences();
            RefreshNow();
        }

        private void OnEnable()
        {
            CacheReferences();
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
                RefreshProgress();
            }
        }

        /// <summary>
        /// 绑定小队管理器，并刷新奥义条显示。
        /// </summary>
        public void BindPartyManager(PartyManager manager)
        {
            Unsubscribe();
            partyManager = manager;
            ultimateContext = null;
            CacheReferences();
            Subscribe();
            RefreshNow();
        }

        /// <summary>
        /// 立即刷新奥义条进度、颜色、键位和文本。
        /// </summary>
        public void RefreshNow()
        {
            CacheVisualReferences();
            RefreshProgress();
            RefreshKeyLabel();
        }

        /// <summary>
        /// 只刷新协同率进度和就绪颜色。
        /// </summary>
        public void RefreshProgress()
        {
            CacheReferences();

            SynergyNormalized = ultimateContext != null ? ultimateContext.SynergyNormalized : 0f;

            if (fillImage != null)
            {
                fillImage.fillAmount = SynergyNormalized;
                fillImage.color = ultimateContext != null && ultimateContext.CanUseUltimate ? readyColor : chargingColor;
            }

            if (valueText != null)
            {
                valueText.text = ultimateContext != null
                    ? $"{Mathf.RoundToInt(SynergyNormalized * 100f)}%"
                    : "0%";
            }
        }

        /// <summary>
        /// 只刷新奥义键位文本。
        /// </summary>
        public void RefreshKeyLabel()
        {
            if (keyLabelText == null)
            {
                return;
            }

            PartyCombatRouter router = partyManager != null ? partyManager.CombatRouter : null;
            keyLabelText.text = router != null
                ? router.GetKeyLabelForCommand(PartyCombatCommandType.Ultimate, PartyCombatActorSlot.Party)
                : "V";
        }

        private void CacheReferences()
        {
            if (partyManager == null)
            {
                partyManager = FindFirstObjectByType<PartyManager>();
            }

            if (ultimateContext == null && partyManager != null)
            {
                ultimateContext = partyManager.GetComponent<PartyUltimateContext>();
            }

            CacheVisualReferences();
        }

        private void CacheVisualReferences()
        {
            if (fillImage == null)
            {
                fillImage = GetComponentInChildren<Image>(true);
            }

            if (valueText == null)
            {
                TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i] != keyLabelText)
                    {
                        valueText = texts[i];
                        break;
                    }
                }
            }
        }

        private void Subscribe()
        {
            if (ultimateContext != null && _subscribedUltimateContext != ultimateContext)
            {
                UnsubscribeUltimateContext();
                _subscribedUltimateContext = ultimateContext;
                _subscribedUltimateContext.SynergyChanged += HandleSynergyChanged;
                _subscribedUltimateContext.UltimateReady += HandleUltimateStateChanged;
                _subscribedUltimateContext.UltimateConsumed += HandleUltimateStateChanged;
            }

            PartyCombatRouter router = partyManager != null ? partyManager.CombatRouter : null;
            if (router != null && _subscribedRouter != router)
            {
                UnsubscribeRouter();
                _subscribedRouter = router;
                _subscribedRouter.KeyBindingsChanged += HandleKeyBindingsChanged;
            }
        }

        private void Unsubscribe()
        {
            UnsubscribeUltimateContext();
            UnsubscribeRouter();
        }

        private void UnsubscribeUltimateContext()
        {
            if (_subscribedUltimateContext == null)
            {
                return;
            }

            _subscribedUltimateContext.SynergyChanged -= HandleSynergyChanged;
            _subscribedUltimateContext.UltimateReady -= HandleUltimateStateChanged;
            _subscribedUltimateContext.UltimateConsumed -= HandleUltimateStateChanged;
            _subscribedUltimateContext = null;
        }

        private void UnsubscribeRouter()
        {
            if (_subscribedRouter == null)
            {
                return;
            }

            _subscribedRouter.KeyBindingsChanged -= HandleKeyBindingsChanged;
            _subscribedRouter = null;
        }

        private void HandleSynergyChanged(float current, float max)
        {
            RefreshProgress();
        }

        private void HandleUltimateStateChanged()
        {
            RefreshProgress();
        }

        private void HandleKeyBindingsChanged()
        {
            RefreshKeyLabel();
        }
    }
}
