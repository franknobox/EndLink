using EndLink.Combat;
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
        private AllyAssistPhase _phase;
        private float _elapsedTime;
        private bool _loggedCooldownWait;

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
            if (IsMainCharacterTooFar())
            {
                Context.StateMachine.CancelAssist(target);
                return;
            }

            if (!TryEnsureAssistTarget(ref target))
            {
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

        private bool TryEnsureAssistTarget(ref Transform target)
        {
            if (AllyTargetSelector.IsTargetSelectable(target))
            {
                return true;
            }

            if (Context.TargetSelector != null
                && Context.TargetSelector.TrySelectTarget(target, out Transform nextTarget)
                && Context.StateMachine.TrySwitchAssistTarget(nextTarget))
            {
                target = nextTarget;
                EnterApproachPhase();
                return true;
            }

            AllyDebugLog.Raise(
                Context.Transform.gameObject,
                AllyDebugCategory.Assist,
                $"assist target invalid and no replacement, target={GetTransformName(target)}");
            Context.StateMachine.CancelAssist(target);
            return false;
        }

        private void TickApproach(Transform target, float deltaTime)
        {
            if (IsTargetInAttackRange(target))
            {
                EnterAttackPhase();
                return;
            }

            Vector3 approachPosition = CombatTargetUtility.GetClosestPoint(
                target,
                Context.Transform.position);

            Context.FollowMotor.TickMoveToPosition(approachPosition, Context.AssistApproachStopDistance, deltaTime);
        }

        private void TickAttack(Transform target, float deltaTime)
        {
            if (IsTargetOutOfRange(target))
            {
                AllyDebugLog.Raise(
                    Context.Transform.gameObject,
                    AllyDebugCategory.Assist,
                    $"target out of range, return approach, target={target.name}, surfaceDist={GetSurfaceDistance(target):F2}, reengage={Context.AssistReengageRange:F2}");
                EnterApproachPhase();
                return;
            }

            _elapsedTime += deltaTime;

            if (_elapsedTime < Context.AssistDuration)
            {
                return;
            }

            if (TryExecuteAssistWhenReadyOrCancel())
            {
                _elapsedTime = 0f;
            }
        }

        private void EnterApproachPhase()
        {
            _phase = AllyAssistPhase.Approach;
            _elapsedTime = 0f;
            _loggedCooldownWait = false;
            AllyDebugLog.Raise(
                Context.Transform.gameObject,
                AllyDebugCategory.Assist,
                $"phase=Approach, target={GetTransformName(Context.CurrentAssistTarget)}, enterDistance={Context.AssistAttackEnterDistance:F2}, stopDistance={Context.AssistApproachStopDistance:F2}");
        }

        private void EnterAttackPhase()
        {
            _phase = AllyAssistPhase.Attack;
            _elapsedTime = 0f;
            _loggedCooldownWait = false;
            AllyDebugLog.Raise(
                Context.Transform.gameObject,
                AllyDebugCategory.Assist,
                $"phase=Attack, target={GetTransformName(Context.CurrentAssistTarget)}, surfaceDist={GetSurfaceDistance(Context.CurrentAssistTarget):F2}, enterDistance={Context.AssistAttackEnterDistance:F2}");
            TryExecuteAssistWhenReadyOrCancel();
        }

        private bool TryExecuteAssistWhenReadyOrCancel()
        {
            if (!Context.CombatDriver.HasAssistAction)
            {
                Context.StateMachine.CancelAssist(Context.CurrentAssistTarget);
                return false;
            }

            if (!Context.CombatDriver.CanAssist)
            {
                if (!_loggedCooldownWait)
                {
                    _loggedCooldownWait = true;
                    AllyDebugLog.Raise(
                        Context.Transform.gameObject,
                        AllyDebugCategory.Assist,
                        $"waiting cooldown, remaining={Context.CombatDriver.AssistCooldownRemaining:F2}");
                }

                return false;
            }

            bool executed = Context.CombatDriver.ExecuteAssist(Context.CurrentAssistTarget);
            if (!executed)
            {
                Context.StateMachine.CancelAssist(Context.CurrentAssistTarget);
            }

            _loggedCooldownWait = false;
            return executed;
        }

        private bool IsTargetInAttackRange(Transform target)
        {
            float attackEnterDistance = Context.AssistAttackEnterDistance;
            float surfaceDistance = CombatTargetUtility.GetSurfaceDistance(
                target,
                Context.Transform.position);

            return surfaceDistance <= attackEnterDistance;
        }

        private bool IsTargetOutOfRange(Transform target)
        {
            float reengageRange = Mathf.Max(Context.AssistAttackEnterDistance, Context.AssistReengageRange);
            float surfaceDistance = CombatTargetUtility.GetSurfaceDistance(
                target,
                Context.Transform.position);

            return surfaceDistance > reengageRange;
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

        private float GetSurfaceDistance(Transform target)
        {
            if (target == null)
            {
                return 0f;
            }

            return CombatTargetUtility.GetSurfaceDistance(
                target,
                Context.Transform.position);
        }

        private static string GetTransformName(Transform target)
        {
            return target != null ? target.name : "None";
        }
    }
}
