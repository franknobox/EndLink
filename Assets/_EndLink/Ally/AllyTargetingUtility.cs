using System.Collections.Generic;
using UnityEngine;

namespace EndLink.Ally
{
    /// <summary>
    /// 队友目标距离工具。
    /// 用目标 Collider 表面计算接近和攻击距离，避免大型敌人按中心点判断导致队友贴边却无法攻击。
    /// </summary>
    public static class AllyTargetingUtility
    {
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

        private static float GetHorizontalSqrDistance(Vector3 fromPosition, Vector3 toPosition)
        {
            Vector3 offset = toPosition - fromPosition;
            offset.y = 0f;
            return offset.sqrMagnitude;
        }
    }
}
