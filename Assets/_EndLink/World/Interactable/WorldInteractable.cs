using UnityEngine;
using UnityEngine.Events;

namespace EndLink.World
{
    /// <summary>
    /// 世界可交互对象基类。
    /// 本身可以直接挂在灰盒机关上，通过 UnityEvent 验证交互；
    /// 具体门、电梯、开关也可以继承它并覆盖 OnInteract。
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldInteractable : MonoBehaviour, IWorldInteractable
    {
        [Header("交互配置")]
        [Tooltip("是否允许当前对象被交互。关闭后交互扫描仍可找到组件，但不会作为有效目标。")]
        [SerializeField]
        private bool interactionEnabled = true;

        [Tooltip("交互提示文本，后续可由 HUD 或世界提示组件读取。")]
        [SerializeField]
        private string interactionPrompt = "交互";

        [Tooltip("可选交互点。为空时使用本物体 Transform 位置。")]
        [SerializeField]
        private Transform interactionPoint;

        [Header("事件")]
        [Tooltip("交互成功后触发，参数为发起交互的 GameObject。")]
        [SerializeField]
        private UnityEvent<GameObject> onInteracted = new();

        /// <inheritdoc />
        public Transform InteractionTransform => interactionPoint != null ? interactionPoint : transform;

        /// <inheritdoc />
        public bool IsInteractionEnabled => interactionEnabled && isActiveAndEnabled;

        /// <inheritdoc />
        public virtual string InteractionPrompt => interactionPrompt;

        /// <summary>
        /// 交互成功事件。
        /// </summary>
        public UnityEvent<GameObject> OnInteracted => onInteracted;

        /// <summary>
        /// 设置交互启用状态。
        /// 供门锁、机关冷却或剧情条件切换时调用。
        /// </summary>
        public void SetInteractionEnabled(bool enabled)
        {
            interactionEnabled = enabled;
        }

        /// <inheritdoc />
        public virtual bool CanInteract(GameObject interactor)
        {
            return IsInteractionEnabled && interactor != null;
        }

        /// <inheritdoc />
        public bool TryInteract(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return false;
            }

            if (!OnInteract(interactor))
            {
                return false;
            }

            onInteracted?.Invoke(interactor);
            return true;
        }

        /// <inheritdoc />
        public virtual Vector3 GetInteractionPoint(Vector3 fromPosition)
        {
            return InteractionTransform.position;
        }

        /// <summary>
        /// 具体交互行为扩展点。
        /// 默认返回 true，用于直接通过 UnityEvent 搭建白模交互。
        /// 子类可返回 false 拒绝本次交互，不触发通用事件。
        /// </summary>
        protected virtual bool OnInteract(GameObject interactor)
        {
            return true;
        }

        protected virtual void OnValidate()
        {
            if (interactionPrompt == null)
            {
                interactionPrompt = string.Empty;
            }
        }
    }
}
