using System;
using EndLink.Combat;
using UnityEngine;
using UnityEngine.Events;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人的平衡值组件。
    /// 平衡值受到动作的 Balance Damage 削减，归零后进入失衡并开放处决资格。
    /// 它不负责决定普通受击硬直；单次命中是否触发 Hit 由敌人韧性和动作 Hit Strength 决定。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyBalance : MonoBehaviour
    {
        private const float BalanceEpsilon = 0.001f;

        [Header("平衡")]
        [Tooltip("敌人的最大平衡值。动作造成的 Balance Damage 会从当前平衡值中扣除。")]
        [SerializeField, Min(1f)]
        private float maxBalance = 100f;

        [Tooltip("最后一次受到平衡伤害后，等待多久才开始恢复。")]
        [SerializeField, Min(0f)]
        private float recoveryDelay = 2f;

        [Tooltip("未失衡时每秒恢复的平衡值。设为 0 表示不会自动恢复。")]
        [SerializeField, Min(0f)]
        private float recoveryPerSecond = 15f;

        [Header("事件")]
        [Tooltip("当前平衡值发生变化时触发。参数依次为当前值和最大值，可供后续敌人平衡条使用。")]
        [SerializeField]
        private EnemyBalanceChangedEvent onBalanceChanged = new();

        [Tooltip("平衡值首次降到 0 并进入失衡时触发。")]
        [SerializeField]
        private UnityEvent onStaggered = new();

        [Tooltip("失衡结束并恢复平衡值时触发。")]
        [SerializeField]
        private UnityEvent onRecovered = new();

        private EnemyHealth _health;
        private float _currentBalance;
        private float _recoveryAllowedTime;
        private bool _isStaggered;

        /// <summary>最大平衡值。</summary>
        public float MaxBalance => Mathf.Max(1f, maxBalance);

        /// <summary>当前平衡值。</summary>
        public float CurrentBalance => _currentBalance;

        /// <summary>当前平衡值的 0 到 1 比例。</summary>
        public float NormalizedBalance => MaxBalance > 0f
            ? Mathf.Clamp01(_currentBalance / MaxBalance)
            : 0f;

        /// <summary>当前是否处于失衡阶段。</summary>
        public bool IsStaggered => _isStaggered;

        /// <summary>
        /// 当前是否允许进入处决流程。
        /// 第一版只提供资格，不在这里实现处决输入、动画或伤害。
        /// </summary>
        public bool CanBeExecuted => _isStaggered && (_health == null || _health.IsAlive);

        /// <summary>供运行时 UI 或调试工具监听的平衡值变化事件。</summary>
        public event Action<float, float> BalanceChanged;

        /// <summary>进入失衡时触发，并携带导致本次失衡的攻击来源。</summary>
        public event Action<GameObject> StaggerStarted;

        /// <summary>处决资格发生变化时触发。</summary>
        public event Action<bool> ExecutionAvailabilityChanged;

        /// <summary>Inspector 可配置的平衡值变化事件。</summary>
        public EnemyBalanceChangedEvent OnBalanceChanged => onBalanceChanged;

        /// <summary>Inspector 可配置的进入失衡事件。</summary>
        public UnityEvent OnStaggered => onStaggered;

        /// <summary>Inspector 可配置的失衡恢复事件。</summary>
        public UnityEvent OnRecovered => onRecovered;

        private void Awake()
        {
            _health = GetComponent<EnemyHealth>();
            ResetBalance();
        }

        private void OnEnable()
        {
            _health ??= GetComponent<EnemyHealth>();

            if (_health != null)
            {
                _health.DamagedDetailed -= HandleDamaged;
                _health.DamagedDetailed += HandleDamaged;
                _health.OnDead.RemoveListener(HandleDead);
                _health.OnDead.AddListener(HandleDead);
                _health.ResetPerformed -= HandleHealthReset;
                _health.ResetPerformed += HandleHealthReset;
            }

            if (_health == null || _health.IsAlive)
            {
                ResetBalance();
            }
        }

        private void OnDisable()
        {
            if (_health == null)
            {
                return;
            }

            _health.DamagedDetailed -= HandleDamaged;
            _health.OnDead.RemoveListener(HandleDead);
            _health.ResetPerformed -= HandleHealthReset;
        }

        private void OnValidate()
        {
            maxBalance = Mathf.Max(1f, maxBalance);
            recoveryDelay = Mathf.Max(0f, recoveryDelay);
            recoveryPerSecond = Mathf.Max(0f, recoveryPerSecond);
        }

        private void Update()
        {
            if (_isStaggered
                || recoveryPerSecond <= 0f
                || _currentBalance >= MaxBalance
                || Time.time < _recoveryAllowedTime
                || (_health != null && _health.IsDead))
            {
                return;
            }

            SetCurrentBalance(_currentBalance + recoveryPerSecond * Time.deltaTime);
        }

        /// <summary>
        /// 对当前敌人施加平衡伤害。
        /// 已死亡或已经失衡时不会重复削减，也不会刷新失衡持续时间。
        /// </summary>
        public bool ApplyBalanceDamage(float amount, GameObject source)
        {
            amount = Mathf.Max(0f, amount);
            if (amount <= 0f
                || _isStaggered
                || (_health != null && (!_health.IsAlive || _health.CurrentHealth <= 0)))
            {
                return false;
            }

            _recoveryAllowedTime = Time.time + recoveryDelay;
            SetCurrentBalance(_currentBalance - amount);

            if (_currentBalance <= BalanceEpsilon)
            {
                BeginStagger(source);
            }

            return true;
        }

        /// <summary>
        /// 强制把平衡值降到 0 并进入失衡。
        /// 供后续弹反、特殊技能或脚本化演出使用。
        /// </summary>
        public bool ForceStagger(GameObject source = null)
        {
            if (_isStaggered
                || (_health != null && (!_health.IsAlive || _health.CurrentHealth <= 0)))
            {
                return false;
            }

            SetCurrentBalance(0f);
            BeginStagger(source);
            return true;
        }

        /// <summary>
        /// 结束失衡并恢复满平衡。
        /// 第一版在 Stagger 状态自然结束时调用，后续处决流程也可以复用。
        /// </summary>
        public void RecoverFromStagger()
        {
            bool wasStaggered = _isStaggered;
            _isStaggered = false;
            _recoveryAllowedTime = Time.time + recoveryDelay;
            SetCurrentBalance(MaxBalance);

            if (!wasStaggered)
            {
                return;
            }

            ExecutionAvailabilityChanged?.Invoke(false);
            onRecovered.Invoke();
        }

        /// <summary>恢复为满平衡并清除失衡资格，用于敌人重置和对象池复用。</summary>
        public void ResetBalance()
        {
            bool executionWasAvailable = CanBeExecuted;
            _isStaggered = false;
            _recoveryAllowedTime = 0f;
            SetCurrentBalance(MaxBalance);

            if (executionWasAvailable)
            {
                ExecutionAvailabilityChanged?.Invoke(false);
            }
        }

        private void HandleDamaged(DamageResult damageResult)
        {
            if (_health != null && _health.CurrentHealth <= 0)
            {
                return;
            }

            CombatActionDefinition actionDefinition = damageResult.Context.ActionDefinition;
            if (actionDefinition != null)
            {
                ApplyBalanceDamage(actionDefinition.BalanceDamage, damageResult.Source);
            }
        }

        private void HandleDead()
        {
            if (!_isStaggered)
            {
                return;
            }

            _isStaggered = false;
            ExecutionAvailabilityChanged?.Invoke(false);
        }

        private void HandleHealthReset()
        {
            ResetBalance();
        }

        private void BeginStagger(GameObject source)
        {
            if (_isStaggered)
            {
                return;
            }

            _isStaggered = true;
            _currentBalance = 0f;
            onStaggered.Invoke();
            StaggerStarted?.Invoke(source);
            ExecutionAvailabilityChanged?.Invoke(true);
        }

        private void SetCurrentBalance(float value)
        {
            float nextBalance = Mathf.Clamp(value, 0f, MaxBalance);
            if (Mathf.Approximately(_currentBalance, nextBalance))
            {
                return;
            }

            _currentBalance = nextBalance;
            RaiseBalanceChanged();
        }

        private void RaiseBalanceChanged()
        {
            onBalanceChanged.Invoke(_currentBalance, MaxBalance);
            BalanceChanged?.Invoke(_currentBalance, MaxBalance);
        }
    }

    /// <summary>敌人平衡值变化事件，参数依次为当前值和最大值。</summary>
    [Serializable]
    public sealed class EnemyBalanceChangedEvent : UnityEvent<float, float>
    {
    }
}
