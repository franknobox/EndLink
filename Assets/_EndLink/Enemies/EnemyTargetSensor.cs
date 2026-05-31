using EndLink.Combat;
using UnityEngine;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人索敌感知组件。
    /// 第一版只负责在一定距离内发现玩家目标，并控制 Alert 到 Combat 的累积流程；
    /// 不负责移动、攻击、仇恨排序或行为树细节，后续敌人索敌机制会继续从这里扩展。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyStateMachine))]
    public sealed class EnemyTargetSensor : MonoBehaviour
    {
        private const int TargetBufferSize = 16;

        private readonly Collider[] _targetBuffer = new Collider[TargetBufferSize];
        private EnemyStateMachine _stateMachine;
        private EnemyHealth _health;
        private Transform _currentDetectedTarget;
        private float _alertTimer;
        private bool _externalAlertControlApplied;

        /// <summary>当前传感器发现的目标。</summary>
        public Transform CurrentDetectedTarget => _currentDetectedTarget;

        /// <summary>当前 Alert 累积时间。</summary>
        public float AlertTimer => _alertTimer;

        /// <summary>Alert 累积进度，0 表示未警觉，1 表示已经满足进战条件。</summary>
        public float AlertProgress01 => RequiredAlertTime > 0f
            ? Mathf.Clamp01(_alertTimer / RequiredAlertTime)
            : 1f;

        private bool DetectionEnabled => _stateMachine != null && _stateMachine.DetectionEnabled;

        private Transform ExplicitTarget => _stateMachine != null ? _stateMachine.ExplicitDetectionTarget : null;

        private LayerMask TargetLayerMask => _stateMachine != null ? _stateMachine.TargetLayerMask : default;

        private Transform DetectionOrigin => _stateMachine != null ? _stateMachine.DetectionOrigin : transform;

        private float DetectionRadius => _stateMachine != null ? _stateMachine.DetectionRadius : 0f;

        private float RequiredAlertTime => _stateMachine != null ? _stateMachine.RequiredAlertTime : 0.01f;

        private void Reset()
        {
            CacheComponents();
        }

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            ApplyExternalAlertControl(DetectionEnabled);
        }

        private void OnDisable()
        {
            ResetDetectionState(clearStateMachineTarget: true);
            ApplyExternalAlertControl(false);
        }

        private void Update()
        {
            ApplyExternalAlertControl(DetectionEnabled);

            if (!DetectionEnabled || !CanDetect())
            {
                ResetDetectionState(clearStateMachineTarget: CurrentStateIsBeforeCombat());
                return;
            }

            Transform detectedTarget = FindDetectedTarget();

            if (detectedTarget == null)
            {
                ResetDetectionState(clearStateMachineTarget: CurrentStateIsBeforeCombat());
                return;
            }

            TickDetectedTarget(detectedTarget, Time.deltaTime);
        }

        /// <summary>
        /// 运行时开关索敌。关闭时会重置 Alert 累积并释放外部 Alert 控制。
        /// </summary>
        public void SetDetectionEnabled(bool enabled)
        {
            if (_stateMachine != null)
            {
                _stateMachine.SetDetectionEnabled(enabled);
            }

            if (!enabled)
            {
                ResetDetectionState(clearStateMachineTarget: true);
            }

            ApplyExternalAlertControl(enabled);
        }

        private void TickDetectedTarget(Transform detectedTarget, float deltaTime)
        {
            bool targetChanged = _currentDetectedTarget != detectedTarget;

            if (targetChanged)
            {
                _currentDetectedTarget = detectedTarget;
                _alertTimer = 0f;
                Log($"detected target={detectedTarget.name}");
            }

            _stateMachine.SetTarget(detectedTarget);

            if (CurrentStateIsBeforeCombat())
            {
                if (_stateMachine.CurrentStateId == EnemyStateId.Idle
                    || _stateMachine.CurrentStateId == EnemyStateId.None)
                {
                    _stateMachine.RequestAlert(detectedTarget);
                }

                _alertTimer += Mathf.Max(0f, deltaTime);

                if (_alertTimer >= RequiredAlertTime)
                {
                    Log($"alert complete, enter Combat target={detectedTarget.name}");
                    _stateMachine.RequestCombat(detectedTarget);
                }
            }
        }

        private Transform FindDetectedTarget()
        {
            if (ExplicitTarget != null)
            {
                Transform target = ResolveTargetTransform(ExplicitTarget);
                return IsTargetInRange(target) && IsTargetValid(target) ? target : null;
            }

            LayerMask targetLayerMask = TargetLayerMask;
            if (targetLayerMask.value == 0)
            {
                return null;
            }

            Transform origin = GetDetectionOrigin();
            int hitCount = Physics.OverlapSphereNonAlloc(
                origin.position,
                DetectionRadius,
                _targetBuffer,
                targetLayerMask,
                QueryTriggerInteraction.Ignore);

            Transform bestTarget = null;
            float bestDistanceSqr = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                Transform candidate = ResolveTargetTransform(_targetBuffer[i]);

                if (!IsTargetValid(candidate) || !IsTargetInRange(candidate))
                {
                    continue;
                }

                float distanceSqr = GetPlanarDistanceSqr(origin.position, candidate.position);

                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    bestTarget = candidate;
                }
            }

            return bestTarget;
        }

        private void ResetDetectionState(bool clearStateMachineTarget)
        {
            if (_currentDetectedTarget != null)
            {
                Log($"lost target={_currentDetectedTarget.name}");
            }

            _currentDetectedTarget = null;
            _alertTimer = 0f;

            if (clearStateMachineTarget && _stateMachine != null)
            {
                _stateMachine.SetTarget(null);
            }
        }

        private bool CanDetect()
        {
            if (_stateMachine == null)
            {
                return false;
            }

            if (_stateMachine.CurrentStateId == EnemyStateId.Dead)
            {
                return false;
            }

            return _health == null || !_health.IsDead;
        }

        private bool CurrentStateIsBeforeCombat()
        {
            return _stateMachine != null
                && _stateMachine.CurrentStateId != EnemyStateId.Combat
                && _stateMachine.CurrentStateId != EnemyStateId.Dead;
        }

        private bool IsTargetInRange(Transform target)
        {
            if (target == null)
            {
                return false;
            }

            return GetPlanarDistanceSqr(GetDetectionOrigin().position, target.position)
                <= DetectionRadius * DetectionRadius;
        }

        private static bool IsTargetValid(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return false;
            }

            ICombatTarget combatTarget = target.GetComponentInParent<ICombatTarget>();
            return combatTarget == null || combatTarget.IsTargetable;
        }

        private Transform ResolveTargetTransform(Transform target)
        {
            if (target == null)
            {
                return null;
            }

            ICombatTarget combatTarget = target.GetComponentInParent<ICombatTarget>();
            return combatTarget?.TargetTransform != null ? combatTarget.TargetTransform : target;
        }

        private Transform ResolveTargetTransform(Collider targetCollider)
        {
            if (targetCollider == null)
            {
                return null;
            }

            ICombatTarget combatTarget = targetCollider.GetComponentInParent<ICombatTarget>();
            if (combatTarget?.TargetTransform != null)
            {
                return combatTarget.TargetTransform;
            }

            PlayerHealth playerHealth = targetCollider.GetComponentInParent<PlayerHealth>();
            return playerHealth != null ? playerHealth.transform : targetCollider.transform;
        }

        private Transform GetDetectionOrigin()
        {
            return DetectionOrigin != null ? DetectionOrigin : transform;
        }

        private void CacheComponents()
        {
            if (_stateMachine == null)
            {
                _stateMachine = GetComponent<EnemyStateMachine>();
            }

            if (_health == null)
            {
                _health = GetComponent<EnemyHealth>();
            }
        }

        private void ApplyExternalAlertControl(bool enabled)
        {
            if (_stateMachine == null || _externalAlertControlApplied == enabled)
            {
                return;
            }

            _stateMachine.SetAlertTransitionExternallyControlled(enabled);
            _externalAlertControlApplied = enabled;
        }

        private void Log(string message)
        {
            if (_stateMachine != null && _stateMachine.LogSensorChanges)
            {
                Debug.Log($"EnemyTargetSensor: {message}", this);
            }
        }

        private void OnDrawGizmosSelected()
        {
            EnemyStateMachine stateMachine = _stateMachine != null ? _stateMachine : GetComponent<EnemyStateMachine>();
            if (stateMachine == null || !stateMachine.DrawDetectionGizmo)
            {
                return;
            }

            Transform origin = stateMachine.DetectionOrigin;
            float radius = stateMachine.DetectionRadius;
            Gizmos.color = _currentDetectedTarget != null
                ? new Color(1f, 0.7f, 0.1f, 0.35f)
                : new Color(0.2f, 0.6f, 1f, 0.25f);
            Gizmos.DrawSphere(origin.position, radius);
            Gizmos.color = _currentDetectedTarget != null
                ? new Color(1f, 0.7f, 0.1f, 1f)
                : new Color(0.2f, 0.6f, 1f, 1f);
            Gizmos.DrawWireSphere(origin.position, radius);
        }

        private static float GetPlanarDistanceSqr(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            offset.y = 0f;
            return offset.sqrMagnitude;
        }

    }
}
