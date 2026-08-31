using System.Collections.Generic;
using EndLink.Combat;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人有限状态机。
    /// 当前管理 Idle、Alert、Combat、Hit、Stagger、Return、Dead 这些大状态；
    /// Combat 内部先由轻量战斗行为状态机处理基础循环，后续可替换或扩展为行为树。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyActor))]
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(EnemyBalance))]
    public sealed class EnemyStateMachine : MonoBehaviour, ICombatParryReceiver
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

        [Header("受击与韧性")]
        [Tooltip("受击硬直的基础持续时间。只有动作 Hit Strength 达到敌人 Poise 时才会进入 Hit。")]
        [SerializeField, Min(0.01f)]
        private float hitDuration = 0.25f;

        [Tooltip("受到有效伤害时，是否把当前战斗目标切换为伤害来源。")]
        [SerializeField]
        private bool retargetOnDamage = true;

        [Tooltip("敌人的隐性韧性。动作 Hit Strength 达到该值时才触发 Hit；Poise 不会被消耗，也不等同于平衡值。")]
        [SerializeField, Min(0f)]
        private float poise = 1f;

        [Tooltip("两次 Hit 状态触发之间的最短间隔，避免多段 Hitbox 在极短时间内反复打断敌人。")]
        [SerializeField, Min(0f)]
        private float hitReactCooldown = 0.12f;

        [Header("失衡状态")]
        [Tooltip("平衡值归零后保持 Stagger 失衡的时间。第一版中这段时间同时作为处决资格窗口。")]
        [SerializeField, Min(0.01f)]
        private float staggerDuration = 3f;

        [Header("Combat 移动与攻击")]
        [Tooltip("基础敌人追击目标时，和目标表面之间保留的很近间隔。实际中心停止距离会自动加上敌人和目标的碰撞半径。")]
        [SerializeField, Min(0f)]
        private float combatChaseStopDistance = 0.2f;

        [Tooltip("Combat 内部定位阶段的退出距离容差。目标离开 Effective Attack Range + 该值后，敌人才重新进入 Approach，避免临界距离反复切换。")]
        [SerializeField, Min(0f)]
        private float combatAttackRangeTolerance = 0.15f;

        [Tooltip("Combat 内部接近攻击目标时的内缩距离。敌人会尝试比动作极限攻击距离更近一点，避免卡在刚好够不到的位置。")]
        [SerializeField, Min(0f)]
        private float combatAttackInnerOffset = 0.1f;

        [Tooltip("释放一次技能前需要完整执行的普攻次数。大于 0 时优先使用固定计数规则；设为 0 时关闭计数并使用技能概率。")]
        [SerializeField, Min(0)]
        private int combatBasicAttacksBeforeSkill;

        [Tooltip("未启用固定普攻计数时，每轮攻击同时满足普攻和技能可用时选择技能的概率。技能未配置或仍在冷却时会自动使用普攻。")]
        [SerializeField, Range(0f, 1f)]
        private float combatSkillChance = 0.35f;

        [Header("Combat 远程行为")]
        [Tooltip("远程敌人允许目标接近的最小表面距离。低于该值时，Engage 阶段会先后撤再尝试攻击。")]
        [SerializeField, Min(0f)]
        private float rangedMinimumDistance = 3.5f;

        [Tooltip("远程敌人希望保持的目标表面距离。运行时会限制在最小距离和当前 Action 的 Effective Attack Range 之间。")]
        [SerializeField, Min(0.01f)]
        private float rangedPreferredDistance = 6f;

        [Tooltip("远程攻击前是否要求发射点到目标锁定点之间没有环境遮挡。")]
        [SerializeField]
        private bool rangedRequireLineOfSight = true;

        [Tooltip("远程视线检测视为遮挡物的 Layer。建议包含 Default、Environment 和 Interactable，不要包含 Player 或 Enemy。")]
        [SerializeField]
        private LayerMask rangedObstructionLayers;

        [Tooltip("远程敌人视线受阻或攻击结束后，单次侧向重新选位的移动距离。")]
        [SerializeField, Min(0.1f)]
        private float rangedRepositionDistance = 2f;

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

        [Header("状态提示")]
        [Tooltip("头顶状态点使用的基础材质。正式构建建议显式指定，避免 Primitive 默认 Shader 被剥离。")]
        [SerializeField]
        private Material stateIndicatorMaterialSource;

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
        private EnemyBalance _balance;
        private Transform _currentTarget;
        private GameObject _stateIndicatorInstance;
        private Material _stateIndicatorMaterial;
        private Renderer _stateIndicatorRenderer;
        private bool _alertTransitionExternallyControlled;
        private EnemyStateId _alertFallbackState = EnemyStateId.Idle;
        private float _nextHitReactTime;
        private Vector3 _capturedHomePosition;
        private bool _hasCapturedHome;
        private EnemyCombatCoordinator _combatCoordinator;
        private float _combatCoordinatorDistanceSqr = float.PositiveInfinity;
        private int _combatCoordinatorPriority = int.MinValue;

        /// <summary>当前状态标识，方便 Inspector 和调试工具观察。</summary>
        public EnemyStateId CurrentStateId => _currentState?.StateId ?? EnemyStateId.None;

        /// <summary>
        /// 当前 Combat 内部行为阶段。敌人不在 Combat 大状态时返回 Approach，
        /// 调用方应同时检查 CurrentStateId，避免把非战斗移动误判为战斗机动。
        /// </summary>
        public EnemyCombatPhase CurrentCombatPhase => _currentState is EnemyCombatState combatState
            ? combatState.CurrentPhase
            : EnemyCombatPhase.Approach;

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

        /// <summary>敌人的隐性韧性阈值。</summary>
        public float Poise => Mathf.Max(0f, poise);

        /// <summary>平衡归零后的失衡持续时间。</summary>
        public float StaggerDuration => staggerDuration;

        /// <summary>Combat 状态追击目标时的停止距离。</summary>
        public float CombatChaseStopDistance => combatChaseStopDistance;

        /// <summary>Combat 定位阶段退出攻击范围时使用的距离容差。</summary>
        public float CombatAttackRangeTolerance => combatAttackRangeTolerance;

        /// <summary>Combat 状态接近攻击目标时，相对动作极限距离向内靠近的距离。</summary>
        public float CombatAttackInnerOffset => combatAttackInnerOffset;

        /// <summary>释放一次技能前需要完整执行的普攻次数。0 表示改用概率规则。</summary>
        public int CombatBasicAttacksBeforeSkill => Mathf.Max(0, combatBasicAttacksBeforeSkill);

        /// <summary>未启用固定计数时，每轮攻击同时可选普攻和技能时使用技能的概率。</summary>
        public float CombatSkillChance => Mathf.Clamp01(combatSkillChance);

        /// <summary>远程行为允许目标接近的最小表面距离。</summary>
        public float RangedMinimumDistance => Mathf.Max(0f, rangedMinimumDistance);

        /// <summary>远程行为希望保持的目标表面距离。</summary>
        public float RangedPreferredDistance => Mathf.Max(0.01f, rangedPreferredDistance);

        /// <summary>远程行为是否要求攻击视线畅通。</summary>
        public bool RangedRequireLineOfSight => rangedRequireLineOfSight;

        /// <summary>远程攻击视线检测使用的遮挡 Layer。</summary>
        public LayerMask RangedObstructionLayers => rangedObstructionLayers;

        /// <summary>远程行为单次重新选位距离。</summary>
        public float RangedRepositionDistance => Mathf.Max(0.1f, rangedRepositionDistance);

        /// <summary>当前归属的敌人战斗协调器。为空时仍可按自身行为攻击，但不参与区域围攻限制。</summary>
        public EnemyCombatCoordinator CombatCoordinator => _combatCoordinator;

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
            _balance = GetComponent<EnemyBalance>();
            CaptureHomeIfNeeded();

            EnemyStateContext context = new EnemyStateContext(
                this,
                _actor,
                _health,
                _balance,
                transform);

            RegisterState(new EnemyIdleState(context));
            RegisterState(new EnemyAlertState(context));
            RegisterState(new EnemyCombatState(context));
            RegisterState(new EnemyHitState(context));
            RegisterState(new EnemyStaggerState(context));
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

            if (_balance == null)
            {
                _balance = GetComponent<EnemyBalance>();
            }

            _health.DamagedDetailed += HandleDamaged;
            _health.OnDead.AddListener(HandleDead);
            _health.ResetPerformed += HandleHealthReset;

            if (_balance != null)
            {
                _balance.StaggerStarted -= HandleStaggerStarted;
                _balance.StaggerStarted += HandleStaggerStarted;
            }

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
            if (_health != null)
            {
                _health.DamagedDetailed -= HandleDamaged;
                _health.OnDead.RemoveListener(HandleDead);
                _health.ResetPerformed -= HandleHealthReset;
            }

            if (_balance != null)
            {
                _balance.StaggerStarted -= HandleStaggerStarted;
            }

            _currentState?.Exit();
            _currentState = null;
            _currentTarget = null;
            _actor?.CombatDriver?.CancelCurrentAction();
            ClearCombatCoordinatorRuntime();
            SetStateIndicatorVisible(false);
        }

        private void OnValidate()
        {
            alertDuration = Mathf.Max(0.01f, alertDuration);
            detectionRadius = Mathf.Max(0.1f, detectionRadius);
            requiredAlertTime = Mathf.Max(0.01f, requiredAlertTime);
            hitDuration = Mathf.Max(0.01f, hitDuration);
            poise = Mathf.Max(0f, poise);
            hitReactCooldown = Mathf.Max(0f, hitReactCooldown);
            staggerDuration = Mathf.Max(0.01f, staggerDuration);
            combatChaseStopDistance = Mathf.Max(0f, combatChaseStopDistance);
            combatAttackRangeTolerance = Mathf.Max(0f, combatAttackRangeTolerance);
            combatAttackInnerOffset = Mathf.Max(0f, combatAttackInnerOffset);
            rangedMinimumDistance = Mathf.Max(0f, rangedMinimumDistance);
            rangedPreferredDistance = Mathf.Max(0.01f, rangedPreferredDistance);
            rangedRepositionDistance = Mathf.Max(0.1f, rangedRepositionDistance);
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
        /// 尝试把敌人归属到指定战斗协调器。
        /// 敌人正在 Combat / Hit / Stagger 且已有协调器时不会切换，避免战斗中围攻规则跳变。
        /// </summary>
        public bool TryAssignCombatCoordinator(
            EnemyCombatCoordinator coordinator,
            float distanceSqr,
            int coordinatorPriority)
        {
            if (coordinator == null || !CanAcceptCombatCoordinator(coordinator, distanceSqr, coordinatorPriority))
            {
                return false;
            }

            if (_combatCoordinator != null && _combatCoordinator != coordinator)
            {
                _combatCoordinator.UnregisterEnemy(this);
            }

            _combatCoordinator = coordinator;
            _combatCoordinatorDistanceSqr = Mathf.Max(0f, distanceSqr);
            _combatCoordinatorPriority = coordinatorPriority;
            return true;
        }

        /// <summary>
        /// 尝试清除当前战斗协调器。
        /// 非强制模式下，Combat / Hit / Stagger 中的敌人会保留当前协调器直到脱战。
        /// </summary>
        public bool TryClearCombatCoordinator(EnemyCombatCoordinator coordinator, bool force)
        {
            if (_combatCoordinator == null || _combatCoordinator != coordinator)
            {
                return false;
            }

            if (!force && IsCombatOwnedState(CurrentStateId))
            {
                return false;
            }

            ClearCombatCoordinatorRuntime();
            return true;
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
            ResetCombatActionPattern();
            _currentState?.Exit();
            _currentState = null;
            _currentTarget = null;
            _alertFallbackState = EnemyStateId.Idle;
            _nextHitReactTime = 0f;
            _balance?.ResetBalance();
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
            if (!CanAcceptStandardRequest())
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
        /// Combat 内部负责基础接近、定位、攻击和恢复，复杂决策后续由行为树接管。
        /// </summary>
        public bool RequestCombat(Transform target = null)
        {
            if (!CanAcceptStandardRequest())
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
            if (!CanAcceptStandardRequest())
            {
                return false;
            }

            ChangeState(EnemyStateId.Hit);
            return CurrentStateId == EnemyStateId.Hit;
        }

        /// <summary>
        /// 请求进入 Stagger 失衡状态。
        /// 会先确保平衡组件进入失衡并开放处决资格，再切换敌人大状态。
        /// </summary>
        public bool RequestStagger(GameObject source = null)
        {
            if (!CanAcceptNonDeadRequest())
            {
                return false;
            }

            _balance?.ForceStagger(source);
            ChangeState(EnemyStateId.Stagger);
            return CurrentStateId == EnemyStateId.Stagger;
        }

        /// <summary>
        /// 接收玩家成功弹反结果。
        /// 第一版仍触发普通 Hit；后续弹反规则可以通过平衡伤害或 RequestStagger 单独进入失衡。
        /// </summary>
        public void ReceiveParry(GameObject parrySource)
        {
            RequestHit();
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
            if (!CanAcceptStandardRequest())
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

            if (nextStateId == EnemyStateId.Idle
                || nextStateId == EnemyStateId.Alert
                || nextStateId == EnemyStateId.Return
                || nextStateId == EnemyStateId.Dead)
            {
                ResetCombatActionPattern();
            }

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

        private void ResetCombatActionPattern()
        {
            if (_states.TryGetValue(EnemyStateId.Combat, out IEnemyState combatState)
                && combatState is EnemyCombatState enemyCombatState)
            {
                enemyCombatState.ResetActionPattern();
            }
        }

        private void HandleDamaged(DamageResult damageResult)
        {
            if (_health != null && _health.CurrentHealth <= 0)
            {
                return;
            }

            bool acquiredTarget = TryRetargetFromDamageSource();

            if (CurrentStateId == EnemyStateId.Stagger)
            {
                return;
            }

            CombatActionDefinition actionDefinition = damageResult.Context.ActionDefinition;
            float hitStrength = actionDefinition != null ? actionDefinition.HitStrength : 0f;
            float currentTime = Time.time;

            if (ShouldTriggerHitReaction(
                    hitStrength,
                    poise,
                    currentTime,
                    _nextHitReactTime))
            {
                _nextHitReactTime = currentTime + hitReactCooldown;
                RequestHit();
                return;
            }

            if (acquiredTarget
                && CurrentStateId != EnemyStateId.Combat
                && CurrentStateId != EnemyStateId.Hit
                && CurrentStateId != EnemyStateId.Stagger)
            {
                RequestCombat(_currentTarget);
            }
        }

        private void HandleStaggerStarted(GameObject source)
        {
            if (TryResolveDamageSourceTarget(source, out Transform damageSourceTarget))
            {
                SetTarget(damageSourceTarget);
            }

            if (CanAcceptNonDeadRequest())
            {
                ChangeState(EnemyStateId.Stagger);
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

        private bool CanAcceptStandardRequest()
        {
            return CanAcceptNonDeadRequest() && CurrentStateId != EnemyStateId.Stagger;
        }

        private bool CanAcceptCombatCoordinator(
            EnemyCombatCoordinator candidate,
            float distanceSqr,
            int coordinatorPriority)
        {
            if (CurrentStateId == EnemyStateId.Dead)
            {
                return false;
            }

            if (_combatCoordinator == candidate)
            {
                return true;
            }

            if (IsCombatOwnedState(CurrentStateId)
                && _combatCoordinator != null)
            {
                return false;
            }

            if (_combatCoordinator == null)
            {
                return true;
            }

            if (coordinatorPriority != _combatCoordinatorPriority)
            {
                return coordinatorPriority > _combatCoordinatorPriority;
            }

            return distanceSqr < _combatCoordinatorDistanceSqr;
        }

        private void ClearCombatCoordinatorRuntime()
        {
            EnemyCombatCoordinator previousCoordinator = _combatCoordinator;
            _combatCoordinator = null;
            _combatCoordinatorDistanceSqr = float.PositiveInfinity;
            _combatCoordinatorPriority = int.MinValue;

            if (previousCoordinator != null)
            {
                previousCoordinator.UnregisterEnemy(this);
            }
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
            float hitStrength,
            float poise,
            float currentTime,
            float nextAllowedTime)
        {
            if (hitStrength <= 0f || currentTime < nextAllowedTime)
            {
                return false;
            }

            return poise <= 0f || hitStrength >= poise;
        }

        private static bool IsCombatOwnedState(EnemyStateId stateId)
        {
            return stateId == EnemyStateId.Combat
                || stateId == EnemyStateId.Hit
                || stateId == EnemyStateId.Stagger;
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

            if (rangedObstructionLayers.value == 0)
            {
                rangedObstructionLayers = LayerMask.GetMask("Default", "Environment", "Interactable");
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

            Material sourceMaterial = stateIndicatorMaterialSource != null
                ? stateIndicatorMaterialSource
                : _stateIndicatorRenderer.sharedMaterial;
            _stateIndicatorMaterial = CreateStateIndicatorMaterial(sourceMaterial);
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
