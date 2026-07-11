using UnityEngine;
using UnityEngine.Events;

namespace EndLink.World
{
    /// <summary>门的基础运动方式。</summary>
    public enum WorldDoorMotion
    {
        Slide,
        Rotate
    }

    /// <summary>门当前所处的运行状态。</summary>
    public enum WorldDoorState
    {
        Closed,
        Opening,
        Open,
        Closing
    }

    /// <summary>
    /// 通用世界门组件。
    /// 组件应挂在稳定的门根物体上，通过 Moving Part 驱动门板平移或旋转，
    /// 并复用现有世界交互系统响应玩家的交互输入。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldDoor : WorldInteractable
    {
        private const float MinMoveDuration = 0.01f;
        private const float EndpointTolerance = 0.0001f;

        [Header("门体引用")]
        [Tooltip("实际移动的门板。建议使用门根物体下的独立子物体；为空时移动当前物体。")]
        [SerializeField]
        private Transform movingPart;

        [Tooltip("门板上的可选运动学 Rigidbody。配置后使用 MovePosition / MoveRotation 驱动碰撞；为空时直接移动 Transform。")]
        [SerializeField]
        private Rigidbody movingBody;

        [Header("开关运动")]
        [Tooltip("Slide 为平移门，Rotate 为绕门板自身 Pivot 旋转。")]
        [SerializeField]
        private WorldDoorMotion motion = WorldDoorMotion.Slide;

        [Tooltip("平移门相对关闭位置的本地坐标偏移。默认向上移动 3 米。")]
        [SerializeField]
        private Vector3 openLocalOffset = new(0f, 3f, 0f);

        [Tooltip("旋转门相对关闭朝向的本地欧拉角偏移。旋转门需要把门板 Pivot 放在门轴位置。")]
        [SerializeField]
        private Vector3 openLocalEulerOffset = new(0f, 90f, 0f);

        [Tooltip("门从完全关闭移动到完全开启所需的时间。")]
        [SerializeField, Min(MinMoveDuration)]
        private float moveDuration = 1f;

        [Tooltip("进入场景时是否处于开启状态。场景中摆放的门板位置始终视为关闭位置。")]
        [SerializeField]
        private bool initiallyOpen;

        [Tooltip("运动过程中再次交互时，是否允许立即反向。关闭后只能等门到达终点再交互。")]
        [SerializeField]
        private bool allowReverseWhileMoving = true;

        [Header("提示")]
        [Tooltip("门关闭或正在关闭时显示的交互提示。")]
        [SerializeField]
        private string openPrompt = "开门";

        [Tooltip("门开启或正在开启时显示的交互提示。")]
        [SerializeField]
        private string closePrompt = "关门";

        [Header("事件")]
        [Tooltip("开始开门时触发。")]
        [SerializeField]
        private UnityEvent onOpeningStarted = new();

        [Tooltip("门完全开启时触发。")]
        [SerializeField]
        private UnityEvent onOpened = new();

        [Tooltip("开始关门时触发。")]
        [SerializeField]
        private UnityEvent onClosingStarted = new();

        [Tooltip("门完全关闭时触发。")]
        [SerializeField]
        private UnityEvent onClosed = new();

        private Vector3 _closedLocalPosition;
        private Quaternion _closedLocalRotation;
        private float _openProgress;
        private bool _targetOpen;
        private WorldDoorState _state;

        /// <summary>门当前状态。</summary>
        public WorldDoorState State => _state;

        /// <summary>门从关闭到开启的归一化进度。</summary>
        public float OpenProgress => _openProgress;

        /// <summary>门当前是否正在运动。</summary>
        public bool IsMoving => _state is WorldDoorState.Opening or WorldDoorState.Closing;

        /// <inheritdoc />
        public override string InteractionPrompt => _targetOpen ? closePrompt : openPrompt;

        private void Awake()
        {
            CacheReferences();
            CacheClosedPose();
            ConfigureBody();

            _targetOpen = initiallyOpen;
            _openProgress = initiallyOpen ? 1f : 0f;
            ApplyPose(_openProgress, false);
            _state = initiallyOpen ? WorldDoorState.Open : WorldDoorState.Closed;
        }

        private void Update()
        {
            if (movingBody == null)
            {
                TickDoor(Time.deltaTime, false);
            }
        }

        private void FixedUpdate()
        {
            if (movingBody != null)
            {
                TickDoor(Time.fixedDeltaTime, true);
            }
        }

        private void Reset()
        {
            movingPart = transform;
            CacheReferences();
            ConfigureBody();
        }

        /// <inheritdoc />
        public override bool CanInteract(GameObject interactor)
        {
            return base.CanInteract(interactor)
                && movingPart != null
                && (allowReverseWhileMoving || !IsMoving);
        }

        /// <summary>切换门的目标状态。可供按钮、剧情或 UnityEvent 直接调用。</summary>
        public bool TryToggle()
        {
            if (movingPart == null || (!allowReverseWhileMoving && IsMoving))
            {
                return false;
            }

            SetOpen(!_targetOpen);
            return true;
        }

        /// <summary>请求开门。</summary>
        public void Open()
        {
            SetOpen(true);
        }

        /// <summary>请求关门。</summary>
        public void Close()
        {
            SetOpen(false);
        }

        /// <summary>设置门的目标状态。</summary>
        public void SetOpen(bool open)
        {
            if (movingPart == null || _targetOpen == open)
            {
                return;
            }

            _targetOpen = open;
            _state = open ? WorldDoorState.Opening : WorldDoorState.Closing;

            if (open)
            {
                onOpeningStarted?.Invoke();
            }
            else
            {
                onClosingStarted?.Invoke();
            }
        }

        /// <inheritdoc />
        protected override bool OnInteract(GameObject interactor)
        {
            return TryToggle();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            moveDuration = Mathf.Max(MinMoveDuration, moveDuration);
            openPrompt ??= string.Empty;
            closePrompt ??= string.Empty;
            CacheReferences();
            ConfigureBody();
        }

        private void TickDoor(float deltaTime, bool useRigidbody)
        {
            if (!IsMoving || movingPart == null)
            {
                return;
            }

            float targetProgress = _targetOpen ? 1f : 0f;
            _openProgress = Mathf.MoveTowards(
                _openProgress,
                targetProgress,
                Mathf.Max(0f, deltaTime) / moveDuration);
            ApplyPose(_openProgress, useRigidbody);

            if (Mathf.Abs(_openProgress - targetProgress) <= EndpointTolerance)
            {
                CompleteMotion();
            }
        }

        private void CompleteMotion()
        {
            _openProgress = _targetOpen ? 1f : 0f;
            _state = _targetOpen ? WorldDoorState.Open : WorldDoorState.Closed;

            if (_targetOpen)
            {
                onOpened?.Invoke();
            }
            else
            {
                onClosed?.Invoke();
            }
        }

        private void ApplyPose(float progress, bool useRigidbody)
        {
            if (movingPart == null)
            {
                return;
            }

            float easedProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
            Vector3 openLocalPosition = _closedLocalPosition + openLocalOffset;
            Quaternion openLocalRotation = _closedLocalRotation * Quaternion.Euler(openLocalEulerOffset);
            Vector3 localPosition = motion == WorldDoorMotion.Slide
                ? Vector3.LerpUnclamped(_closedLocalPosition, openLocalPosition, easedProgress)
                : _closedLocalPosition;
            Quaternion localRotation = motion == WorldDoorMotion.Rotate
                ? Quaternion.SlerpUnclamped(_closedLocalRotation, openLocalRotation, easedProgress)
                : _closedLocalRotation;

            if (useRigidbody && movingBody != null)
            {
                Transform parent = movingPart.parent;
                Vector3 worldPosition = parent != null ? parent.TransformPoint(localPosition) : localPosition;
                Quaternion worldRotation = parent != null ? parent.rotation * localRotation : localRotation;
                movingBody.MovePosition(worldPosition);
                movingBody.MoveRotation(worldRotation);
                return;
            }

            movingPart.SetLocalPositionAndRotation(localPosition, localRotation);
        }

        private void CacheClosedPose()
        {
            if (movingPart == null)
            {
                return;
            }

            _closedLocalPosition = movingPart.localPosition;
            _closedLocalRotation = movingPart.localRotation;
        }

        private void CacheReferences()
        {
            if (movingPart == null)
            {
                movingPart = transform;
            }

            if (movingBody == null && movingPart != null)
            {
                movingBody = movingPart.GetComponent<Rigidbody>();
            }
        }

        private void ConfigureBody()
        {
            if (movingBody == null)
            {
                return;
            }

            movingBody.isKinematic = true;
            movingBody.useGravity = false;
            movingBody.interpolation = RigidbodyInterpolation.Interpolate;
            movingBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void OnDrawGizmosSelected()
        {
            Transform part = movingPart != null ? movingPart : transform;
            Vector3 closedLocalPosition = part.localPosition;
            Vector3 openLocalPosition = closedLocalPosition + openLocalOffset;
            Transform parent = part.parent;
            Vector3 closedWorldPosition = parent != null
                ? parent.TransformPoint(closedLocalPosition)
                : closedLocalPosition;
            Vector3 openWorldPosition = parent != null
                ? parent.TransformPoint(openLocalPosition)
                : openLocalPosition;

            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.8f);
            Gizmos.DrawLine(closedWorldPosition, openWorldPosition);
            Gizmos.DrawWireSphere(openWorldPosition, 0.12f);
        }
    }
}
