using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 可作为战斗目标的对象接口。
    /// 目标选择、AI 和 Hitbox 可以通过它判断对象是否仍然有效，例如死亡后不再可锁定。
    /// </summary>
    public interface ICombatTarget
    {
        /// <summary>当前是否可作为战斗目标。</summary>
        bool IsTargetable { get; }

        /// <summary>用于锁定、寻路和计算距离的目标 Transform。</summary>
        Transform TargetTransform { get; }
    }
}
