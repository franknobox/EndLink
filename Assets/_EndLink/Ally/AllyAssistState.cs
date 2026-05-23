using System.Collections.Generic;
using UnityEngine;

namespace EndLink.Ally
{
    /// <summary>
    /// 队友助战内部阶段。
    /// 这是 Assist 大状态内部的轻量二级状态，后续可以替换为行为树。
    /// </summary>
    public enum AllyAssistPhase
    {
        Approach = 0,
        Attack = 1
    }

    /// <summary>
    /// 队友助战大状态。
    /// 内部先接近目标，进入攻击距离后持续执行助战；目标拉开时重新接近。
    /// </summary>
    public sealed class AllyAssistState : AllyStateBase
    {
        private readonly List<Collider> _targetColliders = new();
        private AllyAssistPhase _phase;
        private float _elapsedTime;

        public AllyAssistState(AllyStateContext context) : base(context)
        {
        }

        public override AllyStateId StateId => AllyStateId.Assist;

        public override void Enter()
        {
            EnterApproachPhase();
        }

        public override void Tick(float deltaTime)
        {
            Transform target = Context.CurrentAssistTarget;
            if (target == null || !target.gameObject.activeInHierarchy || IsMainCharacterTooFar())
            {
                Context.StateMachine.CancelAssist(target);
                return;
            }

            switch (_phase)
            {
                case AllyAssistPhase.Approach:
                    TickApproach(target, deltaTime);
                    return;
                case AllyAssistPhase.Attack:
                    TickAttack(target, deltaTime);
                    return;
            }
        }

        private void TickApproach(Transform target, float deltaTime)
        {
            if (IsTargetInAttackRange(target))
            {
                EnterAttackPhase();
                return;
            }

            Vector3 approachPosition = AllyTargetingUtility.GetClosestPointOnTarget(
                target,
                Context.Transform.position,
                _targetColliders);

            Context.FollowMotor.TickMoveToPosition(approachPosition, Context.AssistAttackRange, deltaTime);
        }

        private void TickAttack(Transform target, float deltaTime)
        {
            if (IsTargetOutOfRange(target))
            {
                EnterApproachPhase();
                return;
            }

            _elapsedTime += deltaTime;

            if (_elapsedTime < Context.AssistDuration)
            {
                return;
            }

            if (!Context.CombatDriver.CanAssist)
            {
                return;
            }

            _elapsedTime = 0f;
            TryExecuteAssistOrCancel();
        }

        private void EnterApproachPhase()
        {
            _phase = AllyAssistPhase.Approach;
            _elapsedTime = 0f;
        }

        private void EnterAttackPhase()
        {
            _phase = AllyAssistPhase.Attack;
            _elapsedTime = 0f;
            TryExecuteAssistOrCancel();
        }

        private void TryExecuteAssistOrCancel()
        {
            bool executed = Context.CombatDriver.ExecuteAssist(Context.CurrentAssistTarget);
            if (!executed)
            {
                Context.StateMachine.CancelAssist(Context.CurrentAssistTarget);
            }
        }

        private bool IsTargetInAttackRange(Transform target)
        {
            float sqrAttackRange = Context.AssistAttackRange * Context.AssistAttackRange;
            float sqrDistanceToTarget = AllyTargetingUtility.GetHorizontalSqrDistanceToTarget(
                target,
                Context.Transform.position,
                _targetColliders);

            return sqrDistanceToTarget <= sqrAttackRange;
        }

        private bool IsTargetOutOfRange(Transform target)
        {
            float reengageRange = Mathf.Max(Context.AssistAttackRange, Context.AssistReengageRange);
            float sqrDistanceToTarget = AllyTargetingUtility.GetHorizontalSqrDistanceToTarget(
                target,
                Context.Transform.position,
                _targetColliders);

            return sqrDistanceToTarget > reengageRange * reengageRange;
        }

        private bool IsMainCharacterTooFar()
        {
            if (Context.FollowTarget == null || Context.AssistBreakOffDistance <= 0f)
            {
                return false;
            }

            Vector3 toMain = Context.FollowTarget.position - Context.Transform.position;
            toMain.y = 0f;

            return toMain.sqrMagnitude >= Context.AssistBreakOffDistance * Context.AssistBreakOffDistance;
        }
    }
}
