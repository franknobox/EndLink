using EndLink.Core;
using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 玩家攻击动作中的短距离踏步与目标追踪位移。
    /// 状态机决定何时开始和结束，本组件只计算逐帧位移并交给 PlayerController 执行。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerAttackMotion : MonoBehaviour
    {
        [Header("攻击踏步")]
        [Tooltip("每段普攻最多向前推进的距离。设为 0 可关闭攻击踏步。")]
        [SerializeField, Min(0f)]
        private float stepDistance = 0.8f;

        [Tooltip("每段踏步完成所需时间。位移前快后慢，不会贯穿整个攻击动作。")]
        [SerializeField, Min(0.01f)]
        private float stepDuration = 0.16f;

        [Tooltip("靠近目标时在目标 Collider 表面前保留的距离，避免踏步穿进敌人体内。")]
        [SerializeField, Min(0f)]
        private float targetStopDistance = 0.35f;

        [Tooltip("目标表面距离超过该值时不再吸附目标，只按角色当前正前方踏步。")]
        [SerializeField, Min(0f)]
        private float maxTargetAssistDistance = 3f;

        private PlayerController _controller;
        private Transform _target;
        private Vector3 _fallbackDirection;
        private float _elapsedTime;
        private float _travelDistance;
        private float _appliedDistance;
        private bool _isActive;

        /// <summary>当前是否仍在执行本段攻击踏步。</summary>
        public bool IsActive => _isActive;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
        }

        private void OnDisable()
        {
            CancelMotion();
        }

        private void OnValidate()
        {
            stepDistance = Mathf.Max(0f, stepDistance);
            stepDuration = Mathf.Max(0.01f, stepDuration);
            targetStopDistance = Mathf.Max(0f, targetStopDistance);
            maxTargetAssistDistance = Mathf.Max(0f, maxTargetAssistDistance);
        }

        /// <summary>开始当前攻击段的踏步。</summary>
        public void BeginMotion(Transform target, Vector3 fallbackForward)
        {
            _target = ResolveUsableTarget(target);
            _fallbackDirection = NormalizePlanar(fallbackForward, transform.forward);
            _elapsedTime = 0f;
            _appliedDistance = 0f;
            _travelDistance = stepDistance;

            if (_target != null)
            {
                float surfaceDistance = CombatTargetUtility.GetSurfaceDistance(_target, transform.position);
                if (surfaceDistance <= maxTargetAssistDistance)
                {
                    _travelDistance = CalculateTravelDistance(stepDistance, surfaceDistance, targetStopDistance);
                }
                else
                {
                    _target = null;
                }
            }

            _isActive = _travelDistance > 0f;
        }

        /// <summary>推进一帧攻击踏步，并在目标移动时有限修正位移方向。</summary>
        public void TickMotion(float deltaTime)
        {
            if (!_isActive || deltaTime <= 0f)
            {
                return;
            }

            _elapsedTime += deltaTime;
            float normalizedTime = Mathf.Clamp01(_elapsedTime / stepDuration);
            float easedTime = 1f - (1f - normalizedTime) * (1f - normalizedTime);
            float desiredAppliedDistance = _travelDistance * easedTime;
            float frameDistance = Mathf.Max(0f, desiredAppliedDistance - _appliedDistance);

            Vector3 direction = ResolveCurrentDirection();
            if (_target != null)
            {
                float surfaceDistance = CombatTargetUtility.GetSurfaceDistance(_target, transform.position);
                frameDistance = Mathf.Min(frameDistance, Mathf.Max(0f, surfaceDistance - targetStopDistance));
            }

            if (frameDistance > 0f)
            {
                _controller.AddExternalDisplacement(direction * frameDistance);
                _appliedDistance += frameDistance;
            }

            if (normalizedTime >= 1f || _appliedDistance >= _travelDistance - 0.0001f)
            {
                _isActive = false;
            }
        }

        /// <summary>立即终止尚未完成的攻击踏步。</summary>
        public void CancelMotion()
        {
            _target = null;
            _elapsedTime = 0f;
            _travelDistance = 0f;
            _appliedDistance = 0f;
            _isActive = false;
        }

        /// <summary>根据目标表面距离计算不会穿入目标的实际踏步距离。</summary>
        public static float CalculateTravelDistance(float configuredDistance, float surfaceDistance, float stopDistance)
        {
            float availableDistance = Mathf.Max(0f, surfaceDistance - Mathf.Max(0f, stopDistance));
            return Mathf.Min(Mathf.Max(0f, configuredDistance), availableDistance);
        }

        private Transform ResolveUsableTarget(Transform target)
        {
            if (target == null || !CombatTargetUtility.TryResolve(target, out ICombatTarget combatTarget))
            {
                return null;
            }

            return combatTarget.IsTargetable ? combatTarget.RootTransform : null;
        }

        private Vector3 ResolveCurrentDirection()
        {
            if (_target != null)
            {
                Vector3 closestPoint = CombatTargetUtility.GetClosestPoint(_target, transform.position);
                Vector3 toTarget = closestPoint - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    return toTarget.normalized;
                }
            }

            return _fallbackDirection;
        }

        private static Vector3 NormalizePlanar(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }

            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
        }
    }
}
