using System;
using UnityEngine;

namespace EndLink.World
{
    /// <summary>
    /// 世界中的通用出生位置类型。
    /// 同一个点可以同时承担开场出生、检查点和敌人生成位置，但具体生成逻辑由外部系统负责。
    /// </summary>
    [Flags]
    public enum WorldSpawnRole
    {
        None = 0,
        PlayerStart = 1 << 0,
        Checkpoint = 1 << 1,
        EnemySpawn = 1 << 2
    }

    /// <summary>
    /// 只描述一个稳定的世界位置、朝向和用途，不负责生成或复活任何对象。
    /// 玩家复活、场景入口、敌人生成器和后续存档系统都可以引用该组件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldSpawnPoint : MonoBehaviour
    {
        private const float MinGizmoRadius = 0.15f;
        private const float DirectionGizmoLength = 0.8f;

        [Header("出生点身份")]
        [Tooltip("稳定且唯一的出生点 ID。未来存档只保存该 ID，不依赖可能被修改的物体名称。")]
        [SerializeField]
        private string pointId;

        [Tooltip("这个位置允许承担的用途，可同时选择开场出生、检查点和敌人生成点。")]
        [SerializeField]
        private WorldSpawnRole roles = WorldSpawnRole.PlayerStart;

        [Tooltip("供未来敌人批量生成或安全落点查询使用的建议半径。玩家复活第一版仍使用中心点精确位置。")]
        [SerializeField, Min(0f)]
        private float spawnRadius = 0.5f;

        /// <summary>供存档和跨系统引用的稳定 ID。</summary>
        public string PointId => pointId;

        /// <summary>当前出生点承担的用途。</summary>
        public WorldSpawnRole Roles => roles;

        /// <summary>建议生成范围半径。</summary>
        public float SpawnRadius => spawnRadius;

        /// <summary>出生位置和完整朝向。</summary>
        public Pose SpawnPose => new(transform.position, transform.rotation);

        /// <summary>判断该位置是否支持指定用途。</summary>
        public bool HasRole(WorldSpawnRole role)
        {
            return role != WorldSpawnRole.None && (roles & role) == role;
        }

        /// <summary>
        /// 为出生点补充用途。
        /// 检查点等专用组件在编辑阶段可用它保证配置一致，不会移除已有用途。
        /// </summary>
        public void AddRole(WorldSpawnRole role)
        {
            roles |= role;
        }

        private void Reset()
        {
            EnsurePointId();
        }

        private void OnValidate()
        {
            spawnRadius = Mathf.Max(0f, spawnRadius);
            EnsurePointId();
        }

        [ContextMenu("重新生成出生点 ID")]
        private void RegeneratePointId()
        {
            pointId = Guid.NewGuid().ToString("N");
        }

        private void EnsurePointId()
        {
            if (string.IsNullOrWhiteSpace(pointId))
            {
                RegeneratePointId();
            }
        }

        private void OnDrawGizmos()
        {
            Color previousColor = Gizmos.color;
            Gizmos.color = GetGizmoColor();

            Vector3 origin = transform.position;
            float radius = Mathf.Max(MinGizmoRadius, spawnRadius);
            Gizmos.DrawWireSphere(origin, radius);

            Vector3 forward = transform.forward;
            if (forward.sqrMagnitude > 0.0001f)
            {
                Gizmos.DrawRay(origin, forward.normalized * DirectionGizmoLength);
            }

            Gizmos.color = previousColor;
        }

        private Color GetGizmoColor()
        {
            if (HasRole(WorldSpawnRole.Checkpoint))
            {
                return new Color(0.2f, 0.85f, 1f, 0.9f);
            }

            if (HasRole(WorldSpawnRole.EnemySpawn))
            {
                return new Color(1f, 0.3f, 0.2f, 0.9f);
            }

            return new Color(0.35f, 1f, 0.45f, 0.9f);
        }
    }
}
