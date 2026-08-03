using EndLink.Combat;
using UnityEngine;

namespace EndLink.UI
{
    /// <summary>
    /// 射击瞄准十字准星的轻量显示桥接。
    /// 只监听 PlayerAimController 的状态并控制 CanvasGroup，不读取输入或计算瞄准点。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UIAimReticle : MonoBehaviour
    {
        [Tooltip("主角射击瞄准控制器。为空时会在场景中查找一次。")]
        [SerializeField]
        private PlayerAimController aimController;

        [Tooltip("控制准星整体显隐的 CanvasGroup。为空时读取当前物体。")]
        [SerializeField]
        private CanvasGroup canvasGroup;

        private PlayerAimController _subscribedController;

        private void Awake()
        {
            canvasGroup ??= GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            ResolveAimController();
            RefreshNow();
        }

        private void Start()
        {
            // 兼容 HUD 比玩家稍早启用的场景，再补一次运行时接线。
            ResolveAimController();
            RefreshNow();
        }

        private void OnDisable()
        {
            Subscribe(null);
        }

        /// <summary>运行时显式绑定主角瞄准控制器。</summary>
        public void Bind(PlayerAimController controller)
        {
            aimController = controller;
            Subscribe(controller);
            RefreshNow();
        }

        /// <summary>立即同步当前准星显隐。</summary>
        public void RefreshNow()
        {
            ApplyVisible(aimController != null && aimController.IsAiming);
        }

        private void ResolveAimController()
        {
            aimController ??= FindFirstObjectByType<PlayerAimController>(FindObjectsInactive.Include);
            Subscribe(aimController);
        }

        private void Subscribe(PlayerAimController controller)
        {
            if (_subscribedController == controller)
            {
                return;
            }

            if (_subscribedController != null)
            {
                _subscribedController.AimStateChanged -= OnAimStateChanged;
            }

            _subscribedController = controller;
            if (_subscribedController != null)
            {
                _subscribedController.AimStateChanged += OnAimStateChanged;
            }
        }

        private void OnAimStateChanged(bool isAiming)
        {
            ApplyVisible(isAiming);
        }

        private void ApplyVisible(bool visible)
        {
            canvasGroup ??= GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
