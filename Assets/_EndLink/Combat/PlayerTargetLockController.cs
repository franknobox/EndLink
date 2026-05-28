using EndLink.Core;
using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 玩家目标锁定控制器。
    /// 只负责把输入读取器里的“锁定/解锁”意图转发给 PlayerTargeting，
    /// 不参与目标评分、不控制相机、不生成 UI，也不决定攻击能否释放。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerTargeting))]
    public sealed class PlayerTargetLockController : MonoBehaviour
    {
        [Header("组件引用")]
        [Tooltip("玩家输入读取器。为空时会优先从当前物体和父物体自动查找。")]
        [SerializeField]
        private PlayerInputReader inputReader;

        [Tooltip("玩家目标选择组件。为空时会从当前物体自动获取。")]
        [SerializeField]
        private PlayerTargeting targeting;

        [Header("调试")]
        [Tooltip("锁定、解锁和未找到目标时是否打印调试日志。")]
        [SerializeField]
        private bool logTargetLockChanges;

        /// <summary>当前绑定的目标选择组件。</summary>
        public PlayerTargeting Targeting => targeting;

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
        }

        private void OnValidate()
        {
            CacheReferences();
        }

        private void Update()
        {
            if (inputReader == null || targeting == null)
            {
                return;
            }

            if (inputReader.ConsumeTargetLockPressed())
            {
                ToggleTargetLock();
            }
        }

        /// <summary>
        /// 切换玩家当前锁定目标。
        /// 已有目标时解锁；没有目标时尝试按 PlayerTargeting 的规则获取目标。
        /// </summary>
        public bool ToggleTargetLock()
        {
            if (targeting == null)
            {
                return false;
            }

            if (targeting.HasTarget)
            {
                Transform clearedTarget = targeting.CurrentTarget;
                targeting.ClearTarget();
                Log($"Target unlocked: {(clearedTarget != null ? clearedTarget.name : "None")}");
                return false;
            }

            bool acquired = targeting.TryAcquireTarget();
            Log(acquired
                ? $"Target locked: {targeting.CurrentTarget.name}"
                : "Target lock failed: no valid target");
            return acquired;
        }

        private void CacheReferences()
        {
            if (targeting == null)
            {
                TryGetComponent(out targeting);
            }

            if (inputReader == null)
            {
                inputReader = GetComponent<PlayerInputReader>();

                if (inputReader == null)
                {
                    inputReader = GetComponentInParent<PlayerInputReader>();
                }
            }
        }

        private void Log(string message)
        {
            if (logTargetLockChanges)
            {
                Debug.Log($"PlayerTargetLockController: {message}", this);
            }
        }
    }
}
