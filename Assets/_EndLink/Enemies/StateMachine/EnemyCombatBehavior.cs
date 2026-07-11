using EndLink.Combat;
using UnityEngine;

namespace EndLink.Enemies
{
    /// <summary>敌人在观察或攻击准备阶段执行的一次短距离机动。</summary>
    public enum EnemyCombatManeuver
    {
        SideLeft = 0,
        SideRight = 1,
        Retreat = 2
    }

    /// <summary>
    /// 敌人 Combat 大状态内部的基础战斗阶段。
    /// 当前只描述单个敌人面对单个目标时的接近、定位、攻击和恢复流程。
    /// </summary>
    public enum EnemyCombatPhase
    {
        Approach = 0,
        Position = 1,
        Prepare = 2,
        Engage = 3,
        Attack = 4,
        Recover = 5,
        Reposition = 6
    }

    /// <summary>
    /// 敌人单人战斗行为状态机。
    /// 只决定 Combat 内部何时移动、停位和请求普攻或技能；
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
        private Vector3 _softPosition;
        private Vector3 _softPositionTargetAnchor;
        private float _nextSoftPositionRefreshTime;
        private Vector3 _observationDestination;
        private float _nextObservationMoveTime;
        private bool _isObservationMoving;
        private Vector3 _prepareDestination;
        private float _prepareStartTime;
        private float _prepareMoveEndTime;
        private bool _isPrepareManeuverStarted;
        private bool _hasSoftPosition;
        private bool _loggedMissingHitbox;
        private CombatActionDefinition _selectedAction;
        private int _completedBasicAttackCount;

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
            _softPosition = default;
            _softPositionTargetAnchor = default;
            _nextSoftPositionRefreshTime = 0f;
            _observationDestination = default;
            _nextObservationMoveTime = 0f;
            _isObservationMoving = false;
            _prepareDestination = default;
            _prepareStartTime = 0f;
            _prepareMoveEndTime = 0f;
            _isPrepareManeuverStarted = false;
            _hasSoftPosition = false;
            _loggedMissingHitbox = false;
            _selectedAction = null;
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
            _hasSoftPosition = false;
            _isObservationMoving = false;
            _isPrepareManeuverStarted = false;
            _selectedAction = null;
            CurrentPhase = EnemyCombatPhase.Approach;
        }

        /// <summary>清空固定普攻/技能循环计数，用于脱战、死亡和完整运行时重置。</summary>
        public void ResetActionPattern()
        {
            _completedBasicAttackCount = 0;
            _selectedAction = null;
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

                case EnemyCombatPhase.Attack:
                    TickAttack(target, action, deltaTime);
                    break;

                case EnemyCombatPhase.Prepare:
                    TickPrepare(target, action, deltaTime);
                    break;

                case EnemyCombatPhase.Engage:
                    TickEngage(target, action, deltaTime);
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
            if (UsesSoftPositioning() && TickSoftPositionMovement(target, deltaTime))
            {
                return;
            }

            TickDirectApproach(target, action, deltaTime);
        }

        private void TickDirectApproach(Transform target, CombatActionDefinition action, float deltaTime)
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
            if (UsesSoftPositioning())
            {
                TickSoftPosition(target, action, deltaTime);
                return;
            }

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

        private void TickSoftPosition(Transform target, CombatActionDefinition action, float deltaTime)
        {
            EnemyCombatCoordinator coordinator = _context.CombatCoordinator;
            float targetMoveDistance = Mathf.Sqrt(GetPlanarDistanceSqr(
                target.position,
                _softPositionTargetAnchor));
            bool crowded = coordinator != null
                && coordinator.IsSoftPositionCrowded(_context.Transform, target);
            if (!_hasSoftPosition
                || ShouldRefreshSoftPosition(
                    targetMoveDistance,
                    coordinator != null ? coordinator.SoftPositionTargetRefreshDistance : 0.8f,
                    _nextSoftPositionRefreshTime,
                    Time.time,
                    crowded))
            {
                _hasSoftPosition = false;
                CurrentPhase = EnemyCombatPhase.Reposition;
                TickReposition(target, action, deltaTime);
                return;
            }

            _attackWaitElapsed += Mathf.Max(0f, deltaTime);
            if (TryBeginPrepare(target, action))
            {
                return;
            }

            TickObservationMovement(target, deltaTime);
        }

        private void TickPrepare(Transform target, CombatActionDefinition action, float deltaTime)
        {
            EnemyCombatCoordinator coordinator = _grantedAttackCoordinator;
            if (coordinator == null || !coordinator.HasAttackPermission(_context.Transform, target))
            {
                ReleaseAttackSlot();
                _hasSoftPosition = false;
                CurrentPhase = EnemyCombatPhase.Reposition;
                TickReposition(target, action, deltaTime);
                return;
            }

            _context.Motor?.FaceTarget(target, deltaTime);
            if (Time.time < _prepareStartTime + coordinator.AttackPrepareDelay)
            {
                _context.Motor?.Stop();
                return;
            }

            if (!_isPrepareManeuverStarted)
            {
                EnemyCombatManeuver maneuver = SelectManeuver(
                    Random.value,
                    coordinator.ManeuverRetreatChance);
                _prepareDestination = ResolveManeuverDestination(
                    target,
                    coordinator,
                    maneuver,
                    coordinator.AttackPrepareMoveDistance);
                _prepareMoveEndTime = Time.time + coordinator.AttackPrepareMoveDuration;
                _isPrepareManeuverStarted = true;
            }

            float arriveDistance = coordinator.SoftPositionArriveDistance;
            bool arrived = GetPlanarDistanceSqr(_context.Transform.position, _prepareDestination)
                <= arriveDistance * arriveDistance;
            if (!arrived && Time.time < _prepareMoveEndTime)
            {
                _context.Motor?.MoveTo(
                    _prepareDestination,
                    arriveDistance,
                    deltaTime,
                    coordinator.AttackPrepareSpeedMultiplier);
                _context.Motor?.FaceTarget(target, deltaTime);
                return;
            }

            CurrentPhase = EnemyCombatPhase.Engage;
            TickEngage(target, action, deltaTime);
        }

        private void TickEngage(Transform target, CombatActionDefinition action, float deltaTime)
        {
            EnemyCombatCoordinator coordinator = _grantedAttackCoordinator;
            if (coordinator != null && !coordinator.HasAttackPermission(_context.Transform, target))
            {
                ReleaseAttackSlot();
                _hasSoftPosition = false;
                CurrentPhase = EnemyCombatPhase.Reposition;
                TickReposition(target, action, deltaTime);
                return;
            }

            if (GetSurfaceDistance(target) > GetPositionEnterDistance(action))
            {
                MoveTowardTargetSurface(target, GetPositionEnterDistance(action), deltaTime);
                _context.Motor?.FaceTarget(target, deltaTime);
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
            if (UsesSoftPositioning())
            {
                _hasSoftPosition = false;
                CurrentPhase = EnemyCombatPhase.Reposition;
                TickReposition(target, action, deltaTime);
                return;
            }

            CurrentPhase = GetSurfaceDistance(target) > GetPositionExitDistance(action)
                ? EnemyCombatPhase.Approach
                : EnemyCombatPhase.Position;
        }

        private void TickReposition(Transform target, CombatActionDefinition action, float deltaTime)
        {
            if (!UsesSoftPositioning() || !TickSoftPositionMovement(target, deltaTime))
            {
                CurrentPhase = EnemyCombatPhase.Approach;
                TickDirectApproach(target, action, deltaTime);
            }
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

            if (_grantedAttackCoordinator != null)
            {
                BeginAttackPreparation();
                return;
            }

            if (actionExecutor.TryExecute(action, target))
            {
                NotifyAttackStarted(target);
                _attackWaitElapsed = 0f;
                CurrentPhase = EnemyCombatPhase.Attack;
            }
        }

        private bool TryBeginPrepare(Transform target, CombatActionDefinition action)
        {
            ICombatActionExecutor actionExecutor = _context.ActionExecutor;
            if (actionExecutor == null || action == null)
            {
                return false;
            }

            float currentTime = Time.time;
            if (currentTime < _nextAttackAttemptTime)
            {
                return false;
            }

            if (action.HitboxPrefab == null)
            {
                if (!_loggedMissingHitbox)
                {
                    actionExecutor.TryExecute(action, target);
                    _loggedMissingHitbox = true;
                }

                _nextAttackAttemptTime = currentTime + MissingHitboxRetryInterval;
                return false;
            }

            if (!actionExecutor.CanExecute(action) || !TryAcquireAttackPermission(target, action))
            {
                return false;
            }

            BeginAttackPreparation();
            return true;
        }

        private void BeginAttackPreparation()
        {
            _context.Motor?.Stop();
            _isObservationMoving = false;
            _isPrepareManeuverStarted = false;
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

        private float CalculateAttackScore(Transform target, CombatActionDefinition action)
        {
            float surfaceDistance = GetSurfaceDistance(target);
            float distanceOutsideRange = Mathf.Max(0f, surfaceDistance - action.EffectiveAttackRange);
            float rangeCloseness = 1f / (1f + distanceOutsideRange);
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

        private bool TickSoftPositionMovement(Transform target, float deltaTime)
        {
            EnemyCombatCoordinator coordinator = _context.CombatCoordinator;
            if (coordinator == null)
            {
                return false;
            }

            if (!_hasSoftPosition)
            {
                if (!coordinator.TryGetSoftPosition(
                        _context.Transform,
                        target,
                        _context.Transform.position,
                        true,
                        out _softPosition))
                {
                    return false;
                }

                _softPositionTargetAnchor = target.position;
                _hasSoftPosition = true;
            }

            float arriveDistance = coordinator.SoftPositionArriveDistance;
            if (GetPlanarDistanceSqr(_context.Transform.position, _softPosition)
                <= arriveDistance * arriveDistance)
            {
                _context.Motor?.Stop();
                _context.Motor?.FaceTarget(target, deltaTime);
                ScheduleNextSoftPositionRefresh(coordinator);
                ScheduleNextObservationMove(coordinator);
                CurrentPhase = EnemyCombatPhase.Position;
                return true;
            }

            _context.Motor?.MoveTo(_softPosition, arriveDistance, deltaTime);
            _context.Motor?.FaceTarget(target, deltaTime);
            return true;
        }

        private void ScheduleNextSoftPositionRefresh(EnemyCombatCoordinator coordinator)
        {
            float minimum = coordinator != null ? coordinator.SoftRepositionIntervalMin : 3f;
            float maximum = coordinator != null ? coordinator.SoftRepositionIntervalMax : 5f;
            _nextSoftPositionRefreshTime = Time.time + Random.Range(minimum, maximum);
        }

        private void TickObservationMovement(Transform target, float deltaTime)
        {
            EnemyCombatCoordinator coordinator = _context.CombatCoordinator;
            if (coordinator == null)
            {
                _context.Motor?.Stop();
                _context.Motor?.FaceTarget(target, deltaTime);
                return;
            }

            float arriveDistance = coordinator.SoftPositionArriveDistance;
            if (_isObservationMoving)
            {
                bool arrived = GetPlanarDistanceSqr(_context.Transform.position, _observationDestination)
                    <= arriveDistance * arriveDistance;
                if (!arrived)
                {
                    _context.Motor?.MoveTo(
                        _observationDestination,
                        arriveDistance,
                        deltaTime,
                        coordinator.ObservationMoveSpeedMultiplier);
                    _context.Motor?.FaceTarget(target, deltaTime);
                    return;
                }

                _isObservationMoving = false;
                ScheduleNextObservationMove(coordinator);
            }

            _context.Motor?.Stop();
            _context.Motor?.FaceTarget(target, deltaTime);
            if (Time.time < _nextObservationMoveTime)
            {
                return;
            }

            EnemyCombatManeuver maneuver = SelectManeuver(
                Random.value,
                coordinator.ManeuverRetreatChance);
            _observationDestination = ResolveManeuverDestination(
                target,
                coordinator,
                maneuver,
                coordinator.ObservationMoveDistance);
            coordinator.UpdateSoftPosition(
                _context.Transform,
                target,
                _observationDestination);
            _isObservationMoving = true;
        }

        private void ScheduleNextObservationMove(EnemyCombatCoordinator coordinator)
        {
            float minimum = coordinator != null ? coordinator.ObservationPauseIntervalMin : 0.6f;
            float maximum = coordinator != null ? coordinator.ObservationPauseIntervalMax : 1.4f;
            _nextObservationMoveTime = Time.time + Random.Range(minimum, maximum);
        }

        private Vector3 ResolveManeuverDestination(
            Transform target,
            EnemyCombatCoordinator coordinator,
            EnemyCombatManeuver maneuver,
            float distance)
        {
            float currentRadius = Mathf.Sqrt(GetPlanarDistanceSqr(
                _context.Transform.position,
                target.position));
            float minimumRadius = coordinator.SoftPositioningEnabled
                ? coordinator.SoftPositionMinDistance
                : Mathf.Max(0f, currentRadius - distance);
            float maximumRadius = coordinator.SoftPositioningEnabled
                ? coordinator.SoftPositionMaxDistance
                : currentRadius + distance;
            Vector3 destination = CalculateManeuverDestination(
                _context.Transform.position,
                target.position,
                maneuver,
                distance,
                distance,
                minimumRadius,
                maximumRadius);

            if (maneuver == EnemyCombatManeuver.Retreat
                && GetPlanarDistanceSqr(_context.Transform.position, destination) <= 0.01f)
            {
                EnemyCombatManeuver side = Random.value < 0.5f
                    ? EnemyCombatManeuver.SideLeft
                    : EnemyCombatManeuver.SideRight;
                destination = CalculateManeuverDestination(
                    _context.Transform.position,
                    target.position,
                    side,
                    distance,
                    distance,
                    minimumRadius,
                    maximumRadius);
            }

            return destination;
        }

        private bool UsesSoftPositioning()
        {
            return _context.CombatCoordinator != null
                && _context.CombatCoordinator.SoftPositioningEnabled;
        }

        /// <summary>判断克制型等待敌人是否需要刷新软站位。</summary>
        public static bool ShouldRefreshSoftPosition(
            float targetMoveDistance,
            float targetRefreshDistance,
            float nextRefreshTime,
            float currentTime,
            bool crowded)
        {
            if (targetMoveDistance >= Mathf.Max(0.01f, targetRefreshDistance)
                || currentTime >= nextRefreshTime)
            {
                return true;
            }

            return crowded && currentTime >= nextRefreshTime - 1f;
        }

        /// <summary>按后撤概率选择一次机动，其余概率平均分配给左右侧移。</summary>
        public static EnemyCombatManeuver SelectManeuver(float randomValue, float retreatChance)
        {
            float value = Mathf.Clamp01(randomValue);
            float retreatThreshold = Mathf.Clamp01(retreatChance);
            if (value < retreatThreshold)
            {
                return EnemyCombatManeuver.Retreat;
            }

            float sideMidpoint = retreatThreshold + (1f - retreatThreshold) * 0.5f;
            return value < sideMidpoint
                ? EnemyCombatManeuver.SideLeft
                : EnemyCombatManeuver.SideRight;
        }

        /// <summary>
        /// 计算目标相对的侧移或后撤终点，并把结果约束在围攻软站位的内外距离之间。
        /// </summary>
        public static Vector3 CalculateManeuverDestination(
            Vector3 enemyPosition,
            Vector3 targetPosition,
            EnemyCombatManeuver maneuver,
            float sideDistance,
            float retreatDistance,
            float minimumRadius,
            float maximumRadius)
        {
            Vector3 radial = enemyPosition - targetPosition;
            radial.y = 0f;
            if (radial.sqrMagnitude <= 0.0001f)
            {
                radial = Vector3.forward;
            }

            radial.Normalize();
            Vector3 direction = maneuver switch
            {
                EnemyCombatManeuver.SideLeft => new Vector3(-radial.z, 0f, radial.x),
                EnemyCombatManeuver.SideRight => new Vector3(radial.z, 0f, -radial.x),
                _ => radial
            };
            float distance = maneuver == EnemyCombatManeuver.Retreat
                ? Mathf.Max(0f, retreatDistance)
                : Mathf.Max(0f, sideDistance);
            Vector3 destination = enemyPosition + direction * distance;

            Vector3 targetOffset = destination - targetPosition;
            targetOffset.y = 0f;
            float safeMinimum = Mathf.Max(0f, minimumRadius);
            float safeMaximum = Mathf.Max(safeMinimum, maximumRadius);
            float radius = Mathf.Clamp(targetOffset.magnitude, safeMinimum, safeMaximum);
            if (targetOffset.sqrMagnitude <= 0.0001f)
            {
                targetOffset = radial;
            }

            targetOffset.Normalize();
            destination = targetPosition + targetOffset * radius;
            destination.y = enemyPosition.y;
            return destination;
        }

        private static float GetPlanarDistanceSqr(Vector3 first, Vector3 second)
        {
            Vector3 offset = first - second;
            offset.y = 0f;
            return offset.sqrMagnitude;
        }

        private void MoveTowardTargetSurface(Transform target, float stopDistance, float deltaTime)
        {
            Vector3 approachPoint = CombatTargetUtility.GetClosestPoint(
                target,
                _context.Transform.position);

            _context.Motor?.MoveTo(approachPoint, stopDistance, deltaTime);
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
