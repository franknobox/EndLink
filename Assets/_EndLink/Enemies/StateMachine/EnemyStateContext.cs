using EndLink.Combat;
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

        /// <summary>敌人的战斗执行器。Combat 状态会在攻击距离内调用其普通攻击。</summary>
        public EnemyCombatDriver CombatDriver => Actor != null ? Actor.CombatDriver : null;

        /// <summary>敌人的统一战斗动作执行接口。</summary>
        public ICombatActionExecutor ActionExecutor => Actor != null ? Actor.ActionExecutor : null;

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

        /// <summary>Combat 状态追击目标时保留的表面间隔。</summary>
        public float CombatChaseStopDistance => StateMachine.CombatChaseStopDistance;

        /// <summary>Combat 状态进入普通攻击距离时额外放宽的容差。</summary>
        public float CombatAttackRangeTolerance => StateMachine.CombatAttackRangeTolerance;

        /// <summary>Combat 状态接近攻击目标时，相对动作极限距离向内靠近的距离。</summary>
        public float CombatAttackInnerOffset => StateMachine.CombatAttackInnerOffset;

        /// <summary>敌人的归位位置。</summary>
        public Vector3 HomePosition => StateMachine.HomePosition;

        /// <summary>敌人距离 Home 允许的最大追击半径。</summary>
        public float MaxChaseRadius => StateMachine.MaxChaseRadius;

        /// <summary>当前目标失效后等待重新获取目标的时间。</summary>
        public float LostTargetDelay => StateMachine.LostTargetDelay;

        /// <summary>Return 状态抵达 Home 使用的水平停止距离。</summary>
        public float ReturnStopDistance => StateMachine.ReturnStopDistance;
    }
}
