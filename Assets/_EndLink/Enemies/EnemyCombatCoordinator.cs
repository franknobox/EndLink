using System.Collections.Generic;
using UnityEngine;

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

        [Header("调试")]
        [Tooltip("是否始终绘制协调器半径 Gizmo。")]
        [SerializeField]
        private bool drawGizmo = true;

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

            int attackerId = attacker.GetInstanceID();
            if (targetState.ActiveAttackers.Contains(attackerId))
            {
                return true;
            }

            if (Time.time < targetState.NextGrantTime)
            {
                UpsertCandidate(targetState, attackerId, score, currentFrame);
                return false;
            }

            if (targetState.ActiveAttackers.Count >= MaxSimultaneousAttackers)
            {
                UpsertCandidate(targetState, attackerId, score, currentFrame);
                return false;
            }

            int openSlotCount = MaxSimultaneousAttackers - targetState.ActiveAttackers.Count;
            FillPreviousFrameCandidates(targetState, currentFrame);
            SelectGrantedAttackers(targetState.CandidateBuffer, openSlotCount, targetState.GrantedBuffer);
            bool granted = targetState.GrantedBuffer.Contains(attackerId);

            if (!granted)
            {
                UpsertCandidate(targetState, attackerId, score, currentFrame);
            }
            else
            {
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
            targetState.Candidates.Remove(attackerId);
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
                targetState.Candidates.Remove(attackerId);
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

        private TargetAttackState GetOrCreateTargetState(Transform target)
        {
            int targetId = target.GetInstanceID();
            if (!_targetStates.TryGetValue(targetId, out TargetAttackState targetState))
            {
                targetState = new TargetAttackState();
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
            if (!drawGizmo)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, coordinationRadius));
        }

        private struct CandidateRecord
        {
            public int AttackerId;
            public float Score;
            public int Sequence;
            public int Frame;
        }

        private sealed class TargetAttackState
        {
            public readonly Dictionary<int, CandidateRecord> Candidates = new();
            public readonly HashSet<int> ActiveAttackers = new();
            public readonly List<int> StaleCandidateIds = new();
            public readonly List<EnemyAttackCandidate> CandidateBuffer = new();
            public readonly List<int> GrantedBuffer = new();
            public float NextGrantTime;
        }
    }
}
