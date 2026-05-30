using EndLink.Party;
using UnityEngine;

namespace EndLink.UI
{
    /// <summary>
    /// 战斗 HUD 总控制器。
    /// 第一版只负责绑定小队引用、控制 HUD 显隐，并驱动小队战斗动作 UI 刷新。
    /// 后续血条、目标信息、连携提示等 HUD 模块都可以从这里接入。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HUDCombatController : MonoBehaviour
    {
        [Header("组件引用")]
        [Tooltip("小队管理器。为空时会在场景中自动查找。")]
        [SerializeField]
        private PartyManager partyManager;

        [Tooltip("小队战斗动作 UI 管理器。通常挂在本 HUD 的子物体上。")]
        [SerializeField]
        private UIPartyCombatAction partyCombatAction;

        [Tooltip("HUD 根 CanvasGroup。配置后可以统一控制显示、交互和射线。")]
        [SerializeField]
        private CanvasGroup hudCanvasGroup;

        [Header("显示")]
        [Tooltip("启用时是否显示 HUD。")]
        [SerializeField]
        private bool visibleOnStart = true;

        [Tooltip("是否在 LateUpdate 自动刷新 HUD 模块。")]
        [SerializeField]
        private bool autoRefresh = true;

        /// <summary>当前绑定的小队管理器。</summary>
        public PartyManager PartyManager => partyManager;

        /// <summary>小队战斗动作 UI 管理器。</summary>
        public UIPartyCombatAction PartyCombatAction => partyCombatAction;

        private void Awake()
        {
            CacheReferences();
            BindModules();
        }

        private void Start()
        {
            SetVisible(visibleOnStart);
            RefreshNow();
        }

        private void Reset()
        {
            CacheReferences();
        }

        private void LateUpdate()
        {
            if (autoRefresh)
            {
                RefreshNow();
            }
        }

        /// <summary>
        /// 绑定小队管理器，并同步给 HUD 下属模块。
        /// </summary>
        public void BindPartyManager(PartyManager manager)
        {
            partyManager = manager;
            BindModules();
            RefreshNow();
        }

        /// <summary>
        /// 立即刷新 HUD 下属模块。
        /// </summary>
        public void RefreshNow()
        {
            if (partyCombatAction != null)
            {
                partyCombatAction.RefreshNow();
            }
        }

        /// <summary>
        /// 设置 HUD 是否显示。隐藏时同时关闭交互和射线，避免挡住调试操作。
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (hudCanvasGroup == null)
            {
                gameObject.SetActive(visible);
                return;
            }

            hudCanvasGroup.alpha = visible ? 1f : 0f;
            hudCanvasGroup.interactable = visible;
            hudCanvasGroup.blocksRaycasts = visible;
        }

        private void CacheReferences()
        {
            if (partyManager == null)
            {
                partyManager = FindFirstObjectByType<PartyManager>();
            }

            if (partyCombatAction == null)
            {
                partyCombatAction = GetComponentInChildren<UIPartyCombatAction>(true);
            }

            if (hudCanvasGroup == null)
            {
                hudCanvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private void BindModules()
        {
            if (partyCombatAction != null)
            {
                partyCombatAction.BindPartyManager(partyManager);
            }
        }
    }
}
