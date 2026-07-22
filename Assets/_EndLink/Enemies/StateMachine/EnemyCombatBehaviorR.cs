using EndLink.Combat;
using UnityEngine;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人远程战斗行为。R 表示 Ranged。
    /// 复用 Approach、Position、Prepare、Engage、Attack、Recover、Reposition 七阶段语义，
    /// 只负责 Combat 内部的距离控制、视线判断和动作请求。
    /// </summary>
    public sealed class EnemyCombatBehaviorR
    {
        private const float MissingHitboxRetryInterval = 1f;
        private const float DefaultPrepareDelay = 0.35f;
        private const float RepositionArriveDistance = 0.2f;
        private const float RepositionTimeout = 1.5f;

        private readonly EnemyStateContext _context;
        private float _nextAttackAttemptTime;
        private float _attackWaitElapsed;
        private float _prepareStartTime;
        private float _repositionEndTime;
        private Transform _grantedAttackTarget;
        private EnemyCombatCoordinator _lastAttackCoordinator;
        private EnemyCombatCoordinator _grantedAttackCoordinator;
        private CombatActionDefinition _selectedAction;
        private Vector3 _repositionDestination;
        private int _completedBasicAttackCount;
        private bool _hasRepositionDestination;
        private bool _isAttackPrepared;
        private bool _retreatUntilPreferredDistance;
        private bool _loggedMissingHitbox;

        public EnemyCombatBehaviorR(EnemyStateContext context)
        {
            _context = context;
        }

        /// <summary>当前 Combat 内部阶段，和基础近战行为使用同一套阶段标识。</summary>
        public EnemyCombatPhase CurrentPhase { get; private set; } = EnemyCombatPhase.Approach;

        /// <summary>进入 Combat 大状态时重置本轮远程行为。</summary>
        public void Enter()
        {
            _nextAttackAttemptTime = 0f;
            _attackWaitElapsed = 0f;
            _prepareStartTime = 0f;
            _repositionEndTime = 0f;
            _grantedAttackTarget = null;
            _lastAttackCoordinator = null;
            _grantedAttackCoordinator = null;
            _selectedAction = null;
            _repositionDestination = default;
            _hasRepositionDestination = false;
            _isAttackPrepared = false;
            _retreatUntilPreferredDistance = false;
            _loggedMissingHitbox = false;
            CurrentPhase = EnemyCombatPhase.Approach;
        }

        /// <summary>离开 Combat 大状态时停止移动并取消当前攻击尝试。</summary>
        public void Exit()
        {
            CancelCurrentAttempt();
        }

        /// <summary>取消当前远程攻击尝试并释放攻击许可。</summary>
        public void CancelCurrentAttempt()
        {
            ReleaseAttackSlot();
            _context?.Motor?.Stop();
            _context?.CombatDriver?.CancelCurrentAction();
            _attackWaitElapsed = 0f;
            _selectedAction = null;
            _hasRepositionDestination = false;
            _isAttackPrepared = false;
            _retreatUntilPreferredDistance = false;
            CurrentPhase = EnemyCombatPhase.Approach;
        }

        /// <summary>清空固定普攻/技能循环计数。</summary>
        public void ResetActionPattern()
        {
            _completedBasicAttackCount = 0;
            _selectedAction = null;
        }

        /// <summary>推进一次远程 Combat 内部决策。</summary>
        public void Tick(float deltaTime)
        {
            if (_context == null || !_context.HasValidTarget)
            {
                _context?.Motor?.Stop();
                return;
            }

            Transform target = _context.CurrentTarget;
            if (!TryGetCombatAction(out CombatActionDefinition action))
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
                case EnemyCombatPhase.Prepare:
                    TickPrepare(target, action, deltaTime);
                    break;
                case EnemyCombatPhase.Engage:
                    TickEngage(target, action, deltaTime);
                    break;
                case EnemyCombatPhase.Attack:
                    TickAttack(target, action, deltaTime);
                    break;
                case EnemyCombatPhase.Recover:
                    TickRecover(target, action, deltaTime);
                    break;
                case EnemyCombatPhase.Reposition:
                    TickReposition(target, action, deltaTime);
                    break;
                default:
                    TickApproach(target, action, deltaTime);
                    break;
            }
        }

        private void TickApproach(Transform target, CombatActionDefinition action, float deltaTime)
        {
            float distance = GetSurfaceDistance(target);
            float minimumDistance = GetMinimumDistance(action);
            float maximumDistance = GetMaximumDistance(action);

            if (distance > maximumDistance)
            {
                MoveTowardTargetSurface(target, GetPreferredDistance(action), deltaTime);
                _context.Motor?.FaceTarget(target, deltaTime);
                return;
            }

            if (distance < minimumDistance)
            {
                _retreatUntilPreferredDistance = true;
                _isAttackPrepared = false;
                CurrentPhase = EnemyCombatPhase.Engage;
                TickEngage(target, action, deltaTime);
                return;
            }

            _context.Motor?.Stop();
            _context.Motor?.FaceTarget(target, deltaTime);
            CurrentPhase = EnemyCombatPhase.Position;
        }

        private void TickPosition(Transform target, CombatActionDefinition action, float deltaTime)
        {
            float distance = GetSurfaceDistance(target);
            float tolerance = Mathf.Max(0f, _context.CombatAttackRangeTolerance);

            if (distance > GetMaximumDistance(action) + tolerance)
            {
                CurrentPhase = EnemyCombatPhase.Approach;
                TickApproach(target, action, deltaTime);
                return;
            }

            if (distance < Mathf.Max(0f, GetMinimumDistance(action) - tolerance))
            {
                _retreatUntilPreferredDistance = true;
                _isAttackPrepared = false;
                CurrentPhase = EnemyCombatPhase.Engage;
                TickEngage(target, action, deltaTime);
                return;
            }

            if (!HasLineOfSight(target, action))
            {
                BeginReposition(target, action);
                TickReposition(target, action, deltaTime);
                return;
            }

            _context.Motor?.Stop();
            _context.Motor?.FaceTarget(target, deltaTime);
            _attackWaitElapsed += Mathf.Max(0f, deltaTime);
            TryStartAttack(target, action);
        }

        private void TickPrepare(Transform target, CombatActionDefinition action, float deltaTime)
        {
            if (!HasCurrentAttackPermission(target))
            {
                ReleaseAttackSlot();
                BeginReposition(target, action);
                TickReposition(target, action, deltaTime);
                return;
            }

            _context.Motor?.Stop();
            _context.Motor?.FaceTarget(target, deltaTime);

            if (!HasLineOfSight(target, action))
            {
                ReleaseAttackSlot();
                BeginReposition(target, action);
                TickReposition(target, action, deltaTime);
                return;
            }

            float prepareDelay = _grantedAttackCoordinator != null
                ? _grantedAttackCoordinator.AttackPrepareDelay
                : DefaultPrepareDelay;
            if (Time.time < _prepareStartTime + prepareDelay)
            {
                return;
            }

            CurrentPhase = EnemyCombatPhase.Engage;
            TickEngage(target, action, deltaTime);
        }

        private void TickEngage(Transform target, CombatActionDefinition action, float deltaTime)
        {
            if (_isAttackPrepared && !HasCurrentAttackPermission(target))
            {
                ReleaseAttackSlot();
                BeginReposition(target, action);
                TickReposition(target, action, deltaTime);
                return;
            }

            float distance = GetSurfaceDistance(target);
            float minimumDistance = GetMinimumDistance(action);
            float preferredDistance = GetPreferredDistance(action);
            float maximumDistance = GetMaximumDistance(action);
            float tolerance = Mathf.Max(0f, _context.CombatAttackRangeTolerance);

            if (distance < minimumDistance)
            {
                _retreatUntilPreferredDistance = true;
            }

            if (_retreatUntilPreferredDistance)
            {
                if (distance < Mathf.Max(minimumDistance, preferredDistance - tolerance))
                {
                    MoveAwayFromTarget(target, preferredDistance - distance, deltaTime);
                    _context.Motor?.FaceTarget(target, deltaTime);
                    return;
                }

                _retreatUntilPreferredDistance = false;
            }

            if (distance > maximumDistance)
            {
                MoveTowardTargetSurface(target, preferredDistance, deltaTime);
                _context.Motor?.FaceTarget(target, deltaTime);
                return;
            }

            if (!HasLineOfSight(target, action))
            {
                ReleaseAttackSlot();
                BeginReposition(target, action);
                TickReposition(target, action, deltaTime);
                return;
            }

            if (!_isAttackPrepared)
            {
                _context.Motor?.Stop();
                _context.Motor?.FaceTarget(target, deltaTime);
                CurrentPhase = EnemyCombatPhase.Position;
                return;
            }

            _context.Motor?.Stop();
            _context.Motor?.FaceTarget(target, deltaTime);
            ICombatActionExecutor actionExecutor = _context.ActionExecutor;
            if (actionExecutor == null || !actionExecutor.CanExecute(action))
            {
                return;
            }

            if (actionExecutor.TryExecute(action, target))
            {
                NotifyAttackStarted(target);
                _attackWaitElapsed = 0f;
                _isAttackPrepared = false;
                CurrentPhase = EnemyCombatPhase.Attack;
            }
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
            CompleteActionPattern(action);
            _selectedAction = null;
            BeginReposition(target, action);
            TickReposition(target, action, deltaTime);
        }

        private void TickReposition(Transform target, CombatActionDefinition action, float deltaTime)
        {
            float distance = GetSurfaceDistance(target);
            if (distance < GetMinimumDistance(action))
            {
                _hasRepositionDestination = false;
                _retreatUntilPreferredDistance = true;
                _isAttackPrepared = false;
                CurrentPhase = EnemyCombatPhase.Engage;
                TickEngage(target, action, deltaTime);
                return;
            }

            if (!_hasRepositionDestination)
            {
                BeginReposition(target, action);
            }

            bool arrived = GetPlanarDistanceSqr(_context.Transform.position, _repositionDestination)
                <= RepositionArriveDistance * RepositionArriveDistance;
            if (!arrived && Time.time < _repositionEndTime)
            {
                _context.Motor?.MoveTo(
                    _repositionDestination,
                    RepositionArriveDistance,
                    deltaTime);
                _context.Motor?.FaceTarget(target, deltaTime);
                return;
            }

            _context.Motor?.Stop();
            _context.Motor?.FaceTarget(target, deltaTime);
            _hasRepositionDestination = false;

            if (distance > GetMaximumDistance(action) + _context.CombatAttackRangeTolerance)
            {
                CurrentPhase = EnemyCombatPhase.Approach;
                return;
            }

            CurrentPhase = HasLineOfSight(target, action)
                ? EnemyCombatPhase.Position
                : EnemyCombatPhase.Reposition;
        }

        private void TickChaseOnly(Transform target, float deltaTime)
        {
            MoveTowardTargetSurface(target, _context.CombatChaseStopDistance, deltaTime);
            _context.Motor?.FaceTarget(target, deltaTime);
        }

        private void TryStartAttack(Transform target, CombatActionDefinition action)
        {
            ICombatActionExecutor actionExecutor = _context.ActionExecutor;
            if (actionExecutor == null || action == null || Time.time < _nextAttackAttemptTime)
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

                _nextAttackAttemptTime = Time.time + MissingHitboxRetryInterval;
                return;
            }

            if (!actionExecutor.CanExecute(action) || !TryAcquireAttackPermission(target, action))
            {
                return;
            }

            _isAttackPrepared = true;
            _prepareStartTime = Time.time;
            CurrentPhase = EnemyCombatPhase.Prepare;
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
            bool granted = coordinator.RequestAttackPermission(
                _context.Transform,
                target,
                score);
            if (granted)
            {
                _grantedAttackTarget = target;
                _grantedAttackCoordinator = coordinator;
            }

            return granted;
        }

        private bool HasCurrentAttackPermission(Transform target)
        {
            return _grantedAttackCoordinator == null
                || _grantedAttackCoordinator.HasAttackPermission(_context.Transform, target);
        }

        private float CalculateAttackScore(Transform target, CombatActionDefinition action)
        {
            float distanceError = Mathf.Abs(GetSurfaceDistance(target) - GetPreferredDistance(action));
            float rangeScore = 1f / (1f + distanceError);
            float waitScore = Mathf.Clamp(_attackWaitElapsed, 0f, 5f);
            EnemyCombatCoordinator coordinator = _context.CombatCoordinator;
            float distanceWeight = coordinator != null ? coordinator.AttackScoreDistanceWeight : 1f;
            float waitWeight = coordinator != null ? coordinator.AttackScoreWaitWeight : 0f;
            return rangeScore * distanceWeight + waitScore * waitWeight;
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
                _grantedAttackCoordinator.NotifyAttackEnded(
                    _context.Transform,
                    _grantedAttackTarget);
            }

            _lastAttackCoordinator?.CancelAttacker(_context.Transform);
            _grantedAttackTarget = null;
            _lastAttackCoordinator = null;
            _grantedAttackCoordinator = null;
            _isAttackPrepared = false;
        }

        private void BeginReposition(Transform target, CombatActionDefinition action)
        {
            EnemyCombatManeuver maneuver = Random.value < 0.5f
                ? EnemyCombatManeuver.SideLeft
                : EnemyCombatManeuver.SideRight;
            float moveDistance = Mathf.Max(0.1f, _context.RangedRepositionDistance);
            _repositionDestination = EnemyCombatBehavior.CalculateManeuverDestination(
                _context.Transform.position,
                target.position,
                maneuver,
                moveDistance,
                moveDistance,
                GetMinimumDistance(action),
                GetMaximumDistance(action));
            _repositionEndTime = Time.time + RepositionTimeout;
            _hasRepositionDestination = true;
            _retreatUntilPreferredDistance = false;
            CurrentPhase = EnemyCombatPhase.Reposition;
        }

        private void MoveTowardTargetSurface(Transform target, float stopDistance, float deltaTime)
        {
            Vector3 approachPoint = CombatTargetUtility.GetClosestPoint(
                target,
                _context.Transform.position);
            _context.Motor?.MoveTo(approachPoint, Mathf.Max(0f, stopDistance), deltaTime);
        }

        private void MoveAwayFromTarget(Transform target, float requiredDistance, float deltaTime)
        {
            Vector3 away = _context.Transform.position - target.position;
            away.y = 0f;
            if (away.sqrMagnitude <= 0.0001f)
            {
                away = -target.forward;
                away.y = 0f;
            }

            if (away.sqrMagnitude <= 0.0001f)
            {
                away = Vector3.back;
            }

            float moveDistance = Mathf.Max(0.5f, requiredDistance + 0.25f);
            Vector3 destination = _context.Transform.position + away.normalized * moveDistance;
            _context.Motor?.MoveTo(destination, 0.05f, deltaTime);
        }

        private bool HasLineOfSight(Transform target, CombatActionDefinition action)
        {
            if (!_context.RangedRequireLineOfSight
                || _context.RangedObstructionLayers.value == 0
                || target == null)
            {
                return true;
            }

            Transform lockPoint = target;
            if (CombatTargetUtility.TryResolve(target, out ICombatTarget combatTarget)
                && combatTarget.LockPoint != null)
            {
                lockPoint = combatTarget.LockPoint;
            }

            Vector3 targetPosition = lockPoint.position;
            Vector3 planarForward = targetPosition - _context.Transform.position;
            planarForward.y = 0f;
            planarForward = planarForward.sqrMagnitude > 0.0001f
                ? planarForward.normalized
                : _context.Transform.forward;
            Vector3 origin = _context.Transform.position
                + Vector3.up * action.HitboxSpawnHeight
                + planarForward * action.HitboxSpawnDistance;

            return !Physics.Linecast(
                origin,
                targetPosition,
                _context.RangedObstructionLayers,
                QueryTriggerInteraction.Ignore);
        }

        private bool TryGetCombatAction(out CombatActionDefinition action)
        {
            if (_selectedAction == null && _context.CombatDriver != null)
            {
                int basicAttacksBeforeSkill = _context.CombatBasicAttacksBeforeSkill;
                bool preferSkill = basicAttacksBeforeSkill > 0
                    && _completedBasicAttackCount >= basicAttacksBeforeSkill;
                _selectedAction = _context.CombatDriver.SelectCombatAction(
                    _context.CombatSkillChance,
                    preferSkill);
            }

            action = _selectedAction;
            return action != null;
        }

        private void CompleteActionPattern(CombatActionDefinition completedAction)
        {
            if (completedAction == null)
            {
                return;
            }

            switch (completedAction.ActionType)
            {
                case CombatActionType.BasicAttack:
                    int requiredBasicAttacks = _context.CombatBasicAttacksBeforeSkill;
                    _completedBasicAttackCount = requiredBasicAttacks > 0
                        ? Mathf.Min(_completedBasicAttackCount + 1, requiredBasicAttacks)
                        : 0;
                    break;
                case CombatActionType.Skill:
                    _completedBasicAttackCount = 0;
                    break;
            }
        }

        private float GetSurfaceDistance(Transform target)
        {
            return CombatTargetUtility.GetSurfaceDistance(target, _context.Transform.position);
        }

        private float GetMinimumDistance(CombatActionDefinition action)
        {
            return Mathf.Clamp(
                _context.RangedMinimumDistance,
                0f,
                GetMaximumDistance(action));
        }

        private float GetPreferredDistance(CombatActionDefinition action)
        {
            return Mathf.Clamp(
                _context.RangedPreferredDistance,
                GetMinimumDistance(action),
                GetMaximumDistance(action));
        }

        private static float GetMaximumDistance(CombatActionDefinition action)
        {
            return action != null
                ? Mathf.Max(0.01f, action.EffectiveAttackRange)
                : CombatActionDefinition.DefaultEffectiveAttackRange;
        }

        private static float GetPlanarDistanceSqr(Vector3 first, Vector3 second)
        {
            Vector3 offset = first - second;
            offset.y = 0f;
            return offset.sqrMagnitude;
        }
    }
}
