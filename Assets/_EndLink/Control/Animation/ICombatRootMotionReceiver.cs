using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 战斗 Root Motion 接收接口。
    /// 后续闪避、突进攻击或处决动画需要使用动画位移时，通过该接口把动画 Delta 交给角色移动层。
    /// </summary>
    public interface ICombatRootMotionReceiver
    {
        /// <summary>当前是否允许接收战斗 Root Motion 位移。</summary>
        bool CanReceiveCombatRootMotion { get; }

        /// <summary>
        /// 应用本帧动画位移和旋转。
        /// 实现方应自行决定如何交给 CharacterController、NavMeshAgent 或其它移动组件。
        /// </summary>
        void ApplyCombatRootMotion(Vector3 deltaPosition, Quaternion deltaRotation);
    }
}
