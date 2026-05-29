using EndLink.Core;
using UnityEngine;
using UnityEngine.Events;

namespace EndLink.Combat
{
    /// <summary>
    /// 玩家生命桥接组件。
    /// 生命值本体由 CharacterHealth 处理；本组件只负责把受击/死亡结果转发给 PlayerStateMachine，
    /// 并保留玩家侧事件入口，方便 UI 或调试工具继续监听 PlayerHealth。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStateMachine))]
    [RequireComponent(typeof(CharacterHealth))]
    public sealed class PlayerHealth : MonoBehaviour
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
        private PlayerHealthChangedEvent onHealthChanged = new();

        [Tooltip("受到有效伤害时触发。参数依次为：实际伤害值、命中标签。")]
        [SerializeField]
        private PlayerHealthDamagedEvent onDamaged = new();

        [Tooltip("获得有效治疗时触发。参数为：实际治疗值。")]
        [SerializeField]
        private PlayerHealedEvent onHealed = new();

        [Tooltip("生命值首次降到 0 时触发。")]
        [SerializeField]
        private UnityEvent onDead = new();

        private CharacterHealth _health;
        private PlayerStateMachine _stateMachine;

        /// <summary>玩家使用的通用生命组件。</summary>
        public CharacterHealth Health => _health;

        /// <summary>最大生命值。</summary>
        public int MaxHealth => _health != null ? _health.MaxHealth : 0;

        /// <summary>当前生命值。</summary>
        public int CurrentHealth => _health != null ? _health.CurrentHealth : 0;

        /// <summary>是否已经死亡。</summary>
        public bool IsDead => _health != null && _health.IsDead;

        /// <summary>生命值变化事件。</summary>
        public PlayerHealthChangedEvent OnHealthChanged => onHealthChanged;

        /// <summary>受击事件。</summary>
        public PlayerHealthDamagedEvent OnDamaged => onDamaged;

        /// <summary>治疗事件。</summary>
        public PlayerHealedEvent OnHealed => onHealed;

        /// <summary>死亡事件。</summary>
        public UnityEvent OnDead => onDead;

        private void Awake()
        {
            _stateMachine = GetComponent<PlayerStateMachine>();
            _health = GetOrAddHealth();
        }

        private void OnEnable()
        {
            _health = GetOrAddHealth();
            _health.HealthChanged += HandleHealthChanged;
            _health.Damaged += HandleDamaged;
            _health.Healed += HandleHealed;
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
            _health.Healed -= HandleHealed;
            _health.Died -= HandleDied;
        }

        /// <summary>兼容旧调用：直接对玩家施加伤害。</summary>
        public void TakeDamage(int damage, CombatTagDefinition tag)
        {
            _health.ApplyDamage(damage, tag, null);
        }

        /// <summary>兼容旧调用：治疗玩家。</summary>
        public int Heal(int amount)
        {
            return _health.Heal(amount);
        }

        /// <summary>重置玩家生命值。</summary>
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

        private void HandleHealed(CharacterHealthHealInfo healInfo)
        {
            onHealed.Invoke(healInfo.HealAmount);
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

    /// <summary>玩家生命值变化事件。参数依次为：当前生命值、最大生命值。</summary>
    [System.Serializable]
    public sealed class PlayerHealthChangedEvent : UnityEvent<int, int>
    {
    }

    /// <summary>玩家受击事件。参数依次为：实际伤害值、命中战斗标签。</summary>
    [System.Serializable]
    public sealed class PlayerHealthDamagedEvent : UnityEvent<int, CombatTagDefinition>
    {
    }

    /// <summary>玩家治疗事件。参数为：实际治疗值。</summary>
    [System.Serializable]
    public sealed class PlayerHealedEvent : UnityEvent<int>
    {
    }
}
