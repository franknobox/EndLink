using UnityEngine;

namespace EndLink.Ally
{
    /// <summary>
    /// 队友助战攻击状态。
    /// 目标有效且主控没有远离时会持续助战：动作窗口结束后等待冷却，冷却好了再次攻击。
    /// </summary>
    public sealed class AllyAssistState : AllyStateBase
    {
        private float _elapsedTime;

        public AllyAssistState(AllyStateContext context) : base(context)
        {
        }

        public override AllyStateId StateId => AllyStateId.Assist;

        public override void Enter()
        {
            _elapsedTime = 0f;
            TryExecuteAssistOrCancel();
        }

        public override void Tick(float deltaTime)
        {
            Transform target = Context.CurrentAssistTarget;
            if (target == null || !target.gameObject.activeInHierarchy || IsMainCharacterTooFar())
            {
                Context.StateMachine.CancelAssist(target);
                return;
            }

            if (IsTargetOutOfRange(target))
            {
                Context.StateMachine.ChangeState(AllyStateId.AssistApproach);
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

        private void TryExecuteAssistOrCancel()
        {
            bool executed = Context.CombatDriver.ExecuteAssist(Context.CurrentAssistTarget);
            if (!executed)
            {
                Context.StateMachine.CancelAssist(Context.CurrentAssistTarget);
            }
        }

        private bool IsTargetOutOfRange(Transform target)
        {
            float reengageRange = Mathf.Max(Context.AssistAttackRange, Context.AssistReengageRange);
            Vector3 toTarget = target.position - Context.Transform.position;
            toTarget.y = 0f;

            return toTarget.sqrMagnitude > reengageRange * reengageRange;
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
