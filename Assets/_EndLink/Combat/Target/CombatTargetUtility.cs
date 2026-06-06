using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 战斗目标解析辅助入口。
    /// 所有 Collider、子节点和事件对象都通过这里归一到 CombatTarget.RootTransform。
    /// </summary>
    public static class CombatTargetUtility
    {
        /// <summary>从组件所在节点向父级查找统一战斗目标。</summary>
        public static bool TryResolve(Component source, out ICombatTarget target)
        {
            target = source != null ? source.GetComponentInParent<ICombatTarget>() : null;
            return IsUnityObjectAlive(target);
        }

        /// <summary>从 Transform 所在节点向父级查找统一战斗目标。</summary>
        public static bool TryResolve(Transform source, out ICombatTarget target)
        {
            return TryResolve(source as Component, out target);
        }

        /// <summary>从 GameObject 所在节点向父级查找统一战斗目标。</summary>
        public static bool TryResolve(GameObject source, out ICombatTarget target)
        {
            return TryResolve(source != null ? source.transform : null, out target);
        }

        /// <summary>解析有效目标并返回其唯一根身份。</summary>
        public static bool TryResolveTargetableRoot(Component source, out Transform root)
        {
            root = null;
            if (!TryResolve(source, out ICombatTarget target) || !target.IsTargetable)
            {
                return false;
            }

            root = target.RootTransform;
            return root != null;
        }

        /// <summary>判断 Transform 是否属于当前有效的统一战斗目标。</summary>
        public static bool IsTargetable(Transform source)
        {
            return TryResolve(source, out ICombatTarget target) && target.IsTargetable;
        }

        /// <summary>把任意目标子节点归一为唯一根身份；解析失败时返回 null。</summary>
        public static Transform ResolveRoot(Transform source)
        {
            return TryResolve(source, out ICombatTarget target) ? target.RootTransform : null;
        }

        /// <summary>获取目标 Collider 表面最近点；解析失败时回退到输入 Transform 位置。</summary>
        public static Vector3 GetClosestPoint(Transform source, Vector3 from)
        {
            if (TryResolve(source, out ICombatTarget target))
            {
                return target.GetClosestPoint(from);
            }

            return source != null ? source.position : from;
        }

        /// <summary>获取到目标 Collider 表面的水平距离；解析失败时按 Transform 位置计算。</summary>
        public static float GetSurfaceDistance(Transform source, Vector3 from)
        {
            if (TryResolve(source, out ICombatTarget target))
            {
                return target.GetSurfaceDistance(from);
            }

            if (source == null)
            {
                return 0f;
            }

            Vector3 offset = source.position - from;
            offset.y = 0f;
            return offset.magnitude;
        }

        private static bool IsUnityObjectAlive(ICombatTarget target)
        {
            return target is Component component && component != null;
        }
    }
}
