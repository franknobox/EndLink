using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 战斗目标的统一只读契约。
    /// 玩家、队友、敌人都由独立 CombatTarget 组件实现，调用方不再自行猜测目标根节点和 Collider 表面。
    /// </summary>
    public interface ICombatTarget
    {
        /// <summary>目标的唯一根身份，用于事件、缓存、去重和状态机目标引用。</summary>
        Transform RootTransform { get; }

        /// <summary>目标当前是否存活。</summary>
        bool IsAlive { get; }

        /// <summary>目标当前是否允许被锁定、选敌和作为主动攻击目标。</summary>
        bool IsTargetable { get; }

        /// <summary>瞄准、朝向和锁定标识使用的参考点。</summary>
        Transform LockPoint { get; }

        /// <summary>获取目标有效 Collider 表面距离指定位置最近的世界坐标点。</summary>
        Vector3 GetClosestPoint(Vector3 from);

        /// <summary>获取指定位置到目标有效 Collider 表面的水平距离。</summary>
        float GetSurfaceDistance(Vector3 from);
    }
}
