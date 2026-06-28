using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 可接收外部位移的角色接口。
    /// 敌人移动推挤、移动平台等外部系统通过这个接口传递本帧位移；
    /// 调用方负责决定允许的位移方向，例如敌人只传水平位移，电梯可以传三维位移。
    /// </summary>
    public interface IExternalDisplacementReceiver
    {
        /// <summary>当前是否允许接收外部位移。</summary>
        bool CanReceiveExternalDisplacement { get; }

        /// <summary>
        /// 施加一段外部位移。
        /// 调用方应传入本帧三维位移量，而不是速度；实现方会自行决定如何交给 CharacterController 或 Transform。
        /// </summary>
        void AddExternalDisplacement(Vector3 displacement);
    }
}
