using EndLink.Combat;
using TMPro;
using UnityEngine;

namespace EndLink.UI
{
    /// <summary>
    /// 主角武器形态的轻量 HUD 显示。
    /// 只监听 PlayerWeaponController 的形态变化并显示 A、B、C，不读取输入，也不负责切换形态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIWeaponForm : MonoBehaviour
    {
        [Tooltip("主角根物体上的武器形态控制器。为空时会在场景中查找一次。")]
        [SerializeField]
        private PlayerWeaponController weaponController;

        [Tooltip("显示当前 A、B、C 形态的文本组件。")]
        [SerializeField]
        private TextMeshProUGUI formLabel;

        private PlayerWeaponController _subscribedController;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshNow();
        }

        private void Start()
        {
            // 兼容 HUD 比玩家更早启用的场景，在首帧后补一次运行时接线。
            ResolveReferences();
            RefreshNow();
        }

        private void OnDisable()
        {
            Subscribe(null);
        }

        /// <summary>运行时显式绑定主角武器形态控制器。</summary>
        public void Bind(PlayerWeaponController controller)
        {
            weaponController = controller;
            Subscribe(controller);
            RefreshNow();
        }

        /// <summary>立即刷新当前形态字母。</summary>
        public void RefreshNow()
        {
            if (formLabel == null)
            {
                return;
            }

            formLabel.text = weaponController != null
                ? weaponController.CurrentForm.ToString()
                : PlayerWeaponForm.A.ToString();
        }

        private void ResolveReferences()
        {
            formLabel ??= GetComponentInChildren<TextMeshProUGUI>(true);
            weaponController ??= FindFirstObjectByType<PlayerWeaponController>(FindObjectsInactive.Include);
            Subscribe(weaponController);
        }

        private void Subscribe(PlayerWeaponController controller)
        {
            if (_subscribedController == controller)
            {
                return;
            }

            if (_subscribedController != null)
            {
                _subscribedController.FormChanged -= OnFormChanged;
            }

            _subscribedController = controller;
            if (_subscribedController != null)
            {
                _subscribedController.FormChanged += OnFormChanged;
            }
        }

        private void OnFormChanged(PlayerWeaponForm form)
        {
            if (formLabel != null)
            {
                formLabel.text = form.ToString();
            }
        }
    }
}
