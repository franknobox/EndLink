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
    }
}
