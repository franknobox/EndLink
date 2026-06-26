using EndLink.Core;
using UnityEngine;

namespace EndLink.World
{
    /// <summary>
    /// 玩家世界交互桥接层。
    /// 只负责把 PlayerInputReader 的交互输入转发给 WorldInteractor，不承载具体机关逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(WorldInteractor))]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("玩家输入读取器。为空时会从同物体自动获取。")]
        [SerializeField]
        private PlayerInputReader inputReader;

        [Tooltip("玩家状态机。为空时会从同物体自动获取；缺失时不限制状态。")]
        [SerializeField]
        private PlayerStateMachine stateMachine;

        [Tooltip("世界交互者。为空时会从同物体自动获取。")]
        [SerializeField]
        private WorldInteractor worldInteractor;

        [Header("状态限制")]
        [Tooltip("开启后，只允许玩家在 Idle 或 Move 状态执行交互，避免攻击、受击、闪避过程中触发机关。")]
        [SerializeField]
        private bool requireLocomotionState = true;

        /// <summary>当前可交互目标。</summary>
        public WorldInteractable CurrentInteractable => worldInteractor != null ? worldInteractor.CurrentInteractable : null;

        /// <summary>当前交互提示文本。</summary>
        public string CurrentPrompt => worldInteractor != null ? worldInteractor.CurrentPrompt : string.Empty;

        private void Awake()
        {
            if (inputReader == null)
            {
                inputReader = GetComponent<PlayerInputReader>();
            }

            if (stateMachine == null)
            {
                TryGetComponent(out stateMachine);
            }

            if (worldInteractor == null)
            {
                worldInteractor = GetComponent<WorldInteractor>();
            }
        }

        private void Update()
        {
            if (inputReader == null || worldInteractor == null)
            {
                return;
            }

            if (!inputReader.ConsumeInteractPressed())
            {
                return;
            }

            if (!CanInteractNow())
            {
                return;
            }

            worldInteractor.TryInteractCurrent(gameObject);
        }

        private bool CanInteractNow()
        {
            if (!requireLocomotionState || stateMachine == null)
            {
                return true;
            }

            return stateMachine.CurrentStateId == PlayerStateId.Idle
                || stateMachine.CurrentStateId == PlayerStateId.Move;
        }

        private void Reset()
        {
            inputReader = GetComponent<PlayerInputReader>();
            TryGetComponent(out stateMachine);
            worldInteractor = GetComponent<WorldInteractor>();
        }
    }
}
