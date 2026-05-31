using System.Collections.Generic;
using EndLink.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人有限状态机。
    /// 当前只管理 Idle、Alert、Combat、Hit、Dead 这些大状态；
    /// 后续更细的追击、攻击、技能和撤退行为会放进 Combat 内部的行为树。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyActor))]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyStateMachine : MonoBehaviour
    {
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

        [Header("受击状态")]
        [Tooltip("受击硬直的基础持续时间。后续可由攻击数据、霸体或韧性系统覆盖。")]
        [SerializeField, Min(0.01f)]
        private float hitDuration = 0.25f;

        [Header("Combat 移动")]
        [Tooltip("基础敌人追击目标时，和目标表面之间保留的很近间隔。实际中心停止距离会自动加上敌人和目标的碰撞半径。")]
        [SerializeField, Min(0f)]
        private float combatChaseStopDistance = 0.2f;

        [Tooltip("目标离敌人超过该距离时脱战并回到 Idle。小于等于 0 表示不按距离脱战。")]
        [SerializeField, Min(0f)]
        private float combatLeashDistance = 18f;

        [Header("调试")]
        [Tooltip("是否打印敌人大状态切换日志。排查受击、进战和死亡流程时开启。")]
        [SerializeField]
        private bool logStateChanges;

        private readonly Dictionary<EnemyStateId, IEnemyState> _states = new();
        private IEnemyState _currentState;
        private EnemyActor _actor;
        private EnemyHealth _health;
        private Transform _currentTarget;
        private GameObject _stateIndicatorInstance;
        private Material _stateIndicatorMaterial;
        private Renderer _stateIndicatorRenderer;
        private bool _alertTransitionExternallyControlled;

        /// <summary>当前状态标识，方便 Inspector 和调试工具观察。</summary>
        public EnemyStateId CurrentStateId => _currentState?.StateId ?? EnemyStateId.None;

        /// <summary>当前敌人关注或战斗的目标。</summary>
        public Transform CurrentTarget => _currentTarget;

        /// <summary>当前目标是否仍然有效。</summary>
        public bool HasValidTarget => IsTargetValid(_currentTarget);

        /// <summary>警觉状态持续时间。</summary>
        public float AlertDuration => alertDuration;

        /// <summary>受击硬直持续时间。</summary>
        public float HitDuration => hitDuration;

        /// <summary>Combat 状态追击目标时的停止距离。</summary>
        public float CombatChaseStopDistance => combatChaseStopDistance;

        /// <summary>Combat 状态目标超过该距离时脱战。小于等于 0 表示不按距离脱战。</summary>
        public float CombatLeashDistance => combatLeashDistance;

        /// <summary>Alert 到 Combat / Idle 的转换是否由外部索敌组件控制。</summary>
        public bool AlertTransitionExternallyControlled => _alertTransitionExternallyControlled;

        private void Awake()
        {
            _actor = GetComponent<EnemyActor>();
            _health = GetComponent<EnemyHealth>();

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
        }

        private void OnEnable()
        {
            if (_health == null)
            {
                _health = GetComponent<EnemyHealth>();
            }

            _health.OnDamaged.AddListener(HandleDamaged);
            _health.OnDead.AddListener(HandleDead);
        }

        private void Start()
        {
            ChangeState(_health != null && _health.IsDead ? EnemyStateId.Dead : initialState);
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
        }

        private void OnValidate()
        {
            alertDuration = Mathf.Max(0.01f, alertDuration);
            hitDuration = Mathf.Max(0.01f, hitDuration);
            combatChaseStopDistance = Mathf.Max(0f, combatChaseStopDistance);
            combatLeashDistance = Mathf.Max(0f, combatLeashDistance);
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
        /// 请求进入 Alert 状态。
        /// 可选目标不为空时会先更新当前目标。
        /// </summary>
        public bool RequestAlert(Transform target = null)
        {
            if (!CanAcceptNonDeadRequest())
            {
                return false;
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
        /// 第一版 Combat 不执行具体行为，后续由行为树接管内部细节。
        /// </summary>
        public bool RequestCombat(Transform target = null)
        {
            if (!CanAcceptNonDeadRequest())
            {
                return false;
            }

            if (target != null)
            {
                SetTarget(target);
            }

            ChangeState(EnemyStateId.Combat);
            return CurrentStateId == EnemyStateId.Combat;
        }

        /// <summary>
        /// 请求进入 Hit 状态。
        /// 受击可以打断 Idle、Alert 和 Combat，但不能覆盖 Dead。
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

            RequestHit();
        }

        private void HandleDead()
        {
            RequestDead();
        }

        private bool CanAcceptNonDeadRequest()
        {
            return CurrentStateId != EnemyStateId.Dead && (_health == null || !_health.IsDead);
        }

        private void RegisterState(IEnemyState state)
        {
            _states[state.StateId] = state;
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

        private static string GetTransformName(Transform target)
        {
            return target != null ? target.name : "None";
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
            Renderer[] renderers = GetComponentsInChildren<Renderer>();

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer currentRenderer = renderers[i];
                if (!IsEnemyBoundsRenderer(currentRenderer))
                {
                    continue;
                }

                bounds = currentRenderer.bounds;

                for (int j = i + 1; j < renderers.Length; j++)
                {
                    Renderer nextRenderer = renderers[j];
                    if (IsEnemyBoundsRenderer(nextRenderer))
                    {
                        bounds.Encapsulate(nextRenderer.bounds);
                    }
                }

                return true;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>();

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider currentCollider = colliders[i];
                if (!IsEnemyBoundsCollider(currentCollider))
                {
                    continue;
                }

                bounds = currentCollider.bounds;

                for (int j = i + 1; j < colliders.Length; j++)
                {
                    Collider nextCollider = colliders[j];
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
