using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 可接收外部位移的角色接口。
    /// 敌人正常移动撞到玩家或队友时，会通过这个接口把挡路角色挤开；
    /// 敌人自身不实现该接口，因此玩家和队友不会通过同一套逻辑反向顶动敌人。
    /// </summary>
    public interface IExternalDisplacementReceiver
    {
        /// <summary>当前是否允许接收外部位移。</summary>
        bool CanReceiveExternalDisplacement { get; }

        /// <summary>
        /// 施加一段外部位移。
        /// 调用方应传入本帧位移量，而不是速度；实现方会自行决定如何交给 CharacterController 或 Transform。
        /// </summary>
        void AddExternalDisplacement(Vector3 displacement);
    }
}
