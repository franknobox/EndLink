using System.Collections.Generic;
using EndLink.Core;
using UnityEngine;
using UnityEngine.Events;

namespace EndLink.World
{
    /// <summary>电梯的两个固定停靠点。</summary>
    public enum ElevatorStop
    {
        Lower,
        Upper
    }

    /// <summary>两层电梯当前所处的运行状态。</summary>
    public enum ElevatorPlatformState
    {
        Lower,
        MovingUp,
        Upper,
        MovingDown
    }

    /// <summary>
    /// 两层电梯移动平台。
    /// 使用运动学 Rigidbody 驱动物理平台，并给 CharacterController 类乘客补充同帧平台位移。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ElevatorPlatform : MonoBehaviour
    {
        private const float MinTravelSpeed = 0.01f;
        private const float MinTravelDistanceSqr = 0.000001f;

        [Header("引用")]
        [Tooltip("电梯平台的运动学 Rigidbody。为空时从当前物体自动获取。")]
        [SerializeField]
        private Rigidbody platformBody;

        [Tooltip("下层停靠点。只读取其世界 Y 高度；平台 X/Z 始终使用进入场景时的初始位置。")]
        [SerializeField]
        private Transform lowerStop;

        [Tooltip("上层停靠点。只读取其世界 Y 高度；平台 X/Z 始终使用进入场景时的初始位置。")]
        [SerializeField]
        private Transform upperStop;

        [Header("运行")]
        [Tooltip("进入场景时电梯所在的初始停靠点。运行时会把平台对齐到该点。")]
        [SerializeField]
        private ElevatorStop initialStop = ElevatorStop.Lower;

        [Tooltip("电梯平均移动速度，单位米/秒。实际位移会在起步和到站阶段自动缓入缓出。")]
        [SerializeField, Min(MinTravelSpeed)]
        private float travelSpeed = 2.5f;

        [Header("事件")]
        [Tooltip("电梯开始移动时触发，可接音效、灯光或电梯门。")]
        [SerializeField]
        private UnityEvent onTravelStarted = new();

        [Tooltip("电梯抵达下层时触发。")]
        [SerializeField]
        private UnityEvent onArrivedLower = new();

        [Tooltip("电梯抵达上层时触发。")]
        [SerializeField]
        private UnityEvent onArrivedUpper = new();

        private readonly Dictionary<Component, int> _passengerOverlapCounts = new();
        private readonly List<Component> _stalePassengers = new();

        private ElevatorPlatformState _state;
        private Vector3 _horizontalAnchorPosition;
        private Vector3 _travelStartPosition;
        private Vector3 _travelDestination;
        private float _travelDuration;
        private float _travelElapsed;

        /// <summary>当前运行状态。</summary>
        public ElevatorPlatformState State => _state;

        /// <summary>当前是否正在移动。</summary>
        public bool IsMoving => !CanStartTravel(_state);

        /// <summary>当前配置是否允许发起一次新的运行。</summary>
        public bool CanAcceptTravelRequest => enabled && HasValidStops && CanStartTravel(_state);

        /// <summary>电梯开始运行事件。</summary>
        public UnityEvent OnTravelStarted => onTravelStarted;

        /// <summary>抵达下层事件。</summary>
        public UnityEvent OnArrivedLower => onArrivedLower;

        /// <summary>抵达上层事件。</summary>
        public UnityEvent OnArrivedUpper => onArrivedUpper;

        private bool HasValidStops => lowerStop != null
            && upperStop != null
            && !lowerStop.IsChildOf(transform)
            && !upperStop.IsChildOf(transform);

        private void Awake()
        {
            CacheAndConfigureBody();
            _horizontalAnchorPosition = platformBody != null
                ? platformBody.position
                : transform.position;

            if (!HasValidStops)
            {
                Debug.LogError(
                    "ElevatorPlatform 的停靠点缺失或位于电梯移动根物体下，无法运行。",
                    this);
                enabled = false;
                return;
            }

            SnapToStop(initialStop);
        }

        private void FixedUpdate()
        {
            if (!IsMoving || platformBody == null)
            {
                return;
            }

            _travelElapsed += Time.fixedDeltaTime;
            float normalizedProgress = _travelDuration <= Mathf.Epsilon
                ? 1f
                : Mathf.Clamp01(_travelElapsed / _travelDuration);
            Vector3 nextPosition = EvaluateTravelPosition(
                _travelStartPosition,
                _travelDestination,
                normalizedProgress);
            Vector3 platformDisplacement = nextPosition - platformBody.position;

            platformBody.MovePosition(nextPosition);
            ApplyPassengerDisplacement(platformDisplacement);

            if (normalizedProgress >= 1f)
            {
                CompleteTravel();
            }
        }

        private void OnDisable()
        {
            _passengerOverlapCounts.Clear();
            _stalePassengers.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            RegisterPassenger(other);
        }

        private void OnTriggerExit(Collider other)
        {
            UnregisterPassenger(other);
        }

        private void OnValidate()
        {
            travelSpeed = Mathf.Max(MinTravelSpeed, travelSpeed);

            if (platformBody == null)
            {
                platformBody = GetComponent<Rigidbody>();
            }
        }

        private void Reset()
        {
            CacheAndConfigureBody();
        }

        /// <summary>
        /// 从当前停靠点出发前往另一层。
        /// 移动中、组件无效或停靠点缺失时返回 false。
        /// </summary>
        public bool TryStartTravel()
        {
            if (!CanAcceptTravelRequest || platformBody == null)
            {
                return false;
            }

            ElevatorStop destinationStop = ResolveDestination(_state);
            _travelStartPosition = platformBody.position;
            _travelDestination = GetStopPosition(destinationStop);
            float distance = Vector3.Distance(_travelStartPosition, _travelDestination);

            if (distance * distance <= MinTravelDistanceSqr)
            {
                SetStoppedState(destinationStop);
                InvokeArrivalEvent(destinationStop);
                return true;
            }

            _travelDuration = distance / Mathf.Max(MinTravelSpeed, travelSpeed);
            _travelElapsed = 0f;
            _state = destinationStop == ElevatorStop.Upper
                ? ElevatorPlatformState.MovingUp
                : ElevatorPlatformState.MovingDown;
            onTravelStarted?.Invoke();
            return true;
        }

        /// <summary>判断指定交互者当前是否位于平台乘客 Trigger 内。</summary>
        public bool IsPassenger(GameObject passenger)
        {
            if (passenger == null)
            {
                return false;
            }

            Transform passengerTransform = passenger.transform;
            foreach (Component component in _passengerOverlapCounts.Keys)
            {
                if (component == null)
                {
                    continue;
                }

                Transform receiverTransform = component.transform;
                if (receiverTransform == passengerTransform
                    || receiverTransform.IsChildOf(passengerTransform)
                    || passengerTransform.IsChildOf(receiverTransform))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>停靠时，下一次运行目标永远是另一层。</summary>
        public static ElevatorStop ResolveDestination(ElevatorPlatformState state)
        {
            return state == ElevatorPlatformState.Upper
                ? ElevatorStop.Lower
                : ElevatorStop.Upper;
        }

        /// <summary>只有停靠在上下层时才允许发起新的运行。</summary>
        public static bool CanStartTravel(ElevatorPlatformState state)
        {
            return state == ElevatorPlatformState.Lower
                || state == ElevatorPlatformState.Upper;
        }

        /// <summary>按 SmoothStep 曲线计算本帧平台位置，让起步和到站更自然。</summary>
        public static Vector3 EvaluateTravelPosition(
            Vector3 start,
            Vector3 destination,
            float normalizedProgress)
        {
            float progress = Mathf.Clamp01(normalizedProgress);
            float easedProgress = progress * progress * (3f - 2f * progress);
            return Vector3.LerpUnclamped(start, destination, easedProgress);
        }

        /// <summary>
        /// 用平台初始 X/Z 和停靠点 Y 组合实际停靠位置。
        /// Stop 的水平坐标只用于方便在场景中摆放标记，不会让电梯发生横向移动。
        /// </summary>
        public static Vector3 ResolveStopPosition(
            Vector3 initialPlatformPosition,
            Vector3 configuredStopPosition)
        {
            return new Vector3(
                initialPlatformPosition.x,
                configuredStopPosition.y,
                initialPlatformPosition.z);
        }

        private void CacheAndConfigureBody()
        {
            if (platformBody == null)
            {
                platformBody = GetComponent<Rigidbody>();
            }

            if (platformBody == null)
            {
                return;
            }

            platformBody.isKinematic = true;
            platformBody.useGravity = false;
            platformBody.interpolation = RigidbodyInterpolation.Interpolate;
            platformBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void SnapToStop(ElevatorStop stop)
        {
            Vector3 stopPosition = GetStopPosition(stop);
            platformBody.position = stopPosition;
            transform.position = stopPosition;
            SetStoppedState(stop);
        }

        private Vector3 GetStopPosition(ElevatorStop stop)
        {
            Vector3 configuredStopPosition = stop == ElevatorStop.Upper
                ? upperStop.position
                : lowerStop.position;
            return ResolveStopPosition(_horizontalAnchorPosition, configuredStopPosition);
        }

        private void CompleteTravel()
        {
            ElevatorStop arrivedStop = _state == ElevatorPlatformState.MovingUp
                ? ElevatorStop.Upper
                : ElevatorStop.Lower;

            platformBody.position = _travelDestination;
            SetStoppedState(arrivedStop);
            InvokeArrivalEvent(arrivedStop);
        }

        private void SetStoppedState(ElevatorStop stop)
        {
            _state = stop == ElevatorStop.Upper
                ? ElevatorPlatformState.Upper
                : ElevatorPlatformState.Lower;
            _travelElapsed = 0f;
            _travelDuration = 0f;
        }

        private void InvokeArrivalEvent(ElevatorStop stop)
        {
            if (stop == ElevatorStop.Upper)
            {
                onArrivedUpper?.Invoke();
            }
            else
            {
                onArrivedLower?.Invoke();
            }
        }

        private void RegisterPassenger(Collider passengerCollider)
        {
            if (!TryResolvePassenger(passengerCollider, out Component passenger))
            {
                return;
            }

            _passengerOverlapCounts.TryGetValue(passenger, out int overlapCount);
            _passengerOverlapCounts[passenger] = overlapCount + 1;
        }

        private void UnregisterPassenger(Collider passengerCollider)
        {
            if (!TryResolvePassenger(passengerCollider, out Component passenger)
                || !_passengerOverlapCounts.TryGetValue(passenger, out int overlapCount))
            {
                return;
            }

            if (overlapCount <= 1)
            {
                _passengerOverlapCounts.Remove(passenger);
            }
            else
            {
                _passengerOverlapCounts[passenger] = overlapCount - 1;
            }
        }

        private bool TryResolvePassenger(Collider passengerCollider, out Component passenger)
        {
            passenger = null;
            if (passengerCollider == null || passengerCollider.transform.IsChildOf(transform))
            {
                return false;
            }

            IExternalDisplacementReceiver receiver =
                passengerCollider.GetComponentInParent<IExternalDisplacementReceiver>();
            if (receiver is Component receiverComponent)
            {
                passenger = receiverComponent;
                return true;
            }

            CharacterController characterController =
                passengerCollider.GetComponentInParent<CharacterController>();
            if (characterController == null)
            {
                return false;
            }

            passenger = characterController;
            return true;
        }

        private void ApplyPassengerDisplacement(Vector3 displacement)
        {
            if (displacement.sqrMagnitude <= MinTravelDistanceSqr)
            {
                return;
            }

            _stalePassengers.Clear();
            foreach (Component passenger in _passengerOverlapCounts.Keys)
            {
                if (passenger == null)
                {
                    _stalePassengers.Add(passenger);
                    continue;
                }

                if (passenger is IExternalDisplacementReceiver receiver)
                {
                    if (receiver.CanReceiveExternalDisplacement)
                    {
                        receiver.AddExternalDisplacement(displacement);
                    }

                    continue;
                }

                if (passenger is CharacterController characterController
                    && characterController.enabled)
                {
                    characterController.Move(displacement);
                    continue;
                }

                _stalePassengers.Add(passenger);
            }

            for (int i = 0; i < _stalePassengers.Count; i++)
            {
                _passengerOverlapCounts.Remove(_stalePassengers[i]);
            }
        }
    }
}
