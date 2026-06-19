namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人在战斗中的基础定位。
    /// 该枚举只描述“敌人主要怎么参与战斗”，不自动覆盖移动、动作、感知或数值配置。
    /// </summary>
    public enum EnemyCombatRole
    {
        /// <summary>地面近战单位。依靠接近目标后使用近距离攻击。</summary>
        GroundMelee = 0,

        /// <summary>地面远程单位。保持地面移动能力，但主要通过远程动作输出。</summary>
        GroundRanged = 1,

        /// <summary>浮空远程单位。通常用于游离态或特殊悬浮敌人，后续可接入独立移动方式。</summary>
        FlyingRanged = 2
    }
}
