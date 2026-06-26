using System;
using UnityEngine;

namespace EndLink.World
{
    /// <summary>
    /// 世界交互者。
    /// 负责在周围范围内寻找当前最合适的可交互对象，并提供统一的执行入口。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldInteractor : MonoBehaviour
    {
        private const int MinCandidateCount = 1;

        [Header("扫描配置")]
        [Tooltip("交互扫描中心。为空时使用本物体 Transform。")]
        [SerializeField]
        private Transform origin;

        [Tooltip("交互半径。玩家与交互点距离超过该半径时不会选中。")]
        [SerializeField, Min(0.1f)]
        private float interactionRadius = 2.2f;

        [Tooltip("可交互对象所在 Layer。建议后续单独建 Interactable Layer，避免扫描无关碰撞体。")]
        [SerializeField]
        private LayerMask interactableLayers = Physics.DefaultRaycastLayers;

        [Tooltip("扫描刷新间隔。0 表示每帧刷新；白模阶段建议 0.08-0.15 秒。")]
        [SerializeField, Min(0f)]
        private float refreshInterval = 0.1f;

        [Tooltip("一次扫描最多缓存的候选碰撞体数量。场景机关很多时可适当调大。")]
        [SerializeField, Min(MinCandidateCount)]
        private int maxCandidates = 16;

        [Header("遮挡")]
        [Tooltip("是否要求交互目标与扫描中心之间没有遮挡。第一版默认关闭，避免灰盒阶段被临时碰撞误挡。")]
        [SerializeField]
        private bool requireLineOfSight;

        [Tooltip("遮挡检测使用的 Layer。仅在 Require Line Of Sight 开启时生效。")]
        [SerializeField]
        private LayerMask obstructionLayers = Physics.DefaultRaycastLayers;

        [Header("调试")]
        [Tooltip("是否打印交互尝试日志。默认关闭，避免 Console 噪声。")]
        [SerializeField]
        private bool logInteractionAttempts;

        private Collider[] _candidateBuffer;
        private WorldInteractable _currentInteractable;
        private float _nextRefreshTime;

        /// <summary>
        /// 当前交互目标变化事件。参数为新的目标，目标为空表示当前没有可交互对象。
        /// </summary>
        public event Action<WorldInteractable> CurrentInteractableChanged;

        /// <summary>当前交互半径。</summary>
        public float InteractionRadius
        {
            get => interactionRadius;
            set => interactionRadius = Mathf.Max(0.1f, value);
        }

        /// <summary>扫描刷新间隔。</summary>
        public float RefreshInterval
        {
            get => refreshInterval;
            set => refreshInterval = Mathf.Max(0f, value);
        }

        /// <summary>当前选中的可交互对象。</summary>
        public WorldInteractable CurrentInteractable => _currentInteractable;

        /// <summary>当前是否有可交互对象。</summary>
        public bool HasInteractable => _currentInteractable != null;

        /// <summary>当前交互提示文本。</summary>
        public string CurrentPrompt => _currentInteractable != null ? _currentInteractable.InteractionPrompt : string.Empty;

        private Transform Origin => origin != null ? origin : transform;

        private void Awake()
        {
            EnsureCandidateBuffer();
        }

        private void Update()
        {
            if (Time.time < _nextRefreshTime)
            {
                return;
            }

            RefreshCurrentInteractable();
        }

        private void OnValidate()
        {
            interactionRadius = Mathf.Max(0.1f, interactionRadius);
            refreshInterval = Mathf.Max(0f, refreshInterval);
            maxCandidates = Mathf.Max(MinCandidateCount, maxCandidates);
            EnsureCandidateBuffer();
        }

        /// <summary>
        /// 立即刷新当前交互目标。
        /// 返回刷新后的当前目标；如果没有有效对象则返回 null。
        /// </summary>
        public WorldInteractable RefreshCurrentInteractable()
        {
            EnsureCandidateBuffer();

            Transform scanOrigin = Origin;
            Vector3 originPosition = scanOrigin.position;
            int hitCount = Physics.OverlapSphereNonAlloc(
                originPosition,
                interactionRadius,
                _candidateBuffer,
                interactableLayers,
                QueryTriggerInteraction.Collide);

            WorldInteractable best = null;
            float bestDistanceSqr = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider candidateCollider = _candidateBuffer[i];
                if (candidateCollider == null || candidateCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                WorldInteractable interactable = candidateCollider.GetComponentInParent<WorldInteractable>();
                if (interactable == null || !interactable.CanInteract(gameObject))
                {
                    continue;
                }

                Vector3 closestPoint = candidateCollider.ClosestPoint(originPosition);
                float distanceSqr = (closestPoint - originPosition).sqrMagnitude;
                if (distanceSqr > interactionRadius * interactionRadius)
                {
                    continue;
                }

                if (requireLineOfSight && IsObstructed(interactable, originPosition, closestPoint))
                {
                    continue;
                }

                if (distanceSqr < bestDistanceSqr)
                {
                    best = interactable;
                    bestDistanceSqr = distanceSqr;
                }
            }

            SetCurrentInteractable(best);
            _nextRefreshTime = Time.time + refreshInterval;
            return _currentInteractable;
        }

        /// <summary>
        /// 尝试执行当前交互对象。
        /// 如果当前缓存目标失效，会先刷新一次再尝试。
        /// </summary>
        public bool TryInteractCurrent(GameObject interactor)
        {
            GameObject actualInteractor = interactor != null ? interactor : gameObject;

            if (_currentInteractable == null || !_currentInteractable.CanInteract(actualInteractor))
            {
                RefreshCurrentInteractable();
            }

            if (_currentInteractable == null)
            {
                if (logInteractionAttempts)
                {
                    Debug.Log("WorldInteractor: no interactable target.", this);
                }

                return false;
            }

            bool success = _currentInteractable.TryInteract(actualInteractor);
            if (logInteractionAttempts)
            {
                Debug.Log(
                    $"WorldInteractor: interact target={_currentInteractable.name}, success={success}",
                    this);
            }

            if (!success)
            {
                RefreshCurrentInteractable();
            }

            return success;
        }

        private bool IsObstructed(WorldInteractable interactable, Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            float distance = direction.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                return false;
            }

            if (!Physics.Raycast(from, direction / distance, out RaycastHit hit, distance, obstructionLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return !hit.transform.IsChildOf(interactable.transform);
        }

        private void SetCurrentInteractable(WorldInteractable next)
        {
            if (_currentInteractable == next)
            {
                return;
            }

            _currentInteractable = next;
            CurrentInteractableChanged?.Invoke(_currentInteractable);
        }

        private void EnsureCandidateBuffer()
        {
            int bufferSize = Mathf.Max(MinCandidateCount, maxCandidates);
            if (_candidateBuffer == null || _candidateBuffer.Length != bufferSize)
            {
                _candidateBuffer = new Collider[bufferSize];
            }
        }

        private void OnDrawGizmosSelected()
        {
            Transform scanOrigin = origin != null ? origin : transform;
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.25f);
            Gizmos.DrawWireSphere(scanOrigin.position, interactionRadius);
        }
    }
}
