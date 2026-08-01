using EndLink.Combat;
using UnityEngine;

namespace EndLink.World
{
    /// <summary>
    /// 关卡中固定使用的六种武器交互类型。
    /// 类型同时决定允许触发它的玩家武器形态，不再由 Combat Action 或战斗标签配置。
    /// </summary>
    public enum ObjInteractionType
    {
        [InspectorName("触发装置（A）")]
        TriggerDevice = 0,

        [InspectorName("网络结构（A）")]
        NetworkStructure = 1,

        [InspectorName("远程节点（B）")]
        RemoteNode = 2,

        [InspectorName("可破坏物（C）")]
        Breakable = 3,

        [InspectorName("重型物体（C）")]
        HeavyObject = 4,

        [InspectorName("受力机关（C）")]
        ForceMechanism = 5
    }

    /// <summary>六种交互类型的固定规则查询。</summary>
    public static class ObjInteractionTypeUtility
    {
        /// <summary>返回指定交互类型唯一对应的武器形态。</summary>
        public static PlayerWeaponForm GetRequiredForm(ObjInteractionType interactionType)
        {
            return interactionType switch
            {
                ObjInteractionType.RemoteNode => PlayerWeaponForm.B,
                ObjInteractionType.Breakable => PlayerWeaponForm.C,
                ObjInteractionType.HeavyObject => PlayerWeaponForm.C,
                ObjInteractionType.ForceMechanism => PlayerWeaponForm.C,
                _ => PlayerWeaponForm.A
            };
        }

        /// <summary>
        /// 网络结构和可破坏物允许通过多次有效命中累计进度；
        /// 其他类型每次有效命中都会直接尝试执行功能。
        /// </summary>
        public static bool UsesHitProgress(ObjInteractionType interactionType)
        {
            return interactionType is ObjInteractionType.NetworkStructure
                or ObjInteractionType.Breakable;
        }
    }
}
