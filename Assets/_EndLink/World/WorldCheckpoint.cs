using UnityEngine;

namespace EndLink.World
{
    /// <summary>
    /// 可交互的检查点入口。
    /// 负责把关联的 WorldSpawnPoint 激活为当前复活点，实际恢复与复活流程交给 WorldRespawnManager。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldCheckpoint : WorldInteractable
    {
        [Header("检查点")]
        [Tooltip("该检查点对应的通用出生位置。为空时读取同物体的 WorldSpawnPoint。")]
        [SerializeField]
        private WorldSpawnPoint spawnPoint;

        [Tooltip("可选的复活调度器。为空时使用当前场景已经启用的 WorldRespawnManager。")]
        [SerializeField]
        private WorldRespawnManager respawnManager;

        [Tooltip("首次或切换到该检查点时显示的提示。")]
        [SerializeField]
        private string activatePrompt = "激活检查点";

        [Tooltip("该检查点已经是当前复活点时显示的提示。再次交互仍会恢复玩家状态。")]
        [SerializeField]
        private string restPrompt = "休整";

        [Tooltip("是否允许在小队战斗上下文仍处于战斗状态时使用。魂类检查点建议保持关闭。")]
        [SerializeField]
        private bool allowDuringCombat;

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

        /// <inheritdoc />
        public override string InteractionPrompt => IsCurrentCheckpoint ? restPrompt : activatePrompt;

        private void Awake()
        {
            CacheSpawnPoint();
        }

        private void Reset()
        {
            CacheSpawnPoint();
            EnsureCheckpointRole();
        }

        /// <inheritdoc />
        public override bool CanInteract(GameObject interactor)
        {
            WorldRespawnManager manager = ResolveManager();
            return base.CanInteract(interactor)
                && spawnPoint != null
                && manager != null
                && manager.CanUseCheckpoint(interactor, allowDuringCombat);
        }

        /// <inheritdoc />
        protected override bool OnInteract(GameObject interactor)
        {
            WorldRespawnManager manager = ResolveManager();
            return manager != null && manager.ActivateCheckpoint(spawnPoint, interactor);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            activatePrompt ??= string.Empty;
            restPrompt ??= string.Empty;
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
