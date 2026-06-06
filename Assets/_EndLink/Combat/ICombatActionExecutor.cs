using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 角色战斗动作执行器的统一查询与执行契约。
    /// 玩家、队友和敌人的状态机、AI、路由及 UI 通过该接口查询动作条件和冷却，
    /// 具体 Hitbox 生成、朝向、日志与冷却存储仍由各自 Driver 负责。
    /// </summary>
    public interface ICombatActionExecutor
    {
        /// <summary>
        /// 判断指定动作是否具备执行层条件。
        /// 当前包括动作配置、Hitbox 资源和该动作自身冷却，不包含角色状态机限制。
        /// </summary>
        bool CanExecute(CombatActionDefinition actionDefinition);

        /// <summary>
        /// 尝试执行指定动作，并可传入本次动作的目标。
        /// 返回 true 表示动作表现、判定和冷却已经成功启动。
        /// </summary>
        bool TryExecute(CombatActionDefinition actionDefinition, Transform target = null);

        /// <summary>获取指定动作当前的冷却剩余时间，单位秒。</summary>
        float GetCooldownRemaining(CombatActionDefinition actionDefinition);

        /// <summary>
        /// 获取指定动作当前的归一化冷却进度。
        /// 1 表示刚进入冷却，0 表示动作可用。
        /// </summary>
        float GetCooldownNormalized(CombatActionDefinition actionDefinition);
    }
}
