using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人攻击候选数据。
    /// Score 越高越优先获得攻击许可；Sequence 用于同分时保持稳定先后顺序。
    /// </summary>
    public readonly struct EnemyAttackCandidate
    {
        public EnemyAttackCandidate(int attackerId, float score, int sequence)
        {
            AttackerId = attackerId;
            Score = score;
            Sequence = sequence;
        }

        /// <summary>申请攻击许可的敌人实例 ID。</summary>
        public int AttackerId { get; }

        /// <summary>本帧攻击评分，越高越应该优先出手。</summary>
        public float Score { get; }

        /// <summary>候选进入队列的顺序，用于同分时稳定排序。</summary>
        public int Sequence { get; }
    }

    /// <summary>
    /// 敌人战斗协调器。
    /// 挂在战斗区域的 EnemyCoordinator 物体上，通过半径扫描自动接管范围内敌人，
    /// 并统一管理这些敌人针对同一目标时的出手许可和围攻节奏。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyCombatCoordinator : MonoBehaviour
    {
        private const int CandidateFrameGrace = 2;
        private const int DefaultEnemyBufferSize = 64;
        private const int SoftPositionSampleCount = 12;
        private const string EnemyLayerName = "Enemy";

        [Header("区域扫描")]
        [Tooltip("协调器影响敌人的半径。半径内的敌人会自动绑定到该协调器。")]
        [SerializeField, Min(0.1f)]
        private float coordinationRadius = 18f;

        [Tooltip("扫描敌人使用的 LayerMask。通常设置为 Enemy。")]
        [SerializeField]
        private LayerMask enemyLayerMask;

        [Tooltip("自动扫描敌人的时间间隔。不要每帧扫描，0.3-0.5 秒通常足够。")]
        [SerializeField, Min(0.05f)]
        private float scanInterval = 0.5f;

        [Tooltip("多个协调器半径重叠时使用的优先级。优先级更高者优先接管非战斗中的敌人；同优先级比较距离。")]
        [SerializeField]
        private int priority;

        [Header("围攻节奏")]
        [Tooltip("是否启用攻击协调。关闭后，绑定到该区域的敌人仍会自动归属，但不会限制同时出手数量。")]
        [SerializeField]
        private bool attackCoordinationEnabled = true;

        [Tooltip("同一目标在同一时间最多允许多少个敌人进入攻击动作。当前第一版建议 1-2。")]
        [SerializeField, Min(1)]
        private int maxSimultaneousAttackers = 1;

        [Tooltip("同一目标两次授予攻击许可之间的最短间隔。用于控制多敌人轮流出手的整体攻击频率。")]
        [SerializeField, Min(0f)]
        private float attackGrantInterval = 0.35f;

        [Tooltip("攻击评分中的距离权重。越高表示越偏向让离目标更近、已经站好位的敌人先出手。")]
        [SerializeField, Min(0f)]
        private float attackScoreDistanceWeight = 1f;

        [Tooltip("攻击评分中的等待时间权重。越高表示等待越久的敌人越容易获得下一次出手机会。")]
        [SerializeField, Min(0f)]
        private float attackScoreWaitWeight = 0.45f;

        [Tooltip("攻击许可被授予后最多保留多久。敌人需要在这段时间内接近并开始攻击，否则许可自动释放。")]
        [SerializeField, Min(0.1f)]
        private float attackReservationDuration = 4f;

        [Header("围攻软站位")]
        [Tooltip("是否为等待攻击许可的敌人分配克制型软站位。关闭后保持原有贴近目标等待的行为。")]
        [SerializeField]
        private bool softPositioningEnabled = true;

        [Tooltip("等待站位距离目标根节点的最小水平距离。")]
        [SerializeField, Min(0.1f)]
        private float softPositionMinDistance = 2.2f;

        [Tooltip("等待站位距离目标根节点的最大水平距离。")]
        [SerializeField, Min(0.1f)]
        private float softPositionMaxDistance = 3.4f;

        [Tooltip("不同等待敌人的软站位之间希望保持的最小水平间距。")]
        [SerializeField, Min(0f)]
        private float softPositionMinSpacing = 1.4f;

        [Tooltip("敌人到达软站位后，下一次主动重新定位的随机时间范围。")]
        [SerializeField]
        private Vector2 softRepositionInterval = new(3f, 5f);

        [Tooltip("目标相对上次分配站位时移动超过该距离后，敌人才刷新软站位。")]
        [SerializeField, Min(0.1f)]
        private float softPositionTargetRefreshDistance = 0.8f;

        [Tooltip("敌人距离软站位小于该值时视为到位。")]
        [SerializeField, Min(0.01f)]
        private float softPositionArriveDistance = 0.25f;

        [Header("观察与攻击准备")]
        [Tooltip("等待攻击许可时，两次观察移动之间的随机停顿时间范围。")]
        [SerializeField]
        private Vector2 observationPauseInterval = new(0.6f, 1.4f);

        [Tooltip("一次观察侧移或后撤的水平距离。")]
        [SerializeField, Min(0.1f)]
        private float observationMoveDistance = 0.65f;

        [Tooltip("观察移动相对敌人基础移动速度的倍率。")]
        [SerializeField, Range(0.05f, 1f)]
        private float observationMoveSpeedMultiplier = 0.4f;

        [Tooltip("获得攻击许可后，开始攻击准备机动前的固定观察时间。")]
        [SerializeField, Min(0f)]
        private float attackPrepareDelay = 0.5f;

        [Tooltip("攻击准备阶段执行一次侧移或后撤的水平距离。")]
        [SerializeField, Min(0.1f)]
        private float attackPrepareMoveDistance = 0.8f;

        [Tooltip("攻击准备机动允许持续的最长时间，结束后进入 Engage。")]
        [SerializeField, Min(0.05f)]
        private float attackPrepareMoveDuration = 0.55f;

        [Tooltip("攻击准备机动相对敌人基础移动速度的倍率。")]
        [SerializeField, Range(0.05f, 1f)]
        private float attackPrepareSpeedMultiplier = 0.65f;

        [Tooltip("随机机动选择后撤的概率。剩余概率平均分给向左和向右侧移。")]
        [SerializeField, Range(0f, 1f)]
        private float maneuverRetreatChance = 0.3f;

        [Header("调试")]
        [Tooltip("是否始终绘制协调器半径 Gizmo。")]
        [SerializeField]
        private bool drawGizmo = true;

        [Tooltip("运行时是否绘制目标周围的软站位和等待距离。")]
        [SerializeField]
        private bool drawSoftPositionGizmos = true;

        private readonly Dictionary<int, TargetAttackState> _targetStates = new();
        private readonly HashSet<EnemyStateMachine> _assignedEnemies = new();
        private readonly List<EnemyStateMachine> _seenEnemies = new();
        private readonly List<EnemyStateMachine> _releaseBuffer = new();
        private readonly Collider[] _enemyBuffer = new Collider[DefaultEnemyBufferSize];
        private int _nextSequence;
        private float _nextScanTime;

        /// <summary>是否启用该区域的攻击协调。</summary>
        public bool AttackCoordinationEnabled => attackCoordinationEnabled;

        /// <summary>同一目标最多允许多少个敌人同时攻击。</summary>
        public int MaxSimultaneousAttackers => Mathf.Max(1, maxSimultaneousAttackers);

        /// <summary>攻击评分中的距离权重。</summary>
        public float AttackScoreDistanceWeight => attackScoreDistanceWeight;

        /// <summary>攻击评分中的等待时间权重。</summary>
        public float AttackScoreWaitWeight => attackScoreWaitWeight;

        /// <summary>是否启用等待敌人的软站位分配。</summary>
        public bool SoftPositioningEnabled => softPositioningEnabled;

        /// <summary>目标移动多远后刷新软站位。</summary>
        public float SoftPositionTargetRefreshDistance => softPositionTargetRefreshDistance;

        /// <summary>抵达软站位使用的水平停止距离。</summary>
        public float SoftPositionArriveDistance => softPositionArriveDistance;

        /// <summary>克制型敌人两次主动换位之间的最短时间。</summary>
        public float SoftRepositionIntervalMin => softRepositionInterval.x;

        /// <summary>克制型敌人两次主动换位之间的最长时间。</summary>
        public float SoftRepositionIntervalMax => softRepositionInterval.y;

        /// <summary>两次观察移动之间的最短停顿时间。</summary>
        public float ObservationPauseIntervalMin => observationPauseInterval.x;

        /// <summary>两次观察移动之间的最长停顿时间。</summary>
        public float ObservationPauseIntervalMax => observationPauseInterval.y;

        /// <summary>一次观察移动的距离。</summary>
        public float ObservationMoveDistance => observationMoveDistance;

        /// <summary>观察移动的局部速度倍率。</summary>
        public float ObservationMoveSpeedMultiplier => observationMoveSpeedMultiplier;

        /// <summary>获得攻击许可后的固定准备停顿时间。</summary>
        public float AttackPrepareDelay => attackPrepareDelay;

        /// <summary>攻击准备阶段单次机动距离。</summary>
        public float AttackPrepareMoveDistance => attackPrepareMoveDistance;

        /// <summary>攻击准备阶段单次机动的最长持续时间。</summary>
        public float AttackPrepareMoveDuration => attackPrepareMoveDuration;

        /// <summary>攻击准备阶段的局部移动速度倍率。</summary>
        public float AttackPrepareSpeedMultiplier => attackPrepareSpeedMultiplier;

        /// <summary>观察和攻击准备动作选择后撤的概率。</summary>
        public float ManeuverRetreatChance => maneuverRetreatChance;

        /// <summary>等待软站位允许的最小目标距离。</summary>
        public float SoftPositionMinDistance => softPositionMinDistance;

        /// <summary>等待软站位允许的最大目标距离。</summary>
        public float SoftPositionMaxDistance => softPositionMaxDistance;

        /// <summary>协调器接管优先级。</summary>
        public int Priority => priority;

        private void Reset()
        {
            enemyLayerMask = GetDefaultEnemyLayerMask();
        }

        private void Awake()
        {
            EnsureDefaults();
        }

        private void OnEnable()
        {
            _nextScanTime = 0f;
            ScanEnemies();
        }

        private void Update()
        {
            if (Time.time < _nextScanTime)
            {
                return;
            }

            ScanEnemies();
        }

        private void OnDisable()
        {
            ReleaseAllAssignedEnemies();
            _targetStates.Clear();
        }

        private void OnValidate()
        {
            coordinationRadius = Mathf.Max(0.1f, coordinationRadius);
            scanInterval = Mathf.Max(0.05f, scanInterval);
            maxSimultaneousAttackers = Mathf.Max(1, maxSimultaneousAttackers);
            attackGrantInterval = Mathf.Max(0f, attackGrantInterval);
            attackScoreDistanceWeight = Mathf.Max(0f, attackScoreDistanceWeight);
            attackScoreWaitWeight = Mathf.Max(0f, attackScoreWaitWeight);
            attackReservationDuration = Mathf.Max(0.1f, attackReservationDuration);
            softPositionMinDistance = Mathf.Max(0.1f, softPositionMinDistance);
            softPositionMaxDistance = Mathf.Max(softPositionMinDistance, softPositionMaxDistance);
            softPositionMinSpacing = Mathf.Max(0f, softPositionMinSpacing);
            softRepositionInterval.x = Mathf.Max(0.1f, softRepositionInterval.x);
            softRepositionInterval.y = Mathf.Max(softRepositionInterval.x, softRepositionInterval.y);
            softPositionTargetRefreshDistance = Mathf.Max(0.1f, softPositionTargetRefreshDistance);
            softPositionArriveDistance = Mathf.Max(0.01f, softPositionArriveDistance);
            observationPauseInterval.x = Mathf.Max(0.05f, observationPauseInterval.x);
            observationPauseInterval.y = Mathf.Max(observationPauseInterval.x, observationPauseInterval.y);
            observationMoveDistance = Mathf.Max(0.1f, observationMoveDistance);
            observationMoveSpeedMultiplier = Mathf.Clamp(observationMoveSpeedMultiplier, 0.05f, 1f);
            attackPrepareDelay = Mathf.Max(0f, attackPrepareDelay);
            attackPrepareMoveDistance = Mathf.Max(0.1f, attackPrepareMoveDistance);
            attackPrepareMoveDuration = Mathf.Max(0.05f, attackPrepareMoveDuration);
            attackPrepareSpeedMultiplier = Mathf.Clamp(attackPrepareSpeedMultiplier, 0.05f, 1f);
            maneuverRetreatChance = Mathf.Clamp01(maneuverRetreatChance);
            EnsureDefaults();
        }

        /// <summary>
        /// 申请针对某个目标的攻击许可。
        /// 为避免 Update 顺序导致先调用者永远占优，本帧提交的分数会在下一帧参与竞争。
        /// </summary>
        public bool RequestAttackPermission(Transform attacker, Transform target, float score)
        {
            if (!attackCoordinationEnabled)
            {
                return true;
            }

            if (attacker == null || target == null)
            {
                return false;
            }

            int currentFrame = Time.frameCount;
            TargetAttackState targetState = GetOrCreateTargetState(target);
            CleanupStaleCandidates(targetState, currentFrame);
            CleanupExpiredReservations(targetState, Time.time);

            int attackerId = attacker.GetInstanceID();
            if (targetState.ActiveAttackers.Contains(attackerId)
                || targetState.ReservedAttackers.ContainsKey(attackerId))
            {
                return true;
            }

            if (Time.time < targetState.NextGrantTime)
            {
                UpsertCandidate(targetState, attackerId, score, currentFrame);
                return false;
            }

            if (!HasAttackCapacity(
                    targetState.ActiveAttackers.Count,
                    targetState.ReservedAttackers.Count,
                    MaxSimultaneousAttackers))
            {
                UpsertCandidate(targetState, attackerId, score, currentFrame);
                return false;
            }

            int openSlotCount = MaxSimultaneousAttackers
                - targetState.ActiveAttackers.Count
                - targetState.ReservedAttackers.Count;
            FillPreviousFrameCandidates(targetState, currentFrame);
            SelectGrantedAttackers(targetState.CandidateBuffer, openSlotCount, targetState.GrantedBuffer);
            bool granted = targetState.GrantedBuffer.Contains(attackerId);

            if (!granted)
            {
                UpsertCandidate(targetState, attackerId, score, currentFrame);
            }
            else
            {
                targetState.ReservedAttackers[attackerId] = Time.time + attackReservationDuration;
                targetState.Candidates.Remove(attackerId);
                targetState.SoftPositions.Remove(attackerId);
                targetState.NextGrantTime = Time.time + attackGrantInterval;
            }

            return granted;
        }

        /// <summary>
        /// 标记敌人已经真正启动攻击。
        /// 只有动作成功执行后才占用攻击槽，避免配置缺失时长期卡住名额。
        /// </summary>
        public void NotifyAttackStarted(Transform attacker, Transform target)
        {
            if (!attackCoordinationEnabled || attacker == null || target == null)
            {
                return;
            }

            TargetAttackState targetState = GetOrCreateTargetState(target);
            int attackerId = attacker.GetInstanceID();
            targetState.ReservedAttackers.Remove(attackerId);
            targetState.ActiveAttackers.Add(attackerId);
            targetState.Candidates.Remove(attackerId);
        }

        /// <summary>释放敌人当前占用的攻击槽。</summary>
        public void NotifyAttackEnded(Transform attacker, Transform target)
        {
            if (!attackCoordinationEnabled || attacker == null || target == null)
            {
                return;
            }

            if (!TryGetTargetState(target, out TargetAttackState targetState))
            {
                return;
            }

            int attackerId = attacker.GetInstanceID();
            targetState.ActiveAttackers.Remove(attackerId);
            targetState.ReservedAttackers.Remove(attackerId);
            targetState.Candidates.Remove(attackerId);
        }

        /// <summary>检查指定敌人是否仍持有当前目标的攻击许可。</summary>
        public bool HasAttackPermission(Transform attacker, Transform target)
        {
            if (!attackCoordinationEnabled)
            {
                return true;
            }

            if (attacker == null || target == null || !TryGetTargetState(target, out TargetAttackState targetState))
            {
                return false;
            }

            CleanupExpiredReservations(targetState, Time.time);
            int attackerId = attacker.GetInstanceID();
            return targetState.ActiveAttackers.Contains(attackerId)
                || targetState.ReservedAttackers.ContainsKey(attackerId);
        }

        /// <summary>
        /// 为等待中的敌人分配一个目标周围的软站位。
        /// 站位只是一项建议，具体移动仍由敌人自己的 CombatBehavior 和 Motor 执行。
        /// </summary>
        public bool TryGetSoftPosition(
            Transform attacker,
            Transform target,
            Vector3 currentPosition,
            bool forceRefresh,
            out Vector3 softPosition)
        {
            softPosition = currentPosition;
            if (!softPositioningEnabled || attacker == null || target == null)
            {
                return false;
            }

            TargetAttackState targetState = GetOrCreateTargetState(target);
            int attackerId = attacker.GetInstanceID();
            if (!forceRefresh && targetState.SoftPositions.TryGetValue(attackerId, out SoftPositionRecord existing))
            {
                softPosition = existing.Position;
                return true;
            }

            BuildSoftPositionCandidates(targetState, attackerId, target.position, currentPosition);
            FillOccupiedSoftPositions(targetState, attackerId);
            int selectedIndex = SelectBestSoftPosition(
                targetState.SoftPositionCandidateBuffer,
                targetState.OccupiedSoftPositionBuffer,
                currentPosition,
                softPositionMinSpacing,
                1f,
                0.18f);
            if (selectedIndex < 0)
            {
                return false;
            }

            softPosition = targetState.SoftPositionCandidateBuffer[selectedIndex];
            targetState.SoftPositions[attackerId] = new SoftPositionRecord
            {
                Position = softPosition
            };
            return true;
        }

        /// <summary>当前软站位是否已经与另一个等待敌人的位置过近。</summary>
        public bool IsSoftPositionCrowded(Transform attacker, Transform target)
        {
            if (attacker == null || target == null || !TryGetTargetState(target, out TargetAttackState targetState))
            {
                return false;
            }

            int attackerId = attacker.GetInstanceID();
            if (!targetState.SoftPositions.TryGetValue(attackerId, out SoftPositionRecord ownPosition))
            {
                return false;
            }

            float minimumDistanceSqr = softPositionMinSpacing * softPositionMinSpacing;
            foreach (KeyValuePair<int, SoftPositionRecord> pair in targetState.SoftPositions)
            {
                if (pair.Key == attackerId)
                {
                    continue;
                }

                if (GetPlanarDistanceSqr(ownPosition.Position, pair.Value.Position) < minimumDistanceSqr)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>释放指定敌人针对目标占用的软站位。</summary>
        public void ReleaseSoftPosition(Transform attacker, Transform target)
        {
            if (attacker == null || target == null || !TryGetTargetState(target, out TargetAttackState targetState))
            {
                return;
            }

            targetState.SoftPositions.Remove(attacker.GetInstanceID());
        }

        /// <summary>
        /// 更新等待敌人当前占用的软站位。
        /// 观察侧移开始时调用，让其他敌人的后续选点仍能避开这段小范围移动。
        /// </summary>
        public void UpdateSoftPosition(Transform attacker, Transform target, Vector3 position)
        {
            if (attacker == null || target == null || !softPositioningEnabled)
            {
                return;
            }

            TargetAttackState targetState = GetOrCreateTargetState(target);
            targetState.SoftPositions[attacker.GetInstanceID()] = new SoftPositionRecord
            {
                Position = position
            };
        }

        /// <summary>
        /// 从所有目标的候选和占用记录里移除指定敌人。
        /// 敌人死亡、受击打断、禁用或离开 Combat 时调用，防止遗留攻击槽。
        /// </summary>
        public void CancelAttacker(Transform attacker)
        {
            if (attacker == null)
            {
                return;
            }

            int attackerId = attacker.GetInstanceID();
            foreach (TargetAttackState targetState in _targetStates.Values)
            {
                targetState.ActiveAttackers.Remove(attackerId);
                targetState.ReservedAttackers.Remove(attackerId);
                targetState.Candidates.Remove(attackerId);
                targetState.SoftPositions.Remove(attackerId);
            }
        }

        /// <summary>敌人被禁用或切换协调器时调用，用于清理该区域内的引用和攻击槽。</summary>
        public void UnregisterEnemy(EnemyStateMachine enemy)
        {
            if (enemy == null)
            {
                return;
            }

            _assignedEnemies.Remove(enemy);
            CancelAttacker(enemy.transform);
        }

        /// <summary>
        /// 按分数从候选列表中选出攻击许可。
        /// 该方法保持无 Unity 对象依赖，方便后续继续补自动化测试和调参工具。
        /// </summary>
        public static int SelectGrantedAttackers(
            IReadOnlyList<EnemyAttackCandidate> candidates,
            int maxGrantedCount,
            List<int> grantedAttackerIds)
        {
            if (grantedAttackerIds == null)
            {
                return 0;
            }

            grantedAttackerIds.Clear();

            if (candidates == null || maxGrantedCount <= 0)
            {
                return 0;
            }

            int grantLimit = Mathf.Min(maxGrantedCount, candidates.Count);
            for (int i = 0; i < grantLimit; i++)
            {
                if (!TryFindBestCandidate(candidates, grantedAttackerIds, out EnemyAttackCandidate bestCandidate))
                {
                    break;
                }

                grantedAttackerIds.Add(bestCandidate.AttackerId);
            }

            return grantedAttackerIds.Count;
        }

        /// <summary>同时攻击容量会把已开始攻击和已获准接近的敌人一起计算。</summary>
        public static bool HasAttackCapacity(int activeCount, int reservedCount, int maxAttackers)
        {
            return Mathf.Max(0, activeCount) + Mathf.Max(0, reservedCount) < Mathf.Max(1, maxAttackers);
        }

        /// <summary>
        /// 从候选站位中选择兼顾敌人间距和移动成本的位置。
        /// 返回候选索引；没有候选时返回 -1。
        /// </summary>
        public static int SelectBestSoftPosition(
            IReadOnlyList<Vector3> candidates,
            IReadOnlyList<Vector3> occupiedPositions,
            Vector3 currentPosition,
            float minSpacing,
            float spacingWeight,
            float travelWeight)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return -1;
            }

            int bestIndex = 0;
            float bestScore = float.NegativeInfinity;
            float safeMinSpacing = Mathf.Max(0f, minSpacing);
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3 candidate = candidates[i];
                float nearestSpacing = safeMinSpacing;
                if (occupiedPositions != null && occupiedPositions.Count > 0)
                {
                    nearestSpacing = float.PositiveInfinity;
                    for (int occupiedIndex = 0; occupiedIndex < occupiedPositions.Count; occupiedIndex++)
                    {
                        nearestSpacing = Mathf.Min(
                            nearestSpacing,
                            Mathf.Sqrt(GetPlanarDistanceSqr(candidate, occupiedPositions[occupiedIndex])));
                    }
                }

                float crowdPenalty = nearestSpacing < safeMinSpacing
                    ? (safeMinSpacing - nearestSpacing) * 4f
                    : 0f;
                float travelDistance = Mathf.Sqrt(GetPlanarDistanceSqr(candidate, currentPosition));
                float score = nearestSpacing * Mathf.Max(0f, spacingWeight)
                    - crowdPenalty
                    - travelDistance * Mathf.Max(0f, travelWeight);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private void ScanEnemies()
        {
            _nextScanTime = Time.time + scanInterval;
            _seenEnemies.Clear();

            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                coordinationRadius,
                _enemyBuffer,
                enemyLayerMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider enemyCollider = _enemyBuffer[i];
                EnemyStateMachine enemy = enemyCollider != null
                    ? enemyCollider.GetComponentInParent<EnemyStateMachine>()
                    : null;
                if (enemy == null)
                {
                    continue;
                }

                float distanceSqr = GetPlanarDistanceSqr(enemy.transform.position);
                if (enemy.TryAssignCombatCoordinator(this, distanceSqr, priority))
                {
                    _assignedEnemies.Add(enemy);
                    if (!_seenEnemies.Contains(enemy))
                    {
                        _seenEnemies.Add(enemy);
                    }
                }
            }

            ReleaseEnemiesOutsideScan();
        }

        private void ReleaseEnemiesOutsideScan()
        {
            _releaseBuffer.Clear();

            foreach (EnemyStateMachine enemy in _assignedEnemies)
            {
                if (enemy == null || !_seenEnemies.Contains(enemy))
                {
                    _releaseBuffer.Add(enemy);
                }
            }

            for (int i = 0; i < _releaseBuffer.Count; i++)
            {
                EnemyStateMachine enemy = _releaseBuffer[i];
                if (enemy == null)
                {
                    _assignedEnemies.Remove(enemy);
                    continue;
                }

                if (enemy.TryClearCombatCoordinator(this, false))
                {
                    UnregisterEnemy(enemy);
                }
            }
        }

        private void ReleaseAllAssignedEnemies()
        {
            _releaseBuffer.Clear();
            _releaseBuffer.AddRange(_assignedEnemies);

            for (int i = 0; i < _releaseBuffer.Count; i++)
            {
                EnemyStateMachine enemy = _releaseBuffer[i];
                if (enemy != null)
                {
                    enemy.TryClearCombatCoordinator(this, true);
                    CancelAttacker(enemy.transform);
                }
            }

            _assignedEnemies.Clear();
        }

        private float GetPlanarDistanceSqr(Vector3 enemyPosition)
        {
            Vector3 offset = enemyPosition - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude;
        }

        private static float GetPlanarDistanceSqr(Vector3 first, Vector3 second)
        {
            Vector3 offset = first - second;
            offset.y = 0f;
            return offset.sqrMagnitude;
        }

        private void BuildSoftPositionCandidates(
            TargetAttackState targetState,
            int attackerId,
            Vector3 targetPosition,
            Vector3 currentPosition)
        {
            targetState.SoftPositionCandidateBuffer.Clear();
            int angularOffset = Mathf.Abs(attackerId % SoftPositionSampleCount);
            for (int i = 0; i < SoftPositionSampleCount; i++)
            {
                int sampleIndex = (i + angularOffset) % SoftPositionSampleCount;
                float angle = sampleIndex * (360f / SoftPositionSampleCount);
                float radius = Mathf.Lerp(
                    softPositionMinDistance,
                    softPositionMaxDistance,
                    GetStableUnitValue(attackerId, sampleIndex));
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 candidate = targetPosition + direction * radius;
                candidate.y = currentPosition.y;

                if (NavMesh.SamplePosition(candidate, out NavMeshHit navMeshHit, 1.25f, NavMesh.AllAreas))
                {
                    candidate = navMeshHit.position;
                }

                targetState.SoftPositionCandidateBuffer.Add(candidate);
            }
        }

        private static float GetStableUnitValue(int attackerId, int sampleIndex)
        {
            unchecked
            {
                uint hash = (uint)(attackerId * 397) ^ (uint)(sampleIndex * 7919);
                hash ^= hash >> 16;
                hash *= 0x7feb352d;
                hash ^= hash >> 15;
                return (hash & 0xFFFF) / 65535f;
            }
        }

        private static void FillOccupiedSoftPositions(TargetAttackState targetState, int ignoredAttackerId)
        {
            targetState.OccupiedSoftPositionBuffer.Clear();
            foreach (KeyValuePair<int, SoftPositionRecord> pair in targetState.SoftPositions)
            {
                if (pair.Key != ignoredAttackerId)
                {
                    targetState.OccupiedSoftPositionBuffer.Add(pair.Value.Position);
                }
            }
        }

        private TargetAttackState GetOrCreateTargetState(Transform target)
        {
            int targetId = target.GetInstanceID();
            if (!_targetStates.TryGetValue(targetId, out TargetAttackState targetState))
            {
                targetState = new TargetAttackState
                {
                    Target = target
                };
                _targetStates[targetId] = targetState;
            }

            return targetState;
        }

        private bool TryGetTargetState(Transform target, out TargetAttackState targetState)
        {
            targetState = null;
            return target != null && _targetStates.TryGetValue(target.GetInstanceID(), out targetState);
        }

        private void UpsertCandidate(
            TargetAttackState targetState,
            int attackerId,
            float score,
            int currentFrame)
        {
            if (!targetState.Candidates.TryGetValue(attackerId, out CandidateRecord candidate))
            {
                candidate = new CandidateRecord
                {
                    AttackerId = attackerId,
                    Sequence = _nextSequence++
                };
            }

            candidate.Score = Mathf.Max(0f, score);
            candidate.Frame = currentFrame;
            targetState.Candidates[attackerId] = candidate;
        }

        private static void FillPreviousFrameCandidates(TargetAttackState targetState, int currentFrame)
        {
            targetState.CandidateBuffer.Clear();

            foreach (CandidateRecord candidate in targetState.Candidates.Values)
            {
                if (candidate.Frame >= currentFrame)
                {
                    continue;
                }

                targetState.CandidateBuffer.Add(new EnemyAttackCandidate(
                    candidate.AttackerId,
                    candidate.Score,
                    candidate.Sequence));
            }
        }

        private static void CleanupStaleCandidates(TargetAttackState targetState, int currentFrame)
        {
            targetState.StaleCandidateIds.Clear();

            foreach (CandidateRecord candidate in targetState.Candidates.Values)
            {
                if (currentFrame - candidate.Frame > CandidateFrameGrace)
                {
                    targetState.StaleCandidateIds.Add(candidate.AttackerId);
                }
            }

            for (int i = 0; i < targetState.StaleCandidateIds.Count; i++)
            {
                targetState.Candidates.Remove(targetState.StaleCandidateIds[i]);
            }
        }

        private static void CleanupExpiredReservations(TargetAttackState targetState, float currentTime)
        {
            targetState.ExpiredReservationIds.Clear();
            foreach (KeyValuePair<int, float> reservation in targetState.ReservedAttackers)
            {
                if (reservation.Value <= currentTime)
                {
                    targetState.ExpiredReservationIds.Add(reservation.Key);
                }
            }

            for (int i = 0; i < targetState.ExpiredReservationIds.Count; i++)
            {
                targetState.ReservedAttackers.Remove(targetState.ExpiredReservationIds[i]);
            }
        }

        private static bool TryFindBestCandidate(
            IReadOnlyList<EnemyAttackCandidate> candidates,
            List<int> alreadyGrantedAttackerIds,
            out EnemyAttackCandidate bestCandidate)
        {
            bestCandidate = default;
            bool hasCandidate = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                EnemyAttackCandidate candidate = candidates[i];
                if (alreadyGrantedAttackerIds.Contains(candidate.AttackerId))
                {
                    continue;
                }

                if (!hasCandidate || IsCandidateBetter(candidate, bestCandidate))
                {
                    bestCandidate = candidate;
                    hasCandidate = true;
                }
            }

            return hasCandidate;
        }

        private static bool IsCandidateBetter(EnemyAttackCandidate candidate, EnemyAttackCandidate currentBest)
        {
            if (!Mathf.Approximately(candidate.Score, currentBest.Score))
            {
                return candidate.Score > currentBest.Score;
            }

            return candidate.Sequence < currentBest.Sequence;
        }

        private void EnsureDefaults()
        {
            if (enemyLayerMask.value == 0)
            {
                enemyLayerMask = GetDefaultEnemyLayerMask();
            }
        }

        private static LayerMask GetDefaultEnemyLayerMask()
        {
            int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
            return enemyLayer >= 0 ? 1 << enemyLayer : 0;
        }

        private void OnDrawGizmos()
        {
            if (drawGizmo)
            {
                Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.35f);
                Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, coordinationRadius));
            }

            if (!Application.isPlaying || !drawSoftPositionGizmos)
            {
                return;
            }

            foreach (TargetAttackState targetState in _targetStates.Values)
            {
                if (targetState.Target == null)
                {
                    continue;
                }

                Gizmos.color = new Color(0.15f, 0.75f, 1f, 0.22f);
                Gizmos.DrawWireSphere(targetState.Target.position, softPositionMinDistance);
                Gizmos.DrawWireSphere(targetState.Target.position, softPositionMaxDistance);

                Gizmos.color = new Color(0.15f, 0.75f, 1f, 0.8f);
                foreach (SoftPositionRecord softPosition in targetState.SoftPositions.Values)
                {
                    Gizmos.DrawSphere(softPosition.Position, 0.12f);
                    Gizmos.DrawLine(targetState.Target.position, softPosition.Position);
                }
            }
        }

        private struct CandidateRecord
        {
            public int AttackerId;
            public float Score;
            public int Sequence;
            public int Frame;
        }

        private struct SoftPositionRecord
        {
            public Vector3 Position;
        }

        private sealed class TargetAttackState
        {
            public Transform Target;
            public readonly Dictionary<int, CandidateRecord> Candidates = new();
            public readonly HashSet<int> ActiveAttackers = new();
            public readonly Dictionary<int, float> ReservedAttackers = new();
            public readonly Dictionary<int, SoftPositionRecord> SoftPositions = new();
            public readonly List<int> StaleCandidateIds = new();
            public readonly List<int> ExpiredReservationIds = new();
            public readonly List<EnemyAttackCandidate> CandidateBuffer = new();
            public readonly List<int> GrantedBuffer = new();
            public readonly List<Vector3> SoftPositionCandidateBuffer = new();
            public readonly List<Vector3> OccupiedSoftPositionBuffer = new();
            public float NextGrantTime;
        }
    }
}
