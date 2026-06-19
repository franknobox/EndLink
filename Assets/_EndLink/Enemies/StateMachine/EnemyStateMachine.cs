using System.Collections.Generic;
using EndLink.Combat;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人有限状态机。
    /// 当前管理 Idle、Alert、Combat、Hit、Return、Dead 这些大状态；
    /// 更细的攻击、技能、撤退和复杂站位行为会放进 Combat 内部的行为树。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyActor))]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyStateMachine : MonoBehaviour
    {
        private const string PlayerLayerName = "Player";
        private const float StateIndicatorHeadOffset = 0.25f;
        private const float StateIndicatorScale = 0.12f;
        private static readonly Color AlertIndicatorColor = new(1f, 0.85f, 0.05f, 1f);
        private static readonly Color CombatIndicatorColor = new(1f, 0.12f, 0.08f, 1f);

        [Header("初始状态")]
        [Tooltip("敌人启用后的初始大状态。通常使用 Idle。")]
        [SerializeField]
        private EnemyStateId initialState = EnemyStateId.Idle;

        [Header("警觉状态")]
        [Tooltip("进入 Alert 后停留的时间。结束后有有效目标则进入 Combat，否则回到 Idle。")]
        [SerializeField, Min(0.01f)]
        private float alertDuration = 0.35f;

        [Header("索敌感知")]
        [Tooltip("是否启用敌人自动索敌。关闭后敌人不会因为玩家进入范围而进入 Alert / Combat。")]
        [SerializeField]
        private bool detectionEnabled = true;

        [Tooltip("指定玩家目标。配置后优先检测该目标是否在范围内；为空时使用 Target Layer Mask 搜索。")]
        [SerializeField]
        private Transform explicitDetectionTarget;

        [Tooltip("索敌检测使用的 Layer。未指定 Explicit Target 时才会使用。建议设置为 Player。")]
        [SerializeField]
        private LayerMask targetLayerMask;

        [Tooltip("索敌原点。为空时使用敌人自身 Transform。")]
        [SerializeField]
        private Transform detectionOrigin;

        [Tooltip("敌人发现目标的半径。目标离开该范围后，Alert 累积会重置。")]
        [SerializeField, Min(0.1f)]
        private float detectionRadius = 8f;

        [Tooltip("目标持续停留在发现范围内多久后进入 Combat。")]
        [SerializeField, Min(0.01f)]
        private float requiredAlertTime = 3f;

        [Header("受击状态")]
        [Tooltip("受击硬直的基础持续时间。后续可由攻击数据、霸体或韧性系统覆盖。")]
        [SerializeField, Min(0.01f)]
        private float hitDuration = 0.25f;

        [Tooltip("受到有效伤害时，是否把当前战斗目标切换为伤害来源。")]
        [SerializeField]
        private bool retargetOnDamage = true;

        [Tooltip("实际伤害达到该值时才进入 Hit 状态。小于等于 0 表示任何有效伤害都会触发 Hit。低于阈值的轻击只会让敌人接战，不会打断当前状态。")]
        [SerializeField, Min(0f)]
        private float heavyHitDamageThreshold = 8f;

        [Tooltip("两次 Hit 状态触发之间的最短间隔，避免多段 Hitbox 在极短时间内反复打断敌人。")]
        [SerializeField, Min(0f)]
        private float hitReactCooldown = 0.12f;

        [Header("Combat 移动与攻击")]
        [Tooltip("基础敌人追击目标时，和目标表面之间保留的很近间隔。实际中心停止距离会自动加上敌人和目标的碰撞半径。")]
        [SerializeField, Min(0f)]
        private float combatChaseStopDistance = 0.2f;

        [Tooltip("Combat 内部普通攻击的攻击距离容差。实际进入攻击距离 = Basic Attack 的 Effective Attack Range + 该值。")]
        [SerializeField, Min(0f)]
        private float combatAttackRangeTolerance = 0.15f;

        [Tooltip("Combat 内部接近攻击目标时的内缩距离。敌人会尝试比动作极限攻击距离更近一点，避免卡在刚好够不到的位置。")]
        [SerializeField, Min(0f)]
        private float combatAttackInnerOffset = 0.1f;

        [Header("脱战与归位")]
        [Tooltip("敌人的归位参考点。为空时记录敌人创建时的世界坐标作为 Home。")]
        [SerializeField]
        private Transform homePoint;

        [FormerlySerializedAs("combatLeashDistance")]
        [Tooltip("敌人距离 Home 超过该半径时停止追击并进入 Return。小于等于 0 表示不限制最大追击半径。")]
        [SerializeField, Min(0f)]
        private float maxChaseRadius = 18f;

        [Tooltip("当前战斗目标失效后等待重新获取目标的时间。等待结束仍无目标时进入 Return。")]
        [SerializeField, Min(0f)]
        private float lostTargetDelay = 0.75f;

        [Tooltip("返回 Home 时允许的水平停止距离。进入该范围后恢复 Idle。")]
        [SerializeField, Min(0f)]
        private float returnStopDistance = 0.2f;

        [Header("调试")]
        [Tooltip("是否打印敌人大状态切换日志。排查受击、进战和死亡流程时开启。")]
        [SerializeField]
        private bool logStateChanges;

        [Tooltip("是否打印索敌发现目标、丢失目标和进入 Combat 的日志。")]
        [SerializeField]
        private bool logSensorChanges;

        [Tooltip("是否绘制索敌范围 Gizmo。")]
        [SerializeField]
        private bool drawDetectionGizmo = true;

        private readonly Dictionary<EnemyStateId, IEnemyState> _states = new();
        private readonly List<Renderer> _boundsRenderers = new();
        private readonly List<Collider> _boundsColliders = new();
        private IEnemyState _currentState;
        private EnemyActor _actor;
        private EnemyHealth _health;
        private Transform _currentTarget;
        private GameObject _stateIndicatorInstance;
        private Material _stateIndicatorMaterial;
        private Renderer _stateIndicatorRenderer;
        private bool _alertTransitionExternallyControlled;
        private EnemyStateId _alertFallbackState = EnemyStateId.Idle;
        private float _nextHitReactTime;
        private Vector3 _capturedHomePosition;
        private bool _hasCapturedHome;

        /// <summary>当前状态标识，方便 Inspector 和调试工具观察。</summary>
        public EnemyStateId CurrentStateId => _currentState?.StateId ?? EnemyStateId.None;

        /// <summary>当前敌人关注或战斗的目标。</summary>
        public Transform CurrentTarget => _currentTarget;

        /// <summary>当前目标是否仍然有效。</summary>
        public bool HasValidTarget => IsTargetValid(_currentTarget);

        /// <summary>警觉状态持续时间。</summary>
        public float AlertDuration => alertDuration;

        /// <summary>是否启用自动索敌。</summary>
        public bool DetectionEnabled => detectionEnabled;

        /// <summary>显式索敌目标。为空时按 Layer 搜索。</summary>
        public Transform ExplicitDetectionTarget => explicitDetectionTarget;

        /// <summary>索敌搜索 Layer。</summary>
        public LayerMask TargetLayerMask => targetLayerMask;

        /// <summary>索敌原点。为空时使用敌人根物体。</summary>
        public Transform DetectionOrigin => detectionOrigin != null ? detectionOrigin : transform;

        /// <summary>索敌半径。</summary>
        public float DetectionRadius => detectionRadius;

        /// <summary>目标停留多久后进入 Combat。</summary>
        public float RequiredAlertTime => requiredAlertTime;

        /// <summary>是否打印索敌日志。</summary>
        public bool LogSensorChanges => logSensorChanges;

        /// <summary>是否绘制索敌范围 Gizmo。</summary>
        public bool DrawDetectionGizmo => drawDetectionGizmo;

        /// <summary>受击硬直持续时间。</summary>
        public float HitDuration => hitDuration;

        /// <summary>Combat 状态追击目标时的停止距离。</summary>
        public float CombatChaseStopDistance => combatChaseStopDistance;

        /// <summary>Combat 状态进入普通攻击距离时额外放宽的容差。</summary>
        public float CombatAttackRangeTolerance => combatAttackRangeTolerance;

        /// <summary>Combat 状态接近攻击目标时，相对动作极限距离向内靠近的距离。</summary>
        public float CombatAttackInnerOffset => combatAttackInnerOffset;

        /// <summary>敌人的归位位置。配置 Home Point 时实时读取，否则使用创建时记录的位置。</summary>
        public Vector3 HomePosition => homePoint != null ? homePoint.position : _capturedHomePosition;

        /// <summary>敌人距离 Home 允许的最大追击半径。小于等于 0 表示不限制。</summary>
        public float MaxChaseRadius => maxChaseRadius;

        /// <summary>战斗目标失效后等待重新获取目标的时间。</summary>
        public float LostTargetDelay => lostTargetDelay;

        /// <summary>Return 状态抵达 Home 使用的水平停止距离。</summary>
        public float ReturnStopDistance => returnStopDistance;

        /// <summary>Alert 到 Combat / Idle 的转换是否由外部索敌组件控制。</summary>
        public bool AlertTransitionExternallyControlled => _alertTransitionExternallyControlled;

        private void Awake()
        {
            EnsureDetectionDefaults();
            _actor = GetComponent<EnemyActor>();
            _health = GetComponent<EnemyHealth>();
            CaptureHomeIfNeeded();

            EnemyStateContext context = new EnemyStateContext(
                this,
                _actor,
                _health,
                transform);

            RegisterState(new EnemyIdleState(context));
            RegisterState(new EnemyAlertState(context));
            RegisterState(new EnemyCombatState(context));
            RegisterState(new EnemyHitState(context));
            RegisterState(new EnemyDeadState(context));
            RegisterState(new EnemyReturnState(context));
            CacheEnemyBoundsComponents();
        }

        private void OnEnable()
        {
            if (_health == null)
            {
                _health = GetComponent<EnemyHealth>();
            }

            _health.OnDamaged.AddListener(HandleDamaged);
            _health.OnDead.AddListener(HandleDead);
            _health.ResetPerformed += HandleHealthReset;
            ResetRuntimeState();
        }

        private void Update()
        {
            _currentState?.Tick(Time.deltaTime);
        }

        private void LateUpdate()
        {
            UpdateStateIndicator();
        }

        private void OnDisable()
        {
            if (_health == null)
            {
                return;
            }

            _health.OnDamaged.RemoveListener(HandleDamaged);
            _health.OnDead.RemoveListener(HandleDead);
            _health.ResetPerformed -= HandleHealthReset;

            _currentState?.Exit();
            _currentState = null;
            _currentTarget = null;
            _actor?.CombatDriver?.CancelCurrentAction();
            SetStateIndicatorVisible(false);
        }

        private void OnValidate()
        {
            alertDuration = Mathf.Max(0.01f, alertDuration);
            detectionRadius = Mathf.Max(0.1f, detectionRadius);
            requiredAlertTime = Mathf.Max(0.01f, requiredAlertTime);
            hitDuration = Mathf.Max(0.01f, hitDuration);
            heavyHitDamageThreshold = Mathf.Max(0f, heavyHitDamageThreshold);
            hitReactCooldown = Mathf.Max(0f, hitReactCooldown);
            combatChaseStopDistance = Mathf.Max(0f, combatChaseStopDistance);
            combatAttackRangeTolerance = Mathf.Max(0f, combatAttackRangeTolerance);
            combatAttackInnerOffset = Mathf.Max(0f, combatAttackInnerOffset);
            maxChaseRadius = Mathf.Max(0f, maxChaseRadius);
            lostTargetDelay = Mathf.Max(0f, lostTargetDelay);
            returnStopDistance = Mathf.Max(0f, returnStopDistance);
            EnsureDetectionDefaults();
            CacheEnemyBoundsComponents();
        }

        private void OnDestroy()
        {
            if (_stateIndicatorInstance != null)
            {
                Destroy(_stateIndicatorInstance);
            }

            if (_stateIndicatorMaterial != null)
            {
                Destroy(_stateIndicatorMaterial);
            }
        }

        /// <summary>
        /// 设置当前敌人关注或战斗的目标。
        /// 这里只保存引用，不主动切换状态。
        /// </summary>
        public void SetTarget(Transform target)
        {
            _currentTarget = target;
        }

        /// <summary>
        /// 设置 Alert 状态是否由外部系统控制转换。
        /// 索敌组件需要持续累积警觉时间时会启用它，避免 Alert 状态按自身默认时长提前进战。
        /// </summary>
        public void SetAlertTransitionExternallyControlled(bool controlled)
        {
            _alertTransitionExternallyControlled = controlled;
        }

        /// <summary>
        /// 运行时开关索敌。实际检测和状态切换由 EnemyTargetSensor 执行。
        /// </summary>
        public void SetDetectionEnabled(bool enabled)
        {
            detectionEnabled = enabled;
        }

        /// <summary>
        /// 把敌人当前位置记录为新的 Home。
        /// 对象池或刷新系统在移动敌人到新出生点后可以调用该入口。
        /// </summary>
        public void CaptureCurrentPositionAsHome()
        {
            homePoint = null;
            _capturedHomePosition = transform.position;
            _hasCapturedHome = true;
        }

        /// <summary>
        /// 清理当前目标、动作和状态运行数据，并按当前生命状态重新进入初始状态或 Dead。
        /// 用于敌人重新启用、生命重置和后续对象池复用。
        /// </summary>
        public void ResetRuntimeState()
        {
            _currentState?.Exit();
            _currentState = null;
            _currentTarget = null;
            _alertFallbackState = EnemyStateId.Idle;
            _nextHitReactTime = 0f;
            _actor?.CombatDriver?.ResetRuntimeState();

            EnemyStateId resetState = _health != null && _health.IsDead
                ? EnemyStateId.Dead
                : initialState;
            ChangeState(resetState);
        }

        /// <summary>
        /// 请求进入 Alert 状态。
        /// 可选目标不为空时会先更新当前目标。
        /// </summary>
        public bool RequestAlert(Transform target = null)
        {
            if (!CanAcceptNonDeadRequest())
            {
                return false;
            }

            if (CurrentStateId != EnemyStateId.Alert)
            {
                _alertFallbackState = CurrentStateId == EnemyStateId.Return
                    ? EnemyStateId.Return
                    : EnemyStateId.Idle;
            }

            if (target != null)
            {
                SetTarget(target);
            }

            ChangeState(EnemyStateId.Alert);
            return CurrentStateId == EnemyStateId.Alert;
        }

        /// <summary>
        /// 请求进入 Combat 大状态。
        /// Combat 当前负责基础追击和面向目标，复杂攻击决策之后由行为树接管。
        /// </summary>
        public bool RequestCombat(Transform target = null)
        {
            if (!CanAcceptNonDeadRequest())
            {
                return false;
            }

            _alertFallbackState = EnemyStateId.Idle;

            if (target != null)
            {
                SetTarget(target);
            }

            ChangeState(EnemyStateId.Combat);
            return CurrentStateId == EnemyStateId.Combat;
        }

        /// <summary>
        /// Alert 失去目标时返回进入警觉前的安全状态。
        /// 从 Return 进入的 Alert 会继续归位，其余情况回到 Idle。
        /// </summary>
        public void ReturnFromAlert()
        {
            EnemyStateId fallbackState = _alertFallbackState == EnemyStateId.Return
                ? EnemyStateId.Return
                : EnemyStateId.Idle;

            _alertFallbackState = EnemyStateId.Idle;
            SetTarget(null);
            ChangeState(fallbackState);
        }

        /// <summary>
        /// 请求进入 Hit 状态。
        /// 受击可以打断 Idle、Alert、Combat 和 Return，但不能覆盖 Dead。
        /// </summary>
        public bool RequestHit()
        {
            if (!CanAcceptNonDeadRequest())
            {
                return false;
            }

            ChangeState(EnemyStateId.Hit);
            return CurrentStateId == EnemyStateId.Hit;
        }

        /// <summary>
        /// 请求进入 Dead 状态。
        /// Dead 是最高优先级终态，进入后不会再被普通请求覆盖。
        /// </summary>
        public bool RequestDead()
        {
            ChangeState(EnemyStateId.Dead);
            return CurrentStateId == EnemyStateId.Dead;
        }

        /// <summary>请求停止当前战斗并返回 Home。</summary>
        public bool RequestReturn()
        {
            if (!CanAcceptNonDeadRequest())
            {
                return false;
            }

            ChangeState(EnemyStateId.Return);
            return CurrentStateId == EnemyStateId.Return;
        }

        /// <summary>
        /// 切换到指定大状态。
        /// 目标状态不存在、为 None 或等于当前状态时不会重复切换。
        /// </summary>
        public void ChangeState(EnemyStateId nextStateId)
        {
            if (nextStateId == EnemyStateId.None || CurrentStateId == nextStateId)
            {
                return;
            }

            if (CurrentStateId == EnemyStateId.Dead && nextStateId != EnemyStateId.Dead)
            {
                return;
            }

            if (nextStateId != EnemyStateId.Dead && _health != null && _health.IsDead)
            {
                nextStateId = EnemyStateId.Dead;
            }

            if (!_states.TryGetValue(nextStateId, out IEnemyState nextState))
            {
                Debug.LogError($"未注册敌人状态：{nextStateId}", this);
                return;
            }

            EnemyStateId previousStateId = CurrentStateId;

            _currentState?.Exit();
            _currentState = nextState;
            _currentState.Enter();

            if (logStateChanges)
            {
                Debug.Log(
                    $"EnemyState: {previousStateId} -> {nextStateId} | target={GetTransformName(_currentTarget)}",
                    this);
            }

            UpdateStateIndicator();
        }

        private void HandleDamaged(int damage, CombatTagDefinition tag)
        {
            if (_health != null && _health.IsDead)
            {
                RequestDead();
                return;
            }

            bool acquiredTarget = TryRetargetFromDamageSource();
            float currentTime = Time.time;

            if (ShouldTriggerHitReaction(
                    damage,
                    heavyHitDamageThreshold,
                    currentTime,
                    _nextHitReactTime))
            {
                _nextHitReactTime = currentTime + hitReactCooldown;
                RequestHit();
                return;
            }

            if (acquiredTarget
                && CurrentStateId != EnemyStateId.Combat
                && CurrentStateId != EnemyStateId.Hit)
            {
                RequestCombat(_currentTarget);
            }
        }

        private void HandleDead()
        {
            RequestDead();
        }

        private void HandleHealthReset()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            EnemyStateId expectedState = _health != null && _health.IsDead
                ? EnemyStateId.Dead
                : initialState;
            if (CurrentStateId == expectedState && _currentTarget == null)
            {
                return;
            }

            ResetRuntimeState();
        }

        private bool CanAcceptNonDeadRequest()
        {
            return CurrentStateId != EnemyStateId.Dead && (_health == null || !_health.IsDead);
        }

        private bool TryRetargetFromDamageSource()
        {
            if (!retargetOnDamage
                || _health == null
                || !TryResolveDamageSourceTarget(_health.LastDamageSource, out Transform damageSourceTarget))
            {
                return false;
            }

            SetTarget(damageSourceTarget);
            return true;
        }

        public static bool ShouldTriggerHitReaction(
            int damage,
            float heavyHitDamageThreshold,
            float currentTime,
            float nextAllowedTime)
        {
            if (damage <= 0 || currentTime < nextAllowedTime)
            {
                return false;
            }

            return heavyHitDamageThreshold <= 0f || damage >= heavyHitDamageThreshold;
        }

        public static bool TryResolveDamageSourceTarget(GameObject damageSource, out Transform target)
        {
            target = null;

            if (damageSource == null
                || !CombatTargetUtility.TryResolve(damageSource, out ICombatTarget combatTarget)
                || !combatTarget.IsTargetable)
            {
                return false;
            }

            target = combatTarget.RootTransform;
            return target != null;
        }

        private void RegisterState(IEnemyState state)
        {
            _states[state.StateId] = state;
        }

        private static bool IsTargetValid(Transform target)
        {
            return CombatTargetUtility.IsTargetable(target);
        }

        private static string GetTransformName(Transform target)
        {
            return target != null ? target.name : "None";
        }

        private void EnsureDetectionDefaults()
        {
            if (targetLayerMask.value == 0)
            {
                targetLayerMask = GetDefaultPlayerLayerMask();
            }
        }

        private void CaptureHomeIfNeeded()
        {
            if (_hasCapturedHome || homePoint != null)
            {
                return;
            }

            _capturedHomePosition = transform.position;
            _hasCapturedHome = true;
        }

        private static LayerMask GetDefaultPlayerLayerMask()
        {
            int playerLayer = LayerMask.NameToLayer(PlayerLayerName);
            return playerLayer >= 0 ? 1 << playerLayer : 0;
        }

        private void UpdateStateIndicator()
        {
            if (!TryGetStateIndicatorColor(CurrentStateId, out Color indicatorColor))
            {
                SetStateIndicatorVisible(false);
                return;
            }

            EnsureStateIndicatorInstance();

            if (_stateIndicatorInstance == null)
            {
                return;
            }

            _stateIndicatorInstance.transform.position = GetStateIndicatorPosition();
            _stateIndicatorInstance.transform.rotation = GetStateIndicatorRotation(_stateIndicatorInstance.transform.position);
            _stateIndicatorInstance.transform.localScale = Vector3.one * StateIndicatorScale;
            SetStateIndicatorColor(indicatorColor);
            SetStateIndicatorVisible(true);
        }

        private bool TryGetStateIndicatorColor(EnemyStateId stateId, out Color indicatorColor)
        {
            switch (stateId)
            {
                case EnemyStateId.Alert:
                    indicatorColor = AlertIndicatorColor;
                    return true;
                case EnemyStateId.Combat:
                    indicatorColor = CombatIndicatorColor;
                    return true;
                default:
                    indicatorColor = Color.clear;
                    return false;
            }
        }

        private void EnsureStateIndicatorInstance()
        {
            if (_stateIndicatorInstance != null)
            {
                return;
            }

            _stateIndicatorInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _stateIndicatorInstance.name = "EnemyStateIndicator_Runtime";

            if (_stateIndicatorInstance.TryGetComponent(out Collider markerCollider))
            {
                Destroy(markerCollider);
            }

            _stateIndicatorRenderer = _stateIndicatorInstance.GetComponentInChildren<Renderer>();
            PrepareStateIndicatorMaterial();
        }

        private void PrepareStateIndicatorMaterial()
        {
            if (_stateIndicatorRenderer == null)
            {
                return;
            }

            _stateIndicatorMaterial = CreateStateIndicatorMaterial(_stateIndicatorRenderer.sharedMaterial);
            _stateIndicatorRenderer.sharedMaterial = _stateIndicatorMaterial;
        }

        private Vector3 GetStateIndicatorPosition()
        {
            if (TryGetEnemyBounds(out Bounds bounds))
            {
                Vector3 top = bounds.center;
                top.y = bounds.max.y;
                return top + Vector3.up * StateIndicatorHeadOffset;
            }

            return transform.position + Vector3.up * StateIndicatorHeadOffset;
        }

        private Quaternion GetStateIndicatorRotation(Vector3 indicatorPosition)
        {
            Camera mainCamera = Camera.main;

            if (mainCamera == null)
            {
                return Quaternion.identity;
            }

            Vector3 toCamera = mainCamera.transform.position - indicatorPosition;

            if (toCamera.sqrMagnitude <= 0.0001f)
            {
                return Quaternion.identity;
            }

            return Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }

        private bool TryGetEnemyBounds(out Bounds bounds)
        {
            for (int i = 0; i < _boundsRenderers.Count; i++)
            {
                Renderer currentRenderer = _boundsRenderers[i];
                if (!IsEnemyBoundsRenderer(currentRenderer))
                {
                    continue;
                }

                bounds = currentRenderer.bounds;

                for (int j = i + 1; j < _boundsRenderers.Count; j++)
                {
                    Renderer nextRenderer = _boundsRenderers[j];
                    if (IsEnemyBoundsRenderer(nextRenderer))
                    {
                        bounds.Encapsulate(nextRenderer.bounds);
                    }
                }

                return true;
            }

            for (int i = 0; i < _boundsColliders.Count; i++)
            {
                Collider currentCollider = _boundsColliders[i];
                if (!IsEnemyBoundsCollider(currentCollider))
                {
                    continue;
                }

                bounds = currentCollider.bounds;

                for (int j = i + 1; j < _boundsColliders.Count; j++)
                {
                    Collider nextCollider = _boundsColliders[j];
                    if (IsEnemyBoundsCollider(nextCollider))
                    {
                        bounds.Encapsulate(nextCollider.bounds);
                    }
                }

                return true;
            }

            bounds = default;
            return false;
        }

        private void CacheEnemyBoundsComponents()
        {
            _boundsRenderers.Clear();
            _boundsColliders.Clear();
            GetComponentsInChildren(false, _boundsRenderers);
            GetComponentsInChildren(false, _boundsColliders);
        }

        private bool IsEnemyBoundsRenderer(Renderer candidate)
        {
            return candidate != null
                && candidate.enabled
                && (_stateIndicatorInstance == null || !candidate.transform.IsChildOf(_stateIndicatorInstance.transform));
        }

        private bool IsEnemyBoundsCollider(Collider candidate)
        {
            return candidate != null
                && candidate.enabled
                && (_stateIndicatorInstance == null || !candidate.transform.IsChildOf(_stateIndicatorInstance.transform));
        }

        private Material CreateStateIndicatorMaterial(Material sourceMaterial)
        {
            Material material = sourceMaterial != null ? new Material(sourceMaterial) : CreateDefaultStateIndicatorMaterial();
            ConfigureStateIndicatorMaterialDepth(material);
            return material;
        }

        private Material CreateDefaultStateIndicatorMaterial()
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
                Debug.LogWarning("EnemyStateMachine could not find a shader for the state indicator.", this);
                return null;
            }

            return new Material(shader);
        }

        private void ConfigureStateIndicatorMaterialDepth(Material material)
        {
            if (material == null)
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

        private void SetStateIndicatorColor(Color color)
        {
            if (_stateIndicatorMaterial == null)
            {
                return;
            }

            if (_stateIndicatorMaterial.HasProperty("_BaseColor"))
            {
                _stateIndicatorMaterial.SetColor("_BaseColor", color);
            }
            else if (_stateIndicatorMaterial.HasProperty("_Color"))
            {
                _stateIndicatorMaterial.SetColor("_Color", color);
            }
        }

        private void SetStateIndicatorVisible(bool visible)
        {
            if (_stateIndicatorInstance != null && _stateIndicatorInstance.activeSelf != visible)
            {
                _stateIndicatorInstance.SetActive(visible);
            }
        }
    }
}
