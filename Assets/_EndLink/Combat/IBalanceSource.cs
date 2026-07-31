using System;

namespace EndLink.Combat
{
    /// <summary>
    /// 战斗单位向 UI 和调试工具暴露平衡值的只读接口。
    /// 敌人、主角或特殊目标可以拥有不同的平衡规则，但显示层只依赖这组统一数据。
    /// </summary>
    public interface IBalanceSource
    {
        /// <summary>当前平衡值。</summary>
        float CurrentBalance { get; }

        /// <summary>最大平衡值。</summary>
        float MaxBalance { get; }

        /// <summary>当前平衡值的 0 到 1 比例。</summary>
        float NormalizedBalance { get; }

        /// <summary>当前是否已经进入失衡状态。</summary>
        bool IsStaggered { get; }

        /// <summary>平衡值变化事件，参数依次为当前值和最大值。</summary>
        event Action<float, float> BalanceChanged;
    }
}
