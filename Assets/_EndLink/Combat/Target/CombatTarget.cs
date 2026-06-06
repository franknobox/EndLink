using System.Collections.Generic;
using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 玩家、队友和敌人共用的唯一战斗目标身份。
    /// 统一提供根节点、存活/可选状态、锁定点以及 Collider 表面距离。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatTarget : MonoBehaviour, ICombatTarget
    {
        [Header("目标点")]
        [Tooltip("瞄准、攻击朝向和锁定逻辑使用的参考点。留空时使用角色根物体。")]
        [SerializeField]
        private Transform lockPoint;

        [Header("目标状态")]
        [Tooltip("是否允许被锁定、选敌和作为主动攻击目标。死亡时无论该值如何都会自动失效。")]
        [SerializeField]
        private bool targetable = true;

        private readonly List<Collider> _colliders = new();
        private readonly List<MonoBehaviour> _lifeStateCandidates = new();
        private ICombatTargetLifeState _lifeState;
        private bool _lifeStateCached;
        private bool _collidersCached;

        /// <inheritdoc />
        public Transform RootTransform => transform;

        /// <inheritdoc />
        public bool IsAlive
        {
            get
            {
                if (!_lifeStateCached
                    || _lifeState is Object unityObject && unityObject == null)
                {
                    CacheLifeState();
                }

                return _lifeState == null || _lifeState.IsAlive;
            }
        }

        /// <inheritdoc />
        public bool IsTargetable => targetable && IsAlive && isActiveAndEnabled && gameObject.activeInHierarchy;

        /// <inheritdoc />
        public Transform LockPoint => lockPoint != null ? lockPoint : transform;

        private void Awake()
        {
            RefreshCaches();
        }

        private void OnEnable()
        {
            RefreshCaches();
        }

        private void Reset()
        {
            lockPoint = null;
            targetable = true;
            RefreshCaches();
        }

        private void OnTransformChildrenChanged()
        {
            CacheColliders();
        }

        /// <summary>
        /// 手动设置目标是否允许被锁定和选取。
        /// 不影响生命值，也不能让已经死亡的目标重新变为有效目标。
        /// </summary>
        public void SetTargetable(bool value)
        {
            targetable = value;
        }

        /// <summary>
        /// 设置锁定参考点。传入 null 时恢复为角色根节点。
        /// </summary>
        public void SetLockPoint(Transform value)
        {
            lockPoint = value;
        }

        /// <summary>
        /// 重新查找生命状态来源和子级 Collider。
        /// 运行时动态添加生命组件或身体 Collider 后可以主动调用。
        /// </summary>
        public void RefreshCaches()
        {
            CacheLifeState();
            CacheColliders();
        }

        /// <inheritdoc />
        public Vector3 GetClosestPoint(Vector3 from)
        {
            if (!_collidersCached)
            {
                CacheColliders();
            }

            Vector3 bestPoint = RootTransform.position;
            float bestSqrDistance = float.PositiveInfinity;
            bool foundCollider = false;

            for (int i = 0; i < _colliders.Count; i++)
            {
                Collider candidate = _colliders[i];
                if (!IsUsableBodyCollider(candidate))
                {
                    continue;
                }

                Vector3 closestPoint = candidate.ClosestPoint(from);
                float sqrDistance = (closestPoint - from).sqrMagnitude;
                if (sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                bestPoint = closestPoint;
                foundCollider = true;
            }

            return foundCollider ? bestPoint : RootTransform.position;
        }

        /// <inheritdoc />
        public float GetSurfaceDistance(Vector3 from)
        {
            if (!_collidersCached)
            {
                CacheColliders();
            }

            float bestSqrDistance = float.PositiveInfinity;

            for (int i = 0; i < _colliders.Count; i++)
            {
                Collider candidate = _colliders[i];
                if (!IsUsableBodyCollider(candidate))
                {
                    continue;
                }

                Vector3 closestPoint = candidate.ClosestPoint(from);
                bestSqrDistance = Mathf.Min(
                    bestSqrDistance,
                    GetPlanarSqrDistance(from, closestPoint));
            }

            if (float.IsPositiveInfinity(bestSqrDistance))
            {
                bestSqrDistance = GetPlanarSqrDistance(from, RootTransform.position);
            }

            return Mathf.Sqrt(bestSqrDistance);
        }

        private void CacheLifeState()
        {
            _lifeState = null;
            _lifeStateCached = true;
            _lifeStateCandidates.Clear();
            GetComponents(_lifeStateCandidates);

            for (int i = 0; i < _lifeStateCandidates.Count; i++)
            {
                if (_lifeStateCandidates[i] is ICombatTargetLifeState candidate)
                {
                    _lifeState = candidate;
                    return;
                }
            }
        }

        private void CacheColliders()
        {
            _colliders.Clear();
            GetComponentsInChildren(false, _colliders);
            _collidersCached = true;
        }

        private static bool IsUsableBodyCollider(Collider candidate)
        {
            return candidate != null
                && candidate.enabled
                && !candidate.isTrigger
                && candidate.gameObject.activeInHierarchy;
        }

        private static float GetPlanarSqrDistance(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            offset.y = 0f;
            return offset.sqrMagnitude;
        }
    }
}
