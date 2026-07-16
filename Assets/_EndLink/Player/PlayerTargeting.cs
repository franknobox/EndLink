using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace EndLink.Combat
{
    /// <summary>
    /// 玩家自动软锁定目标选择模式。
    /// </summary>
    public enum PlayerTargetSelectionMode
    {
        /// <summary>优先选择范围内距离玩家最近的目标。</summary>
        Nearest = 0,

        /// <summary>优先选择视角或玩家正前方附近的目标。</summary>
        CameraForward = 1
    }

    /// <summary>
    /// 玩家目标选择组件。
    /// 负责按刷新间隔维护自动软目标，并可在需要时把当前目标固定为手动硬锁目标。
    /// 不控制相机、不决定攻击能否释放、不生成攻击判定。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerTargeting : MonoBehaviour
    {
        private const string EnemyLayerName = "Enemy";
        private const int MaxTargetBufferSize = 32;

        [Header("自动软锁定")]
        [Tooltip("是否启用自动软锁定。关闭后不会自动获取目标。")]
        [SerializeField]
        private bool autoTargetingEnabled = true;

        [Tooltip("自动刷新目标的间隔。目标不需要每帧刷新，默认 0.2 秒已经足够跟随距离变化。")]
        [SerializeField, Min(0.02f)]
        private float targetRefreshInterval = 0.2f;

        [Tooltip("自动目标选择模式。默认 Nearest，更接近隐性软锁；CameraForward 更偏动作游戏。")]
        [SerializeField]
        private PlayerTargetSelectionMode selectionMode = PlayerTargetSelectionMode.Nearest;

        [Header("搜索范围")]
        [Tooltip("自动软锁定搜索半径。敌人离开该范围后会被自动清除或切换。")]
        [SerializeField, Min(0.1f)]
        private float searchRadius = 12f;

        [Tooltip("CameraForward 模式下的最大可选角度。Nearest 模式不使用该限制。")]
        [SerializeField, Range(0f, 180f)]
        private float maxTargetAngle = 70f;

        [Tooltip("可作为软锁目标的 Layer。默认回填 Enemy Layer。")]
        [SerializeField]
        private LayerMask targetLayerMask;

        [Header("参考点")]
        [Tooltip("搜索原点。为空时使用当前物体 Transform。")]
        [SerializeField]
        private Transform searchOrigin;

        [Tooltip("视角参考。CameraForward 模式用它评分目标，目标点也会优先朝向它。为空时目标点会尝试使用 Main Camera，评分则使用玩家自身朝向。")]
        [SerializeField]
        private Transform viewReference;

        [Header("CameraForward 评分")]
        [Tooltip("CameraForward 模式下的角度评分权重。越高越偏向画面或朝向中心附近目标。")]
        [SerializeField, Min(0f)]
        private float angleScoreWeight = 2f;

        [Tooltip("CameraForward 模式下的距离评分权重。越高越偏向近距离目标。")]
        [SerializeField, Min(0f)]
        private float distanceScoreWeight = 1f;

        [Header("目标点")]
        [FormerlySerializedAs("showLockIndicator")]
        [Tooltip("有软目标或硬锁目标时是否显示目标点。")]
        [SerializeField]
        private bool showTargetIndicator = true;

        [FormerlySerializedAs("lockIndicatorPrefab")]
        [Tooltip("目标点预制体。为空时会自动生成一个简单小白点。")]
        [SerializeField]
        private GameObject targetIndicatorPrefab;

        [Tooltip("目标点相对目标包围盒中心的世界偏移。默认在身体中心。")]
        [SerializeField]
        private Vector3 targetIndicatorOffset = Vector3.zero;

        [Tooltip("目标点从目标身体表面向外推出的距离，避免被模型本体遮住。")]
        [SerializeField, Min(0f)]
        private float targetIndicatorSurfaceOffset = 0.04f;

        [Tooltip("自动生成的小白点是否尽量优先于目标身体显示。只影响自动生成材质，自定义 prefab 需要自己配置材质。")]
        [SerializeField]
        private bool targetIndicatorAlwaysOnTop = true;

        [Tooltip("目标点尺寸。使用自定义预制体时也会作为整体缩放。")]
        [SerializeField, Min(0.01f)]
        private float targetIndicatorScale = 0.12f;

        [Tooltip("自动生成目标点的颜色。使用自定义预制体时不会改它的材质。")]
        [SerializeField]
        private Color targetIndicatorColor = Color.white;

        [Header("调试")]
        [Tooltip("目标获取、切换和清除时是否打印 Debug.Log。")]
        [SerializeField]
        private bool logTargetChanges;

        private readonly Collider[] _targetBuffer = new Collider[MaxTargetBufferSize];
        private CombatTarget _currentTarget;
        private CombatTarget _hardLockedTarget;
        private GameObject _targetIndicatorInstance;
        private Material _runtimeIndicatorMaterial;
        private float _nextTargetRefreshTime;

        /// <summary>当前供攻击等系统使用的有效目标。存在硬锁时优先返回硬锁目标，否则返回自动软目标。</summary>
        public Transform CurrentTarget => GetEffectiveTarget()?.RootTransform;

        /// <summary>当前有效目标的锁定参考点。</summary>
        public Transform CurrentLockPoint => GetEffectiveTarget()?.LockPoint;

        /// <summary>当前是否持有可供战斗系统使用的有效目标。</summary>
        public bool HasTarget => GetEffectiveTarget() != null;

        /// <summary>当前是否处于手动硬锁状态。</summary>
        public bool IsHardLocked => _hardLockedTarget != null && _hardLockedTarget.IsTargetable;

        /// <summary>当前硬锁目标根节点；没有硬锁时返回 null。</summary>
        public Transform HardLockedTarget => IsHardLocked ? _hardLockedTarget.RootTransform : null;

        /// <summary>目标搜索半径。</summary>
        public float SearchRadius => searchRadius;

        /// <summary>CameraForward 模式下的最大可选角度。</summary>
        public float MaxTargetAngle => maxTargetAngle;

        /// <summary>目标 LayerMask。</summary>
        public LayerMask TargetLayerMask => targetLayerMask;

        private void Reset()
        {
            targetLayerMask = GetDefaultEnemyLayerMask();
            targetIndicatorColor = Color.white;
            targetIndicatorOffset = Vector3.zero;
            targetIndicatorScale = 0.12f;
        }

        private void OnValidate()
        {
            searchRadius = Mathf.Max(0.1f, searchRadius);
            targetRefreshInterval = Mathf.Max(0.02f, targetRefreshInterval);
            targetIndicatorScale = Mathf.Max(0.01f, targetIndicatorScale);
            targetIndicatorSurfaceOffset = Mathf.Max(0f, targetIndicatorSurfaceOffset);

            if (targetLayerMask.value == 0)
            {
                targetLayerMask = GetDefaultEnemyLayerMask();
            }
        }

        private void OnEnable()
        {
            _nextTargetRefreshTime = 0f;
        }

        private void Update()
        {
            if (_hardLockedTarget != null && !IsTargetStillValid(_hardLockedTarget))
            {
                ClearHardLock();
            }

            if (!autoTargetingEnabled)
            {
                ClearTarget();
                return;
            }

            if (_currentTarget != null && !IsTargetStillValid(_currentTarget))
            {
                ClearTarget();
            }

            if (Time.time >= _nextTargetRefreshTime)
            {
                RefreshTarget();
                _nextTargetRefreshTime = Time.time + targetRefreshInterval;
            }
        }

        private void LateUpdate()
        {
            UpdateTargetIndicator();
        }

        private void OnDestroy()
        {
            if (_targetIndicatorInstance != null)
            {
                Destroy(_targetIndicatorInstance);
            }

            if (_runtimeIndicatorMaterial != null)
            {
                Destroy(_runtimeIndicatorMaterial);
            }
        }

        /// <summary>
        /// 立即刷新一次自动软锁目标。
        /// 返回 true 表示成功找到目标，false 表示当前范围内没有可用目标。
        /// </summary>
        public bool TryAcquireTarget()
        {
            return RefreshTarget();
        }

        /// <summary>
        /// 在当前自动软目标上建立硬锁；当前没有软目标时会立即搜索一次。
        /// 返回 true 表示已经成功锁定有效目标。
        /// </summary>
        public bool TryAcquireHardLock()
        {
            CombatTarget target = _currentTarget != null && IsTargetStillValid(_currentTarget)
                ? _currentTarget
                : FindBestTarget();

            if (target == null)
            {
                ClearHardLock();
                return false;
            }

            _currentTarget = target;
            _hardLockedTarget = target;
            UpdateTargetIndicator();

            if (logTargetChanges)
            {
                Debug.Log($"PlayerTargeting hard locked: {target.RootTransform.name}", this);
            }

            return true;
        }

        /// <summary>
        /// 切换硬锁状态。已经锁定时解除；未锁定时尝试锁定当前最佳目标。
        /// 返回 true 表示调用后仍处于硬锁状态。
        /// </summary>
        public bool ToggleHardLock()
        {
            if (_hardLockedTarget != null)
            {
                ClearHardLock();
                return false;
            }

            return TryAcquireHardLock();
        }

        /// <summary>解除手动硬锁，但保留自动软目标。</summary>
        public void ClearHardLock()
        {
            if (_hardLockedTarget == null)
            {
                return;
            }

            _hardLockedTarget = null;
            UpdateTargetIndicator();

            if (logTargetChanges)
            {
                Debug.Log("PlayerTargeting cleared hard lock.", this);
            }
        }

        /// <summary>
        /// 手动设置当前软锁目标。
        /// 主要用于调试、UI 选择或后续切目标逻辑。
        /// </summary>
        public void SetCurrentTarget(Transform target)
        {
            CombatTarget resolvedTarget = null;
            if (target != null
                && CombatTargetUtility.TryResolve(target, out ICombatTarget combatTarget)
                && combatTarget.IsTargetable)
            {
                resolvedTarget = combatTarget as CombatTarget;
            }

            if (_currentTarget == resolvedTarget)
            {
                return;
            }

            _currentTarget = resolvedTarget;
            UpdateTargetIndicator();

            if (logTargetChanges)
            {
                Debug.Log(
                    $"PlayerTargeting current target: {(_currentTarget != null ? _currentTarget.RootTransform.name : "None")}",
                    this);
            }
        }

        /// <summary>
        /// 清除当前软锁目标。
        /// </summary>
        public void ClearTarget()
        {
            if (_currentTarget == null)
            {
                return;
            }

            _currentTarget = null;
            UpdateTargetIndicator();

            if (logTargetChanges)
            {
                Debug.Log("PlayerTargeting cleared target.", this);
            }
        }

        private bool RefreshTarget()
        {
            CombatTarget bestTarget = FindBestTarget();

            if (bestTarget == null)
            {
                ClearTarget();
                return false;
            }

            SetResolvedTarget(bestTarget);
            return true;
        }

        private CombatTarget FindBestTarget()
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

            CombatTarget bestTarget = null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                if (!CombatTargetUtility.TryResolve(_targetBuffer[i], out ICombatTarget resolvedTarget)
                    || !resolvedTarget.IsTargetable
                    || resolvedTarget is not CombatTarget candidate)
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
            ICombatTarget candidate,
            Vector3 originPosition,
            Vector3 referenceForward,
            out float score)
        {
            score = 0f;

            Vector3 targetPosition = candidate.LockPoint != null
                ? candidate.LockPoint.position
                : candidate.RootTransform.position;
            Vector3 toTarget = targetPosition - originPosition;
            toTarget.y = 0f;
            float centerDistanceSqr = toTarget.sqrMagnitude;
            float surfaceDistance = candidate.GetSurfaceDistance(originPosition);

            if (centerDistanceSqr <= 0.000001f || surfaceDistance > searchRadius)
            {
                return false;
            }

            if (selectionMode == PlayerTargetSelectionMode.Nearest)
            {
                score = -(surfaceDistance * surfaceDistance);
                return true;
            }

            float distance = Mathf.Sqrt(centerDistanceSqr);
            Vector3 directionToTarget = toTarget / distance;
            float angle = Vector3.Angle(referenceForward, directionToTarget);

            if (angle > maxTargetAngle)
            {
                return false;
            }

            float normalizedAngleScore = 1f - Mathf.Clamp01(angle / Mathf.Max(0.001f, maxTargetAngle));
            float normalizedDistanceScore = 1f - Mathf.Clamp01(surfaceDistance / searchRadius);
            score = normalizedAngleScore * angleScoreWeight + normalizedDistanceScore * distanceScoreWeight;
            return true;
        }

        private bool IsTargetStillValid(CombatTarget target)
        {
            if (target == null || !target.IsTargetable)
            {
                return false;
            }

            Transform root = target.RootTransform;
            if (root == null || ((1 << root.gameObject.layer) & targetLayerMask.value) == 0)
            {
                return false;
            }

            Transform origin = GetSearchOrigin();
            return target.GetSurfaceDistance(origin.position) <= searchRadius;
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

        private void UpdateTargetIndicator()
        {
            CombatTarget target = GetEffectiveTarget();
            if (!showTargetIndicator || target == null)
            {
                SetTargetIndicatorVisible(false);
                return;
            }

            EnsureTargetIndicatorInstance();

            if (_targetIndicatorInstance == null)
            {
                return;
            }

            _targetIndicatorInstance.transform.position = GetTargetIndicatorPosition(target);
            _targetIndicatorInstance.transform.rotation = GetTargetIndicatorRotation(_targetIndicatorInstance.transform.position);
            _targetIndicatorInstance.transform.localScale = Vector3.one * targetIndicatorScale;
            SetTargetIndicatorVisible(true);
        }

        private void EnsureTargetIndicatorInstance()
        {
            if (_targetIndicatorInstance != null)
            {
                return;
            }

            if (targetIndicatorPrefab != null)
            {
                _targetIndicatorInstance = Instantiate(targetIndicatorPrefab);
                _targetIndicatorInstance.name = $"{targetIndicatorPrefab.name}_Runtime";
                return;
            }

            _targetIndicatorInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _targetIndicatorInstance.name = "PlayerTargeting_SoftTargetIndicator";

            if (_targetIndicatorInstance.TryGetComponent(out Collider markerCollider))
            {
                Destroy(markerCollider);
            }

            if (_targetIndicatorInstance.TryGetComponent(out Renderer markerRenderer))
            {
                _runtimeIndicatorMaterial = CreateRuntimeIndicatorMaterial();
                markerRenderer.sharedMaterial = _runtimeIndicatorMaterial;
            }
        }

        private Vector3 GetTargetIndicatorPosition(ICombatTarget target)
        {
            Vector3 viewerPosition = GetTargetIndicatorViewerPosition();
            Vector3 referencePosition = target.LockPoint != null
                ? target.LockPoint.position
                : target.RootTransform.position;
            Vector3 toViewer = Vector3.ProjectOnPlane(viewerPosition - referencePosition, Vector3.up);

            if (toViewer.sqrMagnitude <= 0.0001f)
            {
                toViewer = -GetReferenceForward();
            }

            toViewer.Normalize();
            Vector3 surfacePoint = target.GetClosestPoint(viewerPosition);
            return surfacePoint + toViewer * targetIndicatorSurfaceOffset + targetIndicatorOffset;
        }

        private Quaternion GetTargetIndicatorRotation(Vector3 indicatorPosition)
        {
            Vector3 facingDirection = GetTargetIndicatorViewerPosition() - indicatorPosition;

            if (facingDirection.sqrMagnitude <= 0.0001f)
            {
                facingDirection = -GetReferenceForward();
            }

            return Quaternion.LookRotation(facingDirection.normalized, Vector3.up);
        }

        private Vector3 GetTargetIndicatorViewerPosition()
        {
            Transform indicatorReference = GetTargetIndicatorFacingReference();
            return indicatorReference != null ? indicatorReference.position : GetSearchOrigin().position;
        }

        private Transform GetTargetIndicatorFacingReference()
        {
            if (viewReference != null)
            {
                return viewReference;
            }

            Camera mainCamera = Camera.main;
            return mainCamera != null ? mainCamera.transform : null;
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
                Debug.LogWarning("PlayerTargeting could not find a shader for the runtime target indicator.", this);
                return null;
            }

            Material material = new(shader);

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", targetIndicatorColor);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", targetIndicatorColor);
            }

            ConfigureIndicatorMaterialDepth(material);
            return material;
        }

        private void ConfigureIndicatorMaterialDepth(Material material)
        {
            if (!targetIndicatorAlwaysOnTop || material == null)
            {
                return;
            }

            material.renderQueue = (int)RenderQueue.Overlay;

            if (material.HasProperty("_ZTest"))
            {
                material.SetFloat("_ZTest", (float)CompareFunction.Always);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }
        }

        private void SetTargetIndicatorVisible(bool visible)
        {
            if (_targetIndicatorInstance != null && _targetIndicatorInstance.activeSelf != visible)
            {
                _targetIndicatorInstance.SetActive(visible);
            }
        }

        private static LayerMask GetDefaultEnemyLayerMask()
        {
            int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
            return enemyLayer >= 0 ? 1 << enemyLayer : 0;
        }

        private void SetResolvedTarget(CombatTarget target)
        {
            if (_currentTarget == target)
            {
                return;
            }

            _currentTarget = target;
            UpdateTargetIndicator();

            if (logTargetChanges)
            {
                Debug.Log(
                    $"PlayerTargeting current target: {(_currentTarget != null ? _currentTarget.RootTransform.name : "None")}",
                    this);
            }
        }

        private CombatTarget GetEffectiveTarget()
        {
            if (_hardLockedTarget != null && _hardLockedTarget.IsTargetable)
            {
                return _hardLockedTarget;
            }

            return _currentTarget != null && _currentTarget.IsTargetable
                ? _currentTarget
                : null;
        }
    }
}
