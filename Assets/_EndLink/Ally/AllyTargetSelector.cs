using System.Collections.Generic;
using EndLink.Combat;
using EndLink.Party;
using UnityEngine;

namespace EndLink.Ally
{
    /// <summary>
    /// 队友目标选择器。
    /// 只负责从小队战斗上下文里挑选一个当前可攻击目标，不负责移动、不负责攻击、不直接切状态。
    /// 后续切行为树时，这个组件可以继续作为黑板/服务节点的数据来源。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AllyTargetSelector : MonoBehaviour
    {
        [Header("战斗上下文")]
        [Tooltip("小队战斗上下文。通常挂在 Party_Root 上；留空时会自动在场景中查找。")]
        [SerializeField]
        private PartyCombatContext combatContext;

        [Header("选择规则")]
        [Tooltip("队友可选择目标的最大水平距离。为 0 时不限制距离。")]
        [SerializeField, Min(0f)]
        private float targetSearchRadius = 24f;

        [Tooltip("当前主目标的评分加成。数值越高，越倾向于继续集火主控刚攻击过的目标。")]
        [SerializeField, Min(0f)]
        private float primaryTargetScoreBonus = 10000f;

        [Header("调试")]
        [Tooltip("开启后会向 Ally Monitor 播报目标选择结果，方便排查队友为什么换目标或没有目标。")]
        [SerializeField]
        private bool logSelection;

        private readonly List<Transform> _knownEnemies = new();
        private readonly List<Collider> _colliderBuffer = new();

        /// <summary>当前绑定的小队战斗上下文。</summary>
        public PartyCombatContext CombatContext => combatContext;

        /// <summary>队友目标搜索半径。</summary>
        public float TargetSearchRadius => targetSearchRadius;

        private void Awake()
        {
            CacheReferences();
        }

        private void Reset()
        {
            CacheReferences();
        }

        private void OnValidate()
        {
            targetSearchRadius = Mathf.Max(0f, targetSearchRadius);
            primaryTargetScoreBonus = Mathf.Max(0f, primaryTargetScoreBonus);
        }

        /// <summary>
        /// 尝试选择一个可助战目标。
        /// 默认优先当前小队主目标，其次选择距离队友最近的已知敌人。
        /// </summary>
        public bool TrySelectTarget(out Transform selectedTarget)
        {
            return TrySelectTarget(null, out selectedTarget);
        }

        /// <summary>
        /// 尝试选择一个可助战目标。
        /// currentTarget 预留给后续稳定目标评分使用；当前阶段主要用于接口兼容和调试语义。
        /// </summary>
        public bool TrySelectTarget(Transform currentTarget, out Transform selectedTarget)
        {
            selectedTarget = null;

            if (combatContext == null)
            {
                CacheReferences();
            }

            if (combatContext == null || !combatContext.IsInCombat)
            {
                LogSelection("no combat context or not in combat");
                return false;
            }

            combatContext.GetKnownEnemies(_knownEnemies);

            Transform primaryTarget = combatContext.CurrentPrimaryTarget;
            Transform bestTarget = null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < _knownEnemies.Count; i++)
            {
                Transform candidate = _knownEnemies[i];
                if (!IsTargetSelectable(candidate))
                {
                    continue;
                }

                float sqrDistance = GetHorizontalSqrDistanceToTarget(
                    candidate,
                    transform.position,
                    _colliderBuffer);

                if (targetSearchRadius > 0f && sqrDistance > targetSearchRadius * targetSearchRadius)
                {
                    continue;
                }

                float score = -sqrDistance;
                if (candidate == primaryTarget)
                {
                    score += primaryTargetScoreBonus;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
                }
            }

            selectedTarget = bestTarget;
            LogSelection(bestTarget != null
                ? $"selected target={bestTarget.name}, primary={GetTransformName(primaryTarget)}, previous={GetTransformName(currentTarget)}"
                : $"no selectable target, known={_knownEnemies.Count}");

            return selectedTarget != null;
        }

        /// <summary>
        /// 判断一个 Transform 当前是否仍然能作为队友攻击目标。
        /// 敌人死亡后只要实现 ICombatTarget 并返回 false，就会被排除。
        /// </summary>
        public static bool IsTargetSelectable(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return false;
            }

            ICombatTarget combatTarget = target.GetComponentInParent<ICombatTarget>();
            return combatTarget == null || combatTarget.IsTargetable;
        }

        /// <summary>
        /// 获取目标身上距离 fromPosition 最近的 Collider 表面点。
        /// 如果目标没有可用 Collider，则回退到目标 Transform 位置。
        /// </summary>
        public static Vector3 GetClosestPointOnTarget(
            Transform target,
            Vector3 fromPosition,
            List<Collider> colliderBuffer)
        {
            if (target == null)
            {
                return fromPosition;
            }

            if (colliderBuffer == null)
            {
                return target.position;
            }

            colliderBuffer.Clear();
            target.GetComponentsInChildren(false, colliderBuffer);

            Vector3 bestPoint = target.position;
            float bestSqrDistance = GetHorizontalSqrDistance(fromPosition, bestPoint);

            for (int i = 0; i < colliderBuffer.Count; i++)
            {
                Collider targetCollider = colliderBuffer[i];
                if (targetCollider == null || !targetCollider.enabled || !targetCollider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 closestPoint = targetCollider.ClosestPoint(fromPosition);
                float sqrDistance = GetHorizontalSqrDistance(fromPosition, closestPoint);

                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestPoint = closestPoint;
                }
            }

            return bestPoint;
        }

        /// <summary>
        /// 计算 fromPosition 到目标 Collider 表面的水平距离平方。
        /// 如果目标没有可用 Collider，则回退到目标 Transform 位置。
        /// </summary>
        public static float GetHorizontalSqrDistanceToTarget(
            Transform target,
            Vector3 fromPosition,
            List<Collider> colliderBuffer)
        {
            Vector3 closestPoint = GetClosestPointOnTarget(target, fromPosition, colliderBuffer);
            return GetHorizontalSqrDistance(fromPosition, closestPoint);
        }

        private void CacheReferences()
        {
            if (combatContext != null)
            {
                return;
            }

            combatContext = GetComponentInParent<PartyCombatContext>();
            if (combatContext == null)
            {
                combatContext = FindFirstObjectByType<PartyCombatContext>();
            }
        }

        private void LogSelection(string message)
        {
            if (!logSelection)
            {
                return;
            }

            AllyDebugLog.Raise(gameObject, AllyDebugCategory.Brain, $"target selector: {message}");
        }

        private static string GetTransformName(Transform target)
        {
            return target != null ? target.name : "None";
        }

        private static float GetHorizontalSqrDistance(Vector3 fromPosition, Vector3 toPosition)
        {
            Vector3 offset = toPosition - fromPosition;
            offset.y = 0f;
            return offset.sqrMagnitude;
        }
    }
}
