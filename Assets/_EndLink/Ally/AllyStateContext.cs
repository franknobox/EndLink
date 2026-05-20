using UnityEngine;

namespace EndLink.Ally
{
    /// <summary>
    /// 队友状态共享上下文。
    /// 状态对象通过上下文访问状态机、Transform、战斗执行器和跟随移动器，避免每个状态重复 GetComponent。
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

        /// <summary>队友跟随移动器，只在 Follow 状态中被 Tick 驱动。</summary>
        public AllyFollowMotor FollowMotor { get; }

        /// <summary>当前跟随目标。</summary>
        public Transform FollowTarget => StateMachine.FollowTarget;

        /// <summary>当前助战目标。</summary>
        public Transform CurrentAssistTarget => StateMachine.CurrentAssistTarget;

        /// <summary>当前助战状态持续时间。</summary>
        public float AssistDuration => StateMachine.CurrentAssistDuration;

        /// <summary>受击状态持续时间。</summary>
        public float HitDuration => StateMachine.HitDuration;
    }
}
