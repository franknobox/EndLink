using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 玩家基础目标选择组件。
    /// 只负责在一定范围内选择 Enemy Layer 目标并保存当前目标，不控制相机、不绘制 UI、不决定攻击逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerTargeting : MonoBehaviour
    {
        private const string EnemyLayerName = "Enemy";
        private const int MaxTargetBufferSize = 32;

        [Header("搜索范围")]
        [Tooltip("目标搜索半径。")]
        [SerializeField, Min(0.1f)]
        private float searchRadius = 12f;

        [Tooltip("最大可锁定角度。0 表示只接受正前方，180 表示接受周围所有目标。")]
        [SerializeField, Range(0f, 180f)]
        private float maxTargetAngle = 70f;

        [Tooltip("目标 Layer。默认会在 Reset/OnValidate 时设置为 Enemy Layer。")]
        [SerializeField]
        private LayerMask targetLayerMask;

        [Header("参考点")]
        [Tooltip("搜索原点。为空时使用当前物体 Transform。")]
        [SerializeField]
        private Transform searchOrigin;

        [Tooltip("视角参考。为空时使用当前物体朝向；通常可拖 Main Camera 或 CameraTarget。")]
        [SerializeField]
        private Transform viewReference;

        [Header("评分")]
        [Tooltip("角度评分权重。越高越偏向选择画面/朝向中心附近目标。")]
        [SerializeField, Min(0f)]
        private float angleScoreWeight = 2f;

        [Tooltip("距离评分权重。越高越偏向选择近距离目标。")]
        [SerializeField, Min(0f)]
        private float distanceScoreWeight = 1f;

        [Header("调试")]
        [Tooltip("目标获取、切换和清除时是否打印 Debug.Log。")]
        [SerializeField]
        private bool logTargetChanges;

        private readonly Collider[] _targetBuffer = new Collider[MaxTargetBufferSize];
        private Transform _currentTarget;

        /// <summary>当前目标。</summary>
        public Transform CurrentTarget => _currentTarget;

        /// <summary>当前是否持有有效目标。</summary>
        public bool HasTarget => _currentTarget != null;

        /// <summary>目标搜索半径。</summary>
        public float SearchRadius => searchRadius;

        /// <summary>最大可锁定角度。</summary>
        public float MaxTargetAngle => maxTargetAngle;

        /// <summary>目标 LayerMask。</summary>
        public LayerMask TargetLayerMask => targetLayerMask;

        private void Reset()
        {
            targetLayerMask = GetDefaultEnemyLayerMask();
        }

        private void OnValidate()
        {
            searchRadius = Mathf.Max(0.1f, searchRadius);

            if (targetLayerMask.value == 0)
            {
                targetLayerMask = GetDefaultEnemyLayerMask();
            }
        }

        private void Update()
        {
            if (_currentTarget == null)
            {
                return;
            }

            if (!IsTargetStillValid(_currentTarget))
            {
                ClearTarget();
            }
        }

        /// <summary>
        /// 尝试搜索并设置当前目标。
        /// 返回 true 表示成功找到目标。
        /// </summary>
        public bool TryAcquireTarget()
        {
            Transform bestTarget = FindBestTarget();

            if (bestTarget == null)
            {
                ClearTarget();
                return false;
            }

            SetCurrentTarget(bestTarget);
            return true;
        }

        /// <summary>
        /// 手动设置当前目标。
        /// 主要用于调试、UI 选择或后续目标切换逻辑。
        /// </summary>
        public void SetCurrentTarget(Transform target)
        {
            if (_currentTarget == target)
            {
                return;
            }

            _currentTarget = target;

            if (logTargetChanges)
            {
                Debug.Log($"PlayerTargeting current target: {(_currentTarget != null ? _currentTarget.name : "None")}", this);
            }
        }

        /// <summary>
        /// 清除当前目标。
        /// </summary>
        public void ClearTarget()
        {
            if (_currentTarget == null)
            {
                return;
            }

            _currentTarget = null;

            if (logTargetChanges)
            {
                Debug.Log("PlayerTargeting cleared target.", this);
            }
        }

        private Transform FindBestTarget()
        {
            Transform origin = GetSearchOrigin();
            Vector3 originPosition = origin.position;
            Vector3 referenceForward = GetReferenceForward();

            int hitCount = Physics.OverlapSphereNonAlloc(
                originPosition,
                searchRadius,
                _targetBuffer,
                targetLayerMask,
                QueryTriggerInteraction.Ignore);

            Transform bestTarget = null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider targetCollider = _targetBuffer[i];
                Transform candidate = ResolveTargetTransform(targetCollider);

                if (candidate == null)
                {
                    continue;
                }

                if (!IsCombatTargetValid(candidate))
                {
                    continue;
                }

                if (!TryCalculateTargetScore(candidate, originPosition, referenceForward, out float score))
                {
                    continue;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
                }
            }

            return bestTarget;
        }

        private bool TryCalculateTargetScore(
            Transform candidate,
            Vector3 originPosition,
            Vector3 referenceForward,
            out float score)
        {
            score = 0f;

            Vector3 toTarget = candidate.position - originPosition;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (distance <= 0.001f || distance > searchRadius)
            {
                return false;
            }

            Vector3 directionToTarget = toTarget / distance;
            float angle = Vector3.Angle(referenceForward, directionToTarget);

            if (angle > maxTargetAngle)
            {
                return false;
            }

            float normalizedAngleScore = 1f - Mathf.Clamp01(angle / Mathf.Max(0.001f, maxTargetAngle));
            float normalizedDistanceScore = 1f - Mathf.Clamp01(distance / searchRadius);
            score = normalizedAngleScore * angleScoreWeight + normalizedDistanceScore * distanceScoreWeight;
            return true;
        }

        private bool IsTargetStillValid(Transform target)
        {
            if (target == null)
            {
                return false;
            }

            if (((1 << target.gameObject.layer) & targetLayerMask.value) == 0)
            {
                return false;
            }

            if (!IsCombatTargetValid(target))
            {
                return false;
            }

            Transform origin = GetSearchOrigin();
            Vector3 toTarget = target.position - origin.position;
            toTarget.y = 0f;
            return toTarget.sqrMagnitude <= searchRadius * searchRadius;
        }

        private Transform ResolveTargetTransform(Collider targetCollider)
        {
            if (targetCollider == null)
            {
                return null;
            }

            IHitReceiver receiver = targetCollider.GetComponentInParent<IHitReceiver>();
            return receiver is Component receiverComponent ? receiverComponent.transform : targetCollider.transform;
        }

        private static bool IsCombatTargetValid(Transform target)
        {
            ICombatTarget combatTarget = target.GetComponentInParent<ICombatTarget>();
            return combatTarget == null || combatTarget.IsTargetable;
        }

        private Transform GetSearchOrigin()
        {
            return searchOrigin != null ? searchOrigin : transform;
        }

        private Vector3 GetReferenceForward()
        {
            Transform reference = viewReference != null ? viewReference : transform;
            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up);

            if (forward.sqrMagnitude <= 0.0001f)
            {
                return transform.forward;
            }

            return forward.normalized;
        }

        private static LayerMask GetDefaultEnemyLayerMask()
        {
            int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
            return enemyLayer >= 0 ? 1 << enemyLayer : 0;
        }
    }
}
