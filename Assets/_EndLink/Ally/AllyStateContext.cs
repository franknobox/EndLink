using UnityEngine;

namespace EndLink.Ally
{
    /// <summary>
    /// 队友状态共享上下文。
    /// 状态对象通过上下文访问状态机、Transform、战斗执行器和移动器，避免每个状态重复 GetComponent。
    /// </summary>
    public sealed class AllyStateContext
    {
        public AllyStateContext(
            AllyStateMachine stateMachine,
            Transform transform,
            AllyCombatDriver combatDriver,
            AllyFollowMotor followMotor)
        {
            StateMachine = stateMachine;
            Transform = transform;
            CombatDriver = combatDriver;
            FollowMotor = followMotor;
        }

        /// <summary>队友状态机。</summary>
        public AllyStateMachine StateMachine { get; }

        /// <summary>队友根物体 Transform。</summary>
        public Transform Transform { get; }

        /// <summary>队友战斗执行器，只负责生成 Hitbox 和执行动作。</summary>
        public AllyCombatDriver CombatDriver { get; }

        /// <summary>队友移动器，负责跟随、接近目标和局部避让。</summary>
        public AllyFollowMotor FollowMotor { get; }

        /// <summary>当前跟随目标，通常是固定主控。</summary>
        public Transform FollowTarget => StateMachine.FollowTarget;

        /// <summary>当前助战目标。</summary>
        public Transform CurrentAssistTarget => StateMachine.CurrentAssistTarget;

        /// <summary>当前助战攻击状态持续时间。</summary>
        public float AssistDuration => StateMachine.CurrentAssistDuration;

        /// <summary>助战接近时进入攻击的距离。</summary>
        public float AssistAttackRange => StateMachine.AssistAttackRange;

        /// <summary>持续助战时重新接近目标的距离。</summary>
        public float AssistReengageRange => StateMachine.AssistReengageRange;

        /// <summary>主控离队友过远时放弃助战的距离。</summary>
        public float AssistBreakOffDistance => StateMachine.AssistBreakOffDistance;

        /// <summary>受击状态持续时间。</summary>
        public float HitDuration => StateMachine.HitDuration;
    }
}
