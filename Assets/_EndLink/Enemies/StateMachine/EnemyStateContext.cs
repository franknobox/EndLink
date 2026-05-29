using UnityEngine;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人状态共享上下文。
    /// 状态通过它访问敌人身份、生命和当前目标，避免每个状态重复 GetComponent。
    /// </summary>
    public sealed class EnemyStateContext
    {
        public EnemyStateContext(
            EnemyStateMachine stateMachine,
            EnemyActor actor,
            EnemyHealth health,
            Transform transform)
        {
            StateMachine = stateMachine;
            Actor = actor;
            Health = health;
            Transform = transform;
        }

        /// <summary>敌人状态机。</summary>
        public EnemyStateMachine StateMachine { get; }

        /// <summary>敌人根入口。</summary>
        public EnemyActor Actor { get; }

        /// <summary>敌人生命组件。</summary>
        public EnemyHealth Health { get; }

        /// <summary>敌人的移动能力组件。没有移动能力的敌人可以为空。</summary>
        public EnemyMotorBase Motor => Actor != null ? Actor.Motor : null;

        /// <summary>敌人的战斗执行器。当前基础 Combat 状态不会自动调用它。</summary>
        public EnemyCombatDriver CombatDriver => Actor != null ? Actor.CombatDriver : null;

        /// <summary>敌人根 Transform。</summary>
        public Transform Transform { get; }

        /// <summary>当前敌人关注或战斗的目标。</summary>
        public Transform CurrentTarget => StateMachine.CurrentTarget;

        /// <summary>当前目标是否仍然有效。</summary>
        public bool HasValidTarget => StateMachine.HasValidTarget;

        /// <summary>警觉状态持续时间。</summary>
        public float AlertDuration => StateMachine.AlertDuration;

        /// <summary>Alert 到 Combat / Idle 的转换是否由外部索敌组件控制。</summary>
        public bool AlertTransitionExternallyControlled => StateMachine.AlertTransitionExternallyControlled;

        /// <summary>受击硬直持续时间。</summary>
        public float HitDuration => StateMachine.HitDuration;

        /// <summary>Combat 状态追击目标时的停止距离。</summary>
        public float CombatChaseStopDistance => StateMachine.CombatChaseStopDistance;

        /// <summary>Combat 状态目标超过该距离时脱战。小于等于 0 表示不按距离脱战。</summary>
        public float CombatLeashDistance => StateMachine.CombatLeashDistance;
    }
}
