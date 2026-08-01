using UnityEngine;

namespace EndLink.World
{
    /// <summary>
    /// 两层电梯的交互入口。
    /// 只有交互者位于平台乘客 Trigger 内且电梯已经停靠时，才接受本次交互。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ElevatorPlatform))]
    public sealed class ElevatorInteractable : WorldInteractable
    {
        [Header("电梯")]
        [Tooltip("负责实际移动的电梯平台。为空时从当前物体自动获取。")]
        [SerializeField]
        private ElevatorPlatform elevatorPlatform;

        [Tooltip("电梯停在下层时显示的交互提示。")]
        [SerializeField]
        private string upwardPrompt = "上行";

        [Tooltip("电梯停在上层时显示的交互提示。")]
        [SerializeField]
        private string downwardPrompt = "下行";

        /// <inheritdoc />
        public override string InteractionPrompt => elevatorPlatform != null
            && elevatorPlatform.State == ElevatorPlatformState.Upper
                ? downwardPrompt
                : upwardPrompt;

        private void Awake()
        {
            CachePlatform();
        }

        private void Reset()
        {
            CachePlatform();
        }

        /// <inheritdoc />
        public override bool CanInteract(GameObject interactor)
        {
            return base.CanInteract(interactor)
                && elevatorPlatform != null
                && elevatorPlatform.CanAcceptTravelRequest
                && elevatorPlatform.IsPassenger(interactor);
        }

        /// <inheritdoc />
        protected override bool OnInteract(GameObject interactor)
        {
            return elevatorPlatform != null && elevatorPlatform.TryStartTravel();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            upwardPrompt ??= string.Empty;
            downwardPrompt ??= string.Empty;
            CachePlatform();
        }

        private void CachePlatform()
        {
            if (elevatorPlatform == null)
            {
                elevatorPlatform = GetComponent<ElevatorPlatform>();
            }
        }
    }
}
