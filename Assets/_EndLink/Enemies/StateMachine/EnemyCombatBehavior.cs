using EndLink.Combat;
using UnityEngine;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人 Combat 大状态内部的基础战斗阶段。
    /// 当前只描述单个敌人面对单个目标时的接近、定位、攻击和恢复流程。
    /// </summary>
    public enum EnemyCombatPhase
    {
        Approach = 0,
        Position = 1,
        Attack = 2,
        Recover = 3
    }

    /// <summary>
    /// 敌人单人战斗行为状态机。
    /// 只决定 Combat 内部何时移动、停位和请求普通攻击；
    /// 脱战、受击、死亡和返场仍由外层 EnemyStateMachine 管理。
    /// </summary>
    public sealed class EnemyCombatBehavior
    {
        private const float MissingHitboxRetryInterval = 1f;

        private readonly EnemyStateContext _context;
        private float _selfPlanarRadius;
        private float _nextAttackAttemptTime;
        private float _attackWaitElapsed;
        private Transform _grantedAttackTarget;
        private EnemyCombatCoordinator _lastAttackCoordinator;
        private EnemyCombatCoordinator _grantedAttackCoordinator;
        private bool _loggedMissingHitbox;

        public EnemyCombatBehavior(EnemyStateContext context)
        {
            _context = context;
        }

        /// <summary>当前 Combat 内部阶段，供调试工具和后续行为扩展读取。</summary>
        public EnemyCombatPhase CurrentPhase { get; private set; } = EnemyCombatPhase.Approach;

        /// <summary>进入 Combat 大状态时重置本轮战斗行为。</summary>
        public void Enter()
        {
            _selfPlanarRadius = EstimateSelfPlanarRadius();
            _nextAttackAttemptTime = 0f;
            _attackWaitElapsed = 0f;
            _grantedAttackTarget = null;
            _lastAttackCoordinator = null;
            _grantedAttackCoordinator = null;
            _loggedMissingHitbox = false;
            CurrentPhase = EnemyCombatPhase.Approach;
        }

        /// <summary>离开 Combat 大状态时停止移动并取消尚未完成的动作。</summary>
        public void Exit()
        {
            CancelCurrentAttempt();
        }

        /// <summary>取消当前攻击尝试并释放围攻名额，用于目标丢失、受击打断、脱战或死亡。</summary>
        public void CancelCurrentAttempt()
        {
            ReleaseAttackSlot();
            _context?.Motor?.Stop();
            _context?.CombatDriver?.CancelCurrentAction();
            _attackWaitElapsed = 0f;
            CurrentPhase = EnemyCombatPhase.Approach;
        }

        /// <summary>推进一次 Combat 内部战斗决策。</summary>
        public void Tick(float deltaTime)
        {
            if (_context == null || !_context.HasValidTarget)
            {
                _context?.Motor?.Stop();
                return;
            }

            Transform target = _context.CurrentTarget;
            if (!TryGetBasicAttackAction(out CombatActionDefinition action))
            {
                CurrentPhase = EnemyCombatPhase.Approach;
                TickChaseOnly(target, deltaTime);
                return;
            }

            switch (CurrentPhase)
            {
                case EnemyCombatPhase.Position:
                    TickPosition(target, action, deltaTime);
                    break;

                case EnemyCombatPhase.Attack:
                    TickAttack(target, action, deltaTime);
                    break;

                case EnemyCombatPhase.Recover:
                    TickRecover(target, action, deltaTime);
                    break;

                default:
                    TickApproach(target, action, deltaTime);
                    break;
            }
        }

        private void TickApproach(Transform target, CombatActionDefinition action, float deltaTime)
        {
            float surfaceDistance = GetSurfaceDistance(target);
            float positionEnterDistance = GetPositionEnterDistance(action);

            if (surfaceDistance > positionEnterDistance)
            {
                _attackWaitElapsed = 0f;
                MoveTowardTargetSurface(target, positionEnterDistance, deltaTime);
                return;
            }

            _context.Motor?.Stop();
            _context.Motor?.FaceTarget(target, deltaTime);
            CurrentPhase = EnemyCombatPhase.Position;
        }

        private void TickPosition(Transform target, CombatActionDefinition action, float deltaTime)
        {
            if (GetSurfaceDistance(target) > GetPositionExitDistance(action))
            {
                CurrentPhase = EnemyCombatPhase.Approach;
                _attackWaitElapsed = 0f;
                TickApproach(target, action, deltaTime);
                return;
            }

            _context.Motor?.Stop();
            _context.Motor?.FaceTarget(target, deltaTime);
            _attackWaitElapsed += Mathf.Max(0f, deltaTime);
            TryStartAttack(target, action);
        }

        private void TickAttack(Transform target, CombatActionDefinition action, float deltaTime)
        {
            _context.Motor?.Stop();
            _context.Motor?.FaceTarget(target, deltaTime);

            EnemyCombatDriver combatDriver = _context.CombatDriver;
            if (combatDriver != null
                && combatDriver.IsExecutingAction
                && combatDriver.CurrentActionPhase != CombatActionPhase.Recovery)
            {
                return;
            }

            CurrentPhase = EnemyCombatPhase.Recover;
            TickRecover(target, action, deltaTime);
        }

        private void TickRecover(Transform target, CombatActionDefinition action, float deltaTime)
        {
            _context.Motor?.Stop();
            _context.Motor?.FaceTarget(target, deltaTime);

            if (_context.CombatDriver != null && _context.CombatDriver.IsExecutingAction)
            {
                return;
            }

            ReleaseAttackSlot();
            CurrentPhase = GetSurfaceDistance(target) > GetPositionExitDistance(action)
                ? EnemyCombatPhase.Approach
                : EnemyCombatPhase.Position;
        }

        private void TickChaseOnly(Transform target, float deltaTime)
        {
            MoveTowardTargetSurface(target, GetChaseOnlyStopDistance(), deltaTime);
            _context.Motor?.FaceTarget(target, deltaTime);
        }

        private void TryStartAttack(Transform target, CombatActionDefinition action)
        {
            ICombatActionExecutor actionExecutor = _context.ActionExecutor;
            if (actionExecutor == null || action == null)
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
                    actionExecutor.TryExecute(action, target);
                    _loggedMissingHitbox = true;
                }

                _nextAttackAttemptTime = currentTime + MissingHitboxRetryInterval;
                return;
            }

            if (!actionExecutor.CanExecute(action) || !TryAcquireAttackPermission(target, action))
            {
                return;
            }

            if (actionExecutor.TryExecute(action, target))
            {
                NotifyAttackStarted(target);
                _attackWaitElapsed = 0f;
                CurrentPhase = EnemyCombatPhase.Attack;
            }
        }

        private bool TryAcquireAttackPermission(Transform target, CombatActionDefinition action)
        {
            EnemyCombatCoordinator coordinator = _context.CombatCoordinator;
            if (coordinator == null)
            {
                return true;
            }

            float score = CalculateAttackScore(target, action);
            _lastAttackCoordinator = coordinator;
            return coordinator.RequestAttackPermission(
                _context.Transform,
                target,
                score);
        }

        private float CalculateAttackScore(Transform target, CombatActionDefinition action)
        {
            float surfaceDistance = GetSurfaceDistance(target);
            float rangeCloseness = Mathf.Clamp01(1f - surfaceDistance / Mathf.Max(0.01f, action.EffectiveAttackRange));
            float waitScore = Mathf.Clamp(_attackWaitElapsed, 0f, 5f);
            EnemyCombatCoordinator coordinator = _context.CombatCoordinator;
            float distanceWeight = coordinator != null ? coordinator.AttackScoreDistanceWeight : 1f;
            float waitWeight = coordinator != null ? coordinator.AttackScoreWaitWeight : 0f;
            return rangeCloseness * distanceWeight + waitScore * waitWeight;
        }

        private void NotifyAttackStarted(Transform target)
        {
            EnemyCombatCoordinator coordinator = _lastAttackCoordinator != null
                ? _lastAttackCoordinator
                : _context.CombatCoordinator;
            if (coordinator == null)
            {
                return;
            }

            _grantedAttackTarget = target;
            _grantedAttackCoordinator = coordinator;
            coordinator.NotifyAttackStarted(_context.Transform, target);
        }

        private void ReleaseAttackSlot()
        {
            if (_grantedAttackCoordinator != null && _grantedAttackTarget != null)
            {
                _grantedAttackCoordinator.NotifyAttackEnded(_context.Transform, _grantedAttackTarget);
            }

            _lastAttackCoordinator?.CancelAttacker(_context.Transform);
            _grantedAttackTarget = null;
            _lastAttackCoordinator = null;
            _grantedAttackCoordinator = null;
        }

        private void MoveTowardTargetSurface(Transform target, float stopDistance, float deltaTime)
        {
            Vector3 approachPoint = CombatTargetUtility.GetClosestPoint(
                target,
                _context.Transform.position);

            _context.Motor?.MoveTo(approachPoint, stopDistance, deltaTime);
        }

        private bool TryGetBasicAttackAction(out CombatActionDefinition action)
        {
            action = _context.CombatDriver != null ? _context.CombatDriver.BasicAttackAction : null;
            return action != null;
        }

        private float GetSurfaceDistance(Transform target)
        {
            return CombatTargetUtility.GetSurfaceDistance(target, _context.Transform.position);
        }

        private float GetChaseOnlyStopDistance()
        {
            return _selfPlanarRadius + Mathf.Max(0f, _context.CombatChaseStopDistance);
        }

        private float GetPositionEnterDistance(CombatActionDefinition action)
        {
            return Mathf.Max(
                0.01f,
                action.EffectiveAttackRange - _context.CombatAttackInnerOffset);
        }

        private float GetPositionExitDistance(CombatActionDefinition action)
        {
            return Mathf.Max(
                GetPositionEnterDistance(action),
                action.EffectiveAttackRange + _context.CombatAttackRangeTolerance);
        }

        private float EstimateSelfPlanarRadius()
        {
            CharacterController characterController = _context.Transform.GetComponent<CharacterController>();
            if (characterController == null)
            {
                return 0f;
            }

            float scale = Mathf.Max(
                Mathf.Abs(characterController.transform.lossyScale.x),
                Mathf.Abs(characterController.transform.lossyScale.z));

            return Mathf.Max(0f, characterController.radius * scale);
        }
    }
}
