using EndLink.Combat;
using UnityEngine;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人战斗大状态。
    /// 当前负责基础追击、攻击距离停位、面向目标和普通攻击循环；
    /// 之后可在这里接入行为树，处理站位、攻击和技能细节。
    /// </summary>
    public sealed class EnemyCombatState : EnemyStateBase
    {
        private const float MissingHitboxRetryInterval = 1f;

        private float _selfPlanarRadius;
        private float _nextAttackAttemptTime;
        private float _lostTargetElapsed;
        private bool _loggedMissingHitbox;

        public EnemyCombatState(EnemyStateContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public override EnemyStateId StateId => EnemyStateId.Combat;

        /// <inheritdoc />
        public override void Enter()
        {
            _selfPlanarRadius = EstimateSelfPlanarRadius();
            _nextAttackAttemptTime = 0f;
            _lostTargetElapsed = 0f;
            _loggedMissingHitbox = false;
        }

        /// <inheritdoc />
        public override void Exit()
        {
            Context.Motor?.Stop();
            Context.CombatDriver?.CancelCurrentAction();
        }

        /// <inheritdoc />
        public override void Tick(float deltaTime)
        {
            if (IsBeyondHomeLeash())
            {
                Context.StateMachine.RequestReturn();
                return;
            }

            if (!Context.HasValidTarget)
            {
                TickLostTarget(deltaTime);
                return;
            }

            _lostTargetElapsed = 0f;
            Transform target = Context.CurrentTarget;

            if (TryGetBasicAttackAction(out CombatActionDefinition basicAttackAction))
            {
                TickMeleeAttack(target, basicAttackAction, deltaTime);
                return;
            }

            TickChaseOnly(target, deltaTime);
        }

        private void TickLostTarget(float deltaTime)
        {
            Context.Motor?.Stop();
            _lostTargetElapsed += Mathf.Max(0f, deltaTime);

            if (_lostTargetElapsed < Context.LostTargetDelay)
            {
                return;
            }

            Context.StateMachine.SetTarget(null);
            Context.StateMachine.RequestReturn();
        }

        private void TickChaseOnly(Transform target, float deltaTime)
        {
            MoveTowardTargetSurface(target, GetChaseOnlyStopDistance(), deltaTime);
            Context.Motor?.FaceTarget(target, deltaTime);
        }

        private void TickMeleeAttack(Transform target, CombatActionDefinition action, float deltaTime)
        {
            float surfaceDistance = CombatTargetUtility.GetSurfaceDistance(
                target,
                Context.Transform.position);

            if (surfaceDistance > GetAttackEnterDistance(action))
            {
                MoveTowardTargetSurface(target, GetAttackApproachStopDistance(action), deltaTime);
                return;
            }

            Context.Motor?.Stop();
            Context.Motor?.FaceTarget(target, deltaTime);
            TryExecuteBasicAttack(target, action);
        }

        private void MoveTowardTargetSurface(Transform target, float stopDistance, float deltaTime)
        {
            Vector3 approachPoint = CombatTargetUtility.GetClosestPoint(
                target,
                Context.Transform.position);

            Context.Motor?.MoveTo(approachPoint, stopDistance, deltaTime);
        }

        private bool TryGetBasicAttackAction(out CombatActionDefinition action)
        {
            action = Context.CombatDriver != null ? Context.CombatDriver.BasicAttackAction : null;
            return action != null;
        }

        private void TryExecuteBasicAttack(Transform target, CombatActionDefinition action)
        {
            if (Context.ActionExecutor == null || action == null)
            {
                return;
            }

            float currentTime = Time.time;
            if (currentTime < _nextAttackAttemptTime)
            {
                return;
            }

            if (action.HitboxPrefab == null)
            {
                if (!_loggedMissingHitbox)
                {
                    Context.ActionExecutor.TryExecute(action, target);
                    _loggedMissingHitbox = true;
                }

                _nextAttackAttemptTime = currentTime + MissingHitboxRetryInterval;
                return;
            }

            if (!Context.ActionExecutor.CanExecute(action))
            {
                return;
            }

            if (Context.ActionExecutor.TryExecute(action, target))
            {
                _nextAttackAttemptTime = currentTime + GetAttackLockDuration(action);
            }
        }

        private float GetChaseOnlyStopDistance()
        {
            return _selfPlanarRadius + Mathf.Max(0f, Context.CombatChaseStopDistance);
        }

        private float GetAttackEnterDistance(CombatActionDefinition action)
        {
            return Mathf.Max(
                0.01f,
                action.EffectiveAttackRange + Context.CombatAttackRangeTolerance);
        }

        private float GetAttackApproachStopDistance(CombatActionDefinition action)
        {
            return Mathf.Max(
                0.01f,
                action.EffectiveAttackRange - Context.CombatAttackInnerOffset);
        }

        private static float GetAttackLockDuration(CombatActionDefinition action)
        {
            if (action == null)
            {
                return 0f;
            }

            return Mathf.Max(0f, action.Cooldown, action.TotalDuration);
        }

        private float EstimateSelfPlanarRadius()
        {
            CharacterController characterController = Context.Transform.GetComponent<CharacterController>();
            if (characterController != null)
            {
                float scale = Mathf.Max(
                    Mathf.Abs(characterController.transform.lossyScale.x),
                    Mathf.Abs(characterController.transform.lossyScale.z));

                return Mathf.Max(0f, characterController.radius * scale);
            }

            return 0f;
        }

        private bool IsBeyondHomeLeash()
        {
            if (Context.MaxChaseRadius <= 0f)
            {
                return false;
            }

            Vector3 offset = Context.Transform.position - Context.HomePosition;
            offset.y = 0f;
            return offset.sqrMagnitude > Context.MaxChaseRadius * Context.MaxChaseRadius;
        }
    }
}
