using System.Collections.Generic;
using EndLink.Core;
using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 玩家普攻连段配置与运行时入口。
    /// 管理普攻段数、下一段输入窗口，以及每段开始时的短距离攻击踏步；不读取输入，也不生成 Hitbox。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerComboController : MonoBehaviour
    {
        [Header("普攻连段")]
        [Tooltip("按顺序执行的普攻动作。空槽会回退使用 PlayerCombatDriver 的基础普攻，默认三个空槽即三段同动作连段。")]
        [SerializeField]
        private List<CombatActionDefinition> comboActions = new() { null, null, null };

        [Tooltip("动作归一化时间达到该值后，才允许缓存下一段普攻输入。")]
        [SerializeField, Range(0f, 1f)]
        private float inputWindowStart = 0.55f;

        [Tooltip("动作归一化时间超过该值后，不再接受本段的下一段输入。")]
        [SerializeField, Range(0f, 1f)]
        private float inputWindowEnd = 1f;

        [Tooltip("下一段已经排队但暂时无法执行时，最多继续等待的时间。超时后结束连段，避免永久停留在 Attack。")]
        [SerializeField, Min(0f)]
        private float queuedStepTimeout = 0.15f;

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

        private readonly PlayerComboSequence _sequence = new();
        private CombatActionDefinition _fallbackAction;
        private PlayerController _controller;
        private Transform _motionTarget;
        private Vector3 _fallbackDirection;
        private float _motionElapsedTime;
        private float _travelDistance;
        private float _appliedDistance;
        private bool _isMotionActive;

        /// <summary>当前连段的段数索引，从 0 开始。</summary>
        public int CurrentStepIndex => _sequence.CurrentStepIndex;

        /// <summary>当前段实际使用的动作资产。</summary>
        public CombatActionDefinition CurrentAction => ResolveAction(CurrentStepIndex);

        /// <summary>下一段实际使用的动作资产；没有下一段时为空。</summary>
        public CombatActionDefinition NextAction => _sequence.HasNext
            ? ResolveAction(CurrentStepIndex + 1)
            : null;

        /// <summary>是否已经在当前输入窗口中缓存了下一段。</summary>
        public bool HasQueuedNext => _sequence.HasQueuedNext;

        /// <summary>是否仍有下一段可执行。</summary>
        public bool HasNext => _sequence.HasNext;

        /// <summary>当前是否仍在执行本段攻击踏步。</summary>
        public bool IsMotionActive => _isMotionActive;

        /// <summary>下一段排队后允许等待执行条件恢复的最长时间。</summary>
        public float QueuedStepTimeout => Mathf.Max(0f, queuedStepTimeout);

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
        }

        private void OnDisable()
        {
            ResetCombo();
        }

        private void OnValidate()
        {
            inputWindowStart = Mathf.Clamp01(inputWindowStart);
            inputWindowEnd = Mathf.Clamp(inputWindowEnd, inputWindowStart, 1f);
            queuedStepTimeout = Mathf.Max(0f, queuedStepTimeout);
            stepDistance = Mathf.Max(0f, stepDistance);
            stepDuration = Mathf.Max(0.01f, stepDuration);
            targetStopDistance = Mathf.Max(0f, targetStopDistance);
            maxTargetAssistDistance = Mathf.Max(0f, maxTargetAssistDistance);
        }

        /// <summary>从第一段开始一次新的普攻连段。</summary>
        public CombatActionDefinition BeginCombo(CombatActionDefinition fallbackAction)
        {
            _fallbackAction = fallbackAction;
            int stepCount = comboActions != null && comboActions.Count > 0 ? comboActions.Count : 1;
            _sequence.Begin(stepCount);
            return CurrentAction;
        }

        /// <summary>在不改变连段运行时状态的情况下，解析第一段将使用的动作。</summary>
        public CombatActionDefinition GetFirstAction(CombatActionDefinition fallbackAction)
        {
            if (comboActions != null && comboActions.Count > 0 && comboActions[0] != null)
            {
                return comboActions[0];
            }

            return fallbackAction;
        }

        /// <summary>在当前动作的合法窗口内尝试缓存下一段。</summary>
        public bool TryQueueNext(float normalizedActionTime)
        {
            return _sequence.TryQueueNext(normalizedActionTime, inputWindowStart, inputWindowEnd);
        }

        /// <summary>判断当前时间是否处于可缓存下一段的窗口。</summary>
        public bool CanQueueNext(float normalizedActionTime)
        {
            return _sequence.CanQueueNext(normalizedActionTime, inputWindowStart, inputWindowEnd);
        }

        /// <summary>消费已缓存输入并推进到下一段。</summary>
        public bool TryAdvance()
        {
            return _sequence.TryAdvance();
        }

        /// <summary>清除当前连段进度。</summary>
        public void ResetCombo()
        {
            _fallbackAction = null;
            _sequence.Reset();
            CancelMotion();
        }

        /// <summary>开始当前普攻段的攻击踏步。</summary>
        public void BeginStepMotion(Transform target, Vector3 fallbackForward)
        {
            _motionTarget = ResolveUsableTarget(target);
            _fallbackDirection = NormalizePlanar(fallbackForward, transform.forward);
            _motionElapsedTime = 0f;
            _appliedDistance = 0f;
            _travelDistance = stepDistance;

            if (_motionTarget != null)
            {
                float surfaceDistance = CombatTargetUtility.GetSurfaceDistance(_motionTarget, transform.position);
                if (surfaceDistance <= maxTargetAssistDistance)
                {
                    _travelDistance = CalculateTravelDistance(stepDistance, surfaceDistance, targetStopDistance);
                }
                else
                {
                    _motionTarget = null;
                }
            }

            _isMotionActive = _travelDistance > 0f;
        }

        /// <summary>推进一帧攻击踏步，并在目标移动时有限修正位移方向。</summary>
        public void TickMotion(float deltaTime)
        {
            if (!_isMotionActive || deltaTime <= 0f)
            {
                return;
            }

            _motionElapsedTime += deltaTime;
            float normalizedTime = Mathf.Clamp01(_motionElapsedTime / stepDuration);
            float easedTime = 1f - (1f - normalizedTime) * (1f - normalizedTime);
            float desiredAppliedDistance = _travelDistance * easedTime;
            float frameDistance = Mathf.Max(0f, desiredAppliedDistance - _appliedDistance);

            Vector3 direction = ResolveCurrentMotionDirection();
            if (_motionTarget != null)
            {
                float surfaceDistance = CombatTargetUtility.GetSurfaceDistance(_motionTarget, transform.position);
                frameDistance = Mathf.Min(frameDistance, Mathf.Max(0f, surfaceDistance - targetStopDistance));
            }

            if (frameDistance > 0f)
            {
                _controller.AddExternalDisplacement(direction * frameDistance);
                _appliedDistance += frameDistance;
            }

            if (normalizedTime >= 1f || _appliedDistance >= _travelDistance - 0.0001f)
            {
                _isMotionActive = false;
            }
        }

        /// <summary>立即终止尚未完成的攻击踏步。</summary>
        public void CancelMotion()
        {
            _motionTarget = null;
            _motionElapsedTime = 0f;
            _travelDistance = 0f;
            _appliedDistance = 0f;
            _isMotionActive = false;
        }

        /// <summary>根据目标表面距离计算不会穿入目标的实际踏步距离。</summary>
        public static float CalculateTravelDistance(float configuredDistance, float surfaceDistance, float stopDistance)
        {
            float availableDistance = Mathf.Max(0f, surfaceDistance - Mathf.Max(0f, stopDistance));
            return Mathf.Min(Mathf.Max(0f, configuredDistance), availableDistance);
        }

        /// <summary>判断已排队的下一段是否仍处于允许等待的时间内。</summary>
        public static bool ShouldWaitForQueuedStep(float waitedTime, float timeout)
        {
            return Mathf.Max(0f, waitedTime) < Mathf.Max(0f, timeout);
        }

        private CombatActionDefinition ResolveAction(int stepIndex)
        {
            if (comboActions != null
                && stepIndex >= 0
                && stepIndex < comboActions.Count
                && comboActions[stepIndex] != null)
            {
                return comboActions[stepIndex];
            }

            return _fallbackAction;
        }

        private Transform ResolveUsableTarget(Transform target)
        {
            if (target == null || !CombatTargetUtility.TryResolve(target, out ICombatTarget combatTarget))
            {
                return null;
            }

            return combatTarget.IsTargetable ? combatTarget.RootTransform : null;
        }

        private Vector3 ResolveCurrentMotionDirection()
        {
            if (_motionTarget != null)
            {
                Vector3 closestPoint = CombatTargetUtility.GetClosestPoint(_motionTarget, transform.position);
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

    /// <summary>
    /// 不依赖场景的连段序列状态，只保存段数、当前索引和下一段缓存。
    /// </summary>
    public sealed class PlayerComboSequence
    {
        private int _stepCount;

        public int CurrentStepIndex { get; private set; }

        public bool HasQueuedNext { get; private set; }

        public bool HasNext => _stepCount > 0 && CurrentStepIndex + 1 < _stepCount;

        public void Begin(int stepCount)
        {
            _stepCount = Mathf.Max(1, stepCount);
            CurrentStepIndex = 0;
            HasQueuedNext = false;
        }

        public bool TryQueueNext(float normalizedTime, float windowStart, float windowEnd)
        {
            if (!CanQueueNext(normalizedTime, windowStart, windowEnd))
            {
                return false;
            }

            HasQueuedNext = true;
            return true;
        }

        public bool CanQueueNext(float normalizedTime, float windowStart, float windowEnd)
        {
            if (!HasNext || HasQueuedNext)
            {
                return false;
            }

            float start = Mathf.Clamp01(windowStart);
            float end = Mathf.Clamp(windowEnd, start, 1f);
            float time = Mathf.Clamp01(normalizedTime);
            return time >= start && time <= end;
        }

        public bool TryAdvance()
        {
            if (!HasQueuedNext || !HasNext)
            {
                return false;
            }

            CurrentStepIndex++;
            HasQueuedNext = false;
            return true;
        }

        public void Reset()
        {
            _stepCount = 0;
            CurrentStepIndex = 0;
            HasQueuedNext = false;
        }
    }
}
