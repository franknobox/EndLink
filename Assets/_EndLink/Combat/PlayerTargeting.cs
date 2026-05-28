using EndLink.Core;
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

        [Header("Input")]
        [Tooltip("玩家输入读取器。为空时会从当前物体或父物体自动查找。")]
        [SerializeField]
        private PlayerInputReader inputReader;

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

        [Header("Lock Indicator")]
        [Tooltip("锁定目标时是否显示头顶标识。")]
        [SerializeField]
        private bool showLockIndicator = true;

        [Tooltip("锁定标识预制体。为空时会自动生成一个简单的小球标识。")]
        [SerializeField]
        private GameObject lockIndicatorPrefab;

        [Tooltip("锁定标识相对目标头顶的世界偏移。默认略高于目标包围盒。")]
        [SerializeField]
        private Vector3 lockIndicatorOffset = new(0f, 0.35f, 0f);

        [Tooltip("自动生成标识的尺寸。使用自定义预制体时也会作为整体缩放。")]
        [SerializeField, Min(0.01f)]
        private float lockIndicatorScale = 0.25f;

        [Tooltip("自动生成标识的颜色。使用自定义预制体时不会改它的材质。")]
        [SerializeField]
        private Color lockIndicatorColor = new(1f, 0.85f, 0.1f, 1f);

        [Header("调试")]
        [Tooltip("目标获取、切换和清除时是否打印 Debug.Log。")]
        [SerializeField]
        private bool logTargetChanges;

        private readonly Collider[] _targetBuffer = new Collider[MaxTargetBufferSize];
        private Transform _currentTarget;
        private GameObject _lockIndicatorInstance;
        private Material _runtimeIndicatorMaterial;

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
            CacheReferences();
            targetLayerMask = GetDefaultEnemyLayerMask();
        }

        private void OnValidate()
        {
            CacheReferences();
            searchRadius = Mathf.Max(0.1f, searchRadius);
            lockIndicatorScale = Mathf.Max(0.01f, lockIndicatorScale);

            if (targetLayerMask.value == 0)
            {
                targetLayerMask = GetDefaultEnemyLayerMask();
            }
        }

        private void Update()
        {
            if (inputReader != null && inputReader.ConsumeTargetLockPressed())
            {
                ToggleTargetLock();
            }

            if (_currentTarget == null)
            {
                return;
            }

            if (!IsTargetStillValid(_currentTarget))
            {
                ClearTarget();
            }
        }

        private void LateUpdate()
        {
            UpdateLockIndicator();
        }

        private void OnDestroy()
        {
            if (_lockIndicatorInstance != null)
            {
                Destroy(_lockIndicatorInstance);
            }

            if (_runtimeIndicatorMaterial != null)
            {
                Destroy(_runtimeIndicatorMaterial);
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
        /// 切换当前锁定目标。已有目标时解锁；没有目标时尝试按当前搜索规则获取目标。
        /// </summary>
        public bool ToggleTargetLock()
        {
            if (HasTarget)
            {
                ClearTarget();
                return false;
            }

            return TryAcquireTarget();
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
            UpdateLockIndicator();

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
            SetLockIndicatorVisible(false);

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

        private void CacheReferences()
        {
            if (inputReader != null)
            {
                return;
            }

            inputReader = GetComponent<PlayerInputReader>();

            if (inputReader == null)
            {
                inputReader = GetComponentInParent<PlayerInputReader>();
            }
        }

        private void UpdateLockIndicator()
        {
            if (!showLockIndicator || _currentTarget == null)
            {
                SetLockIndicatorVisible(false);
                return;
            }

            EnsureLockIndicatorInstance();

            if (_lockIndicatorInstance == null)
            {
                return;
            }

            _lockIndicatorInstance.transform.position = GetLockIndicatorPosition(_currentTarget);
            _lockIndicatorInstance.transform.localScale = Vector3.one * lockIndicatorScale;
            SetLockIndicatorVisible(true);
        }

        private void EnsureLockIndicatorInstance()
        {
            if (_lockIndicatorInstance != null)
            {
                return;
            }

            if (lockIndicatorPrefab != null)
            {
                _lockIndicatorInstance = Instantiate(lockIndicatorPrefab);
                _lockIndicatorInstance.name = $"{lockIndicatorPrefab.name}_Runtime";
                return;
            }

            _lockIndicatorInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _lockIndicatorInstance.name = "PlayerTargeting_LockIndicator";

            if (_lockIndicatorInstance.TryGetComponent(out Collider markerCollider))
            {
                Destroy(markerCollider);
            }

            if (_lockIndicatorInstance.TryGetComponent(out Renderer markerRenderer))
            {
                _runtimeIndicatorMaterial = CreateRuntimeIndicatorMaterial();
                markerRenderer.sharedMaterial = _runtimeIndicatorMaterial;
            }
        }

        private Vector3 GetLockIndicatorPosition(Transform target)
        {
            if (TryGetTargetBounds(target, out Bounds bounds))
            {
                Vector3 top = bounds.center;
                top.y = bounds.max.y;
                return top + lockIndicatorOffset;
            }

            return target.position + lockIndicatorOffset;
        }

        private static bool TryGetTargetBounds(Transform target, out Bounds bounds)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || !renderers[i].enabled)
                {
                    continue;
                }

                bounds = renderers[i].bounds;

                for (int j = i + 1; j < renderers.Length; j++)
                {
                    if (renderers[j] != null && renderers[j].enabled)
                    {
                        bounds.Encapsulate(renderers[j].bounds);
                    }
                }

                return true;
            }

            Collider[] colliders = target.GetComponentsInChildren<Collider>();

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null || !colliders[i].enabled)
                {
                    continue;
                }

                bounds = colliders[i].bounds;

                for (int j = i + 1; j < colliders.Length; j++)
                {
                    if (colliders[j] != null && colliders[j].enabled)
                    {
                        bounds.Encapsulate(colliders[j].bounds);
                    }
                }

                return true;
            }

            bounds = default;
            return false;
        }

        private Material CreateRuntimeIndicatorMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                Debug.LogWarning("PlayerTargeting could not find a shader for the runtime lock indicator.", this);
                return null;
            }

            Material material = new(shader);

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", lockIndicatorColor);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", lockIndicatorColor);
            }

            return material;
        }

        private void SetLockIndicatorVisible(bool visible)
        {
            if (_lockIndicatorInstance != null && _lockIndicatorInstance.activeSelf != visible)
            {
                _lockIndicatorInstance.SetActive(visible);
            }
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
