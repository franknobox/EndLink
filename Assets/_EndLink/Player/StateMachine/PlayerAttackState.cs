using EndLink.Combat;
using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 玩家普攻连段状态。
    /// 每一段仍由 PlayerCombatDriver 和 CombatActionDefinition 执行；本状态只处理输入窗口、段落衔接、攻击踏步和状态退出。
    /// </summary>
    public sealed class PlayerAttackState : PlayerStateBase
    {
        private CombatActionDefinition _currentAction;
        private float _currentDuration;
        private float _elapsedTime;
        private float _queuedStepWaitTime;
        private bool _actionStarted;
        private bool _waitForAnimationEnd;
        private bool _animationCanCancel;

        public PlayerAttackState(PlayerStateContext context) : base(context)
        {
        }

        public override PlayerStateId StateId => PlayerStateId.Attack;

        public override void Enter()
        {
            _elapsedTime = 0f;
            _actionStarted = false;

            _currentAction = Context.ComboController != null
                ? Context.ComboController.BeginCombo(Context.CombatDriver.BasicAttackAction)
                : Context.CombatDriver.BasicAttackAction;

            StartCurrentStep();
        }

        public override void Tick(float deltaTime)
        {
            if (!_actionStarted)
            {
                ExitToLocomotion();
                return;
            }

            _elapsedTime += deltaTime;
            Context.ConsumeJumpPressed();

            Vector2 attackMoveInput = Context.InputReader.MoveInput * Context.AttackMoveInputScale;
            Context.Controller.TickMovement(attackMoveInput, deltaTime);
            Context.TickAttackTargetFacing(deltaTime);
            Context.ComboController?.TickMotion(deltaTime);

            TryQueueNextStep();

            if (_waitForAnimationEnd)
            {
                return;
            }

            if (_elapsedTime < _currentDuration)
            {
                return;
            }

            if (TryStartQueuedStep())
            {
                return;
            }

            if (Context.ComboController != null && Context.ComboController.HasQueuedNext)
            {
                _queuedStepWaitTime += deltaTime;
                if (PlayerComboController.ShouldWaitForQueuedStep(
                        _queuedStepWaitTime,
                        Context.ComboController.QueuedStepTimeout))
                {
                    // Driver 可能比状态机晚一帧结束当前时序，短时间保留已缓存输入并重试。
                    return;
                }
            }

            ExitToLocomotion();
        }

        public override void Exit()
        {
            Context.ComboController?.ResetCombo();
            Context.CombatDriver?.CancelCurrentAction();
            _currentAction = null;
            _actionStarted = false;
            _waitForAnimationEnd = false;
            _animationCanCancel = false;
        }

        /// <summary>动画事件打开当前攻击的取消或连段输入窗口。</summary>
        public void NotifyActionCanCancel()
        {
            if (!_actionStarted || !_waitForAnimationEnd)
            {
                return;
            }

            _animationCanCancel = true;
            TryQueueNextStep();
        }

        /// <summary>Driver 通知当前段已经自然结束。</summary>
        public void NotifyActionEnded()
        {
            if (!_actionStarted)
            {
                return;
            }

            if (!_waitForAnimationEnd && _elapsedTime < _currentDuration)
            {
                return;
            }

            if (TryStartQueuedStep())
            {
                return;
            }

            ExitToLocomotion();
        }

        private void TryQueueNextStep()
        {
            if (Context.ComboController == null || !Context.ComboController.HasNext || _currentDuration <= 0f)
            {
                return;
            }

            float normalizedTime = Mathf.Clamp01(_elapsedTime / _currentDuration);
            bool canQueue = _animationCanCancel || Context.ComboController.CanQueueNext(normalizedTime);
            if (Context.StateMachine.TryConsumeAttackBuffer(canQueue))
            {
                Context.ComboController.TryQueueNext(normalizedTime);
            }
        }

        private bool TryStartQueuedStep()
        {
            if (Context.ComboController == null
                || !Context.ComboController.HasQueuedNext
                || Context.CombatDriver.IsExecutingAction)
            {
                return false;
            }

            CombatActionDefinition nextAction = Context.ComboController.NextAction;
            if (!Context.ActionExecutor.CanExecute(nextAction))
            {
                return false;
            }

            if (!Context.ComboController.TryAdvance())
            {
                return false;
            }

            _currentAction = Context.ComboController.CurrentAction;
            StartCurrentStep();
            return _actionStarted;
        }

        private void StartCurrentStep()
        {
            Transform target = Context.GetCurrentAttackTarget();
            _actionStarted = Context.ActionExecutor != null
                && Context.ActionExecutor.TryExecute(_currentAction, target);
            if (!_actionStarted)
            {
                return;
            }

            _elapsedTime = 0f;
            _queuedStepWaitTime = 0f;
            _currentDuration = Context.GetAttackDuration(_currentAction);
            _waitForAnimationEnd = _currentAction != null
                && _currentAction.TimingSource == CombatActionTimingSource.AnimationEventDriven;
            _animationCanCancel = false;
            Context.ComboController?.BeginStepMotion(target, Context.Transform.forward);
        }

        private void ExitToLocomotion()
        {
            Context.StateMachine.ChangeState(Context.HasMoveInput ? PlayerStateId.Move : PlayerStateId.Idle);
        }
    }
}
