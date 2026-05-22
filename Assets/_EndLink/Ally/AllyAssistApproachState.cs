using UnityEngine;

namespace EndLink.Ally
{
    /// <summary>
    /// 队友助战接近状态。
    /// 负责让队友先跑到目标附近；进入攻击距离后再切到 Assist 执行真正攻击。
    /// </summary>
    public sealed class AllyAssistApproachState : AllyStateBase
    {
        private float _elapsedTime;

        public AllyAssistApproachState(AllyStateContext context) : base(context)
        {
        }

        public override AllyStateId StateId => AllyStateId.AssistApproach;

        public override void Enter()
        {
            _elapsedTime = 0f;
        }

        public override void Tick(float deltaTime)
        {
            _elapsedTime += deltaTime;

            Transform target = Context.CurrentAssistTarget;
            if (target == null || !target.gameObject.activeInHierarchy || IsMainCharacterTooFar())
            {
                Context.StateMachine.CancelAssist(target);
                return;
            }

            if (_elapsedTime >= Context.AssistApproachTimeout)
            {
                Context.StateMachine.CancelAssist(target);
                return;
            }

            float sqrAttackRange = Context.AssistAttackRange * Context.AssistAttackRange;
            Vector3 toTarget = target.position - Context.Transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= sqrAttackRange)
            {
                Context.StateMachine.ChangeState(AllyStateId.Assist);
                return;
            }

            Context.FollowMotor.TickMoveToPosition(target.position, Context.AssistAttackRange, deltaTime);
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
