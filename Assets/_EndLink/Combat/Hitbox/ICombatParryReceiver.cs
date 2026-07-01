using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>能够响应玩家成功弹反的战斗对象。</summary>
    public interface ICombatParryReceiver
    {
        void ReceiveParry(GameObject parrySource);
    }
}
