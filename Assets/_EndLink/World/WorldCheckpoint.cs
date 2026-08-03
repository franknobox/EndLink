using UnityEngine;
using UnityEngine.Events;

namespace EndLink.World
{
    /// <summary>
    /// 检查点功能入口。
    /// 负责把关联的 WorldSpawnPoint 激活为当前复活点，实际恢复与复活流程交给 WorldRespawnManager；
    /// 由子物体 ObjInteractable 通过 IObjFunction 提交武器交互。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldCheckpoint : MonoBehaviour, IObjFunction
    {
        [Header("检查点")]
        [Tooltip("该检查点对应的通用出生位置。为空时读取同物体的 WorldSpawnPoint。")]
        [SerializeField]
        private WorldSpawnPoint spawnPoint;

        [Tooltip("可选的复活调度器。为空时使用当前场景已经启用的 WorldRespawnManager。")]
        [SerializeField]
        private WorldRespawnManager respawnManager;

        [Tooltip("是否允许在小队战斗上下文仍处于战斗状态时使用。魂类检查点建议保持关闭。")]
        [SerializeField]
        private bool allowDuringCombat;

        [Header("事件")]
        [Tooltip("检查点成功激活或休整时触发，参数为发起交互的对象。可用于点亮篝火、播放音效或特效。")]
        [SerializeField]
        private UnityEvent<GameObject> onActivated = new();

        /// <summary>该检查点对应的通用出生位置。</summary>
        public WorldSpawnPoint SpawnPoint => spawnPoint;

        /// <summary>当前是否已经被设置为复活点。</summary>
        public bool IsCurrentCheckpoint
        {
            get
            {
                WorldRespawnManager manager = ResolveManager();
                return manager != null && manager.CurrentSpawnPoint == spawnPoint;
            }
        }

        private void Awake()
        {
            CacheSpawnPoint();
        }

        private void Reset()
        {
            CacheSpawnPoint();
            EnsureCheckpointRole();
        }

        /// <summary>
        /// 接收通用 ObjInteractable 提交的武器交互请求。
        /// </summary>
        public bool TryExecute(ObjInteractionContext context)
        {
            GameObject interactor = context.Interactor;
            WorldRespawnManager manager = ResolveManager();
            if (!isActiveAndEnabled
                || interactor == null
                || spawnPoint == null
                || manager == null
                || !manager.CanUseCheckpoint(interactor, allowDuringCombat))
            {
                return false;
            }

            if (!manager.ActivateCheckpoint(spawnPoint, interactor))
            {
                return false;
            }

            onActivated?.Invoke(interactor);
            return true;
        }

        private void OnValidate()
        {
            CacheSpawnPoint();
            EnsureCheckpointRole();
        }

        private WorldRespawnManager ResolveManager()
        {
            return respawnManager != null ? respawnManager : WorldRespawnManager.Active;
        }

        private void CacheSpawnPoint()
        {
            if (spawnPoint == null)
            {
                spawnPoint = GetComponent<WorldSpawnPoint>();
            }
        }

        private void EnsureCheckpointRole()
        {
            spawnPoint?.AddRole(WorldSpawnRole.Checkpoint);
        }
    }
}
