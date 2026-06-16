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

    /// <summary>
    /// 纯动作时序推进器。
    /// 只负责按 startup / active / recovery 推进相位，并在跨过 startup 边界时触发一次动作生效。
    /// </summary>
    public enum CombatActionPhase
    {
        Startup = 0,
        Active = 1,
        Recovery = 2,
        Completed = 3
    }

    /// <summary>
    /// 轻量动作时序运行时。
    /// Driver 持有它来判断何时真正提交动作效果，以及何时结束本次动作锁定。
    /// </summary>
    public sealed class CombatActionTimeline
    {
        public CombatActionTimeline(float startupDuration, float activeDuration, float recoveryDuration)
        {
            StartupDuration = Mathf.Max(0f, startupDuration);
            ActiveDuration = Mathf.Max(0f, activeDuration);
            RecoveryDuration = Mathf.Max(0f, recoveryDuration);
            Phase = CombatActionPhase.Startup;
        }

        public float StartupDuration { get; }

        public float ActiveDuration { get; }

        public float RecoveryDuration { get; }

        public float TotalDuration => StartupDuration + ActiveDuration + RecoveryDuration;

        public float ElapsedTime { get; private set; }

        public CombatActionPhase Phase { get; private set; }

        public bool HasTriggeredEffect { get; private set; }

        public bool IsCompleted => Phase == CombatActionPhase.Completed;

        public void Begin(out bool triggerEffect)
        {
            triggerEffect = false;

            if (IsCompleted)
            {
                return;
            }

            if (StartupDuration > 0f)
            {
                Phase = CombatActionPhase.Startup;
                return;
            }

            triggerEffect = true;
            HasTriggeredEffect = true;
            Phase = ActiveDuration > 0f ? CombatActionPhase.Active : CombatActionPhase.Recovery;

            if (TotalDuration <= 0f)
            {
                Phase = CombatActionPhase.Completed;
            }
        }

        public void Tick(float deltaTime, out bool triggerEffect, out bool completed)
        {
            triggerEffect = false;
            completed = false;

            if (IsCompleted)
            {
                completed = true;
                return;
            }

            float previousElapsed = ElapsedTime;
            ElapsedTime += Mathf.Max(0f, deltaTime);

            if (!HasTriggeredEffect && previousElapsed < StartupDuration && ElapsedTime >= StartupDuration)
            {
                HasTriggeredEffect = true;
                triggerEffect = true;
                Phase = ActiveDuration > 0f ? CombatActionPhase.Active : CombatActionPhase.Recovery;
            }

            float activeEndTime = StartupDuration + ActiveDuration;
            if (HasTriggeredEffect && ElapsedTime >= activeEndTime && !IsCompleted)
            {
                Phase = CombatActionPhase.Recovery;
            }

            if (ElapsedTime >= TotalDuration)
            {
                Phase = CombatActionPhase.Completed;
                completed = true;
            }
        }
    }
}
