using EndLink.Combat;
using UnityEngine;
using UnityEngine.Events;

namespace EndLink.Ally
{
    /// <summary>
    /// 队友生命桥接组件。
    /// CharacterHealth 负责真正的血量、受击和死亡；本组件只把结果接到 AllyStateMachine。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AllyStateMachine))]
    [RequireComponent(typeof(CharacterHealth))]
    public sealed class AllyHealth : MonoBehaviour
    {
        [Header("状态机接线")]
        [Tooltip("受到有效伤害且未死亡时，是否请求进入 Hit 状态。")]
        [SerializeField]
        private bool requestHitStateOnDamage = true;

        [Tooltip("生命值首次降到 0 时，是否请求进入 Dead 状态。")]
        [SerializeField]
        private bool requestDeadStateOnDeath = true;

        [Header("事件")]
        [Tooltip("生命值变化时触发。参数依次为：当前生命值、最大生命值。")]
        [SerializeField]
        private AllyHealthChangedEvent onHealthChanged = new();

        [Tooltip("受到有效伤害时触发。参数依次为：实际伤害值、命中标签。")]
        [SerializeField]
        private AllyHealthDamagedEvent onDamaged = new();

        [Tooltip("生命值首次降到 0 时触发。")]
        [SerializeField]
        private UnityEvent onDead = new();

        private CharacterHealth _health;
        private AllyStateMachine _stateMachine;

        /// <summary>队友使用的通用生命组件。</summary>
        public CharacterHealth Health => _health;

        /// <summary>最大生命值。</summary>
        public int MaxHealth => _health != null ? _health.MaxHealth : 0;

        /// <summary>当前生命值。</summary>
        public int CurrentHealth => _health != null ? _health.CurrentHealth : 0;

        /// <summary>是否已经死亡。</summary>
        public bool IsDead => _health != null && _health.IsDead;

        /// <summary>生命值变化事件。</summary>
        public AllyHealthChangedEvent OnHealthChanged => onHealthChanged;

        /// <summary>受击事件。</summary>
        public AllyHealthDamagedEvent OnDamaged => onDamaged;

        /// <summary>死亡事件。</summary>
        public UnityEvent OnDead => onDead;

        private void Awake()
        {
            _stateMachine = GetComponent<AllyStateMachine>();
            _health = GetOrAddHealth();
        }

        private void OnEnable()
        {
            _health = GetOrAddHealth();
            _health.HealthChanged += HandleHealthChanged;
            _health.Damaged += HandleDamaged;
            _health.Died += HandleDied;
        }

        private void OnDisable()
        {
            if (_health == null)
            {
                return;
            }

            _health.HealthChanged -= HandleHealthChanged;
            _health.Damaged -= HandleDamaged;
            _health.Died -= HandleDied;
        }

        /// <summary>直接对队友施加伤害。主要用于调试或敌人攻击接线前的临时测试。</summary>
        public void TakeDamage(int damage, CombatTagDefinition tag)
        {
            _health.ApplyDamage(damage, tag, null);
        }

        /// <summary>治疗队友。</summary>
        public int Heal(int amount)
        {
            return _health.Heal(amount);
        }

        /// <summary>重置队友生命值。</summary>
        public void ResetHealth()
        {
            _health.ResetHealth();
        }

        private CharacterHealth GetOrAddHealth()
        {
            CharacterHealth health = GetComponent<CharacterHealth>();
            if (health == null)
            {
                health = gameObject.AddComponent<CharacterHealth>();
            }

            return health;
        }

        private void HandleHealthChanged(CharacterHealthChangeInfo changeInfo)
        {
            onHealthChanged.Invoke(changeInfo.CurrentHealth, changeInfo.MaxHealth);
        }

        private void HandleDamaged(CharacterHealthDamageInfo damageInfo)
        {
            onDamaged.Invoke(damageInfo.Damage, damageInfo.Tag);

            if (requestHitStateOnDamage && damageInfo.Health.CurrentHealth > 0)
            {
                _stateMachine.RequestHit();
            }
        }

        private void HandleDied(CharacterHealthDeathInfo deathInfo)
        {
            onDead.Invoke();

            if (requestDeadStateOnDeath)
            {
                _stateMachine.RequestDead();
            }
        }
    }

    /// <summary>队友生命值变化事件。参数依次为：当前生命值、最大生命值。</summary>
    [System.Serializable]
    public sealed class AllyHealthChangedEvent : UnityEvent<int, int>
    {
    }

    /// <summary>队友受击事件。参数依次为：实际伤害值、命中战斗标签。</summary>
    [System.Serializable]
    public sealed class AllyHealthDamagedEvent : UnityEvent<int, CombatTagDefinition>
    {
    }
}
