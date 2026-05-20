using EndLink.Core;
using UnityEngine;
using UnityEngine.Events;

namespace EndLink.Combat
{
    /// <summary>
    /// 玩家生命值与受击接线组件。
    /// 负责接收伤害、扣血、触发生命值事件，并把受击/死亡结果转发给 PlayerStateMachine。
    /// 它不负责 Debuff、Buff 或复杂状态效果的内部逻辑；那些后续应由独立状态效果系统处理。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStateMachine))]
    public sealed class PlayerHealth : MonoBehaviour, IHitReceiver, IDamageable
    {
        [Header("血量")]
        [Tooltip("玩家最大生命值。")]
        [SerializeField, Min(1)]
        private int maxHealth = 100;

        [Header("状态机接线")]
        [Tooltip("受到有效伤害且未死亡时，是否请求进入 Hit 状态。")]
        [SerializeField]
        private bool requestHitStateOnDamage = true;

        [Tooltip("生命值首次降到 0 时，是否请求进入 Dead 状态。")]
        [SerializeField]
        private bool requestDeadStateOnDeath = true;

        [Header("调试显示")]
        [Tooltip("受到伤害、治疗和死亡时是否打印 Debug.Log。")]
        [SerializeField]
        private bool logHealthChanges = true;

        [Tooltip("是否把当前血量显示到 GameObject 名字上。仅用于白模调试。")]
        [SerializeField]
        private bool showHealthInName;

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

        private PlayerStateMachine _stateMachine;
        private string _originalName;
        private int _currentHealth;
        private bool _isDead;

        /// <summary>最大生命值。</summary>
        public int MaxHealth => maxHealth;

        /// <summary>当前生命值。</summary>
        public int CurrentHealth => _currentHealth;

        /// <summary>是否已经死亡。</summary>
        public bool IsDead => _isDead;

        /// <summary>生命值变化事件。</summary>
        public PlayerHealthChangedEvent OnHealthChanged => onHealthChanged;

        /// <summary>受伤事件。</summary>
        public PlayerHealthDamagedEvent OnDamaged => onDamaged;

        /// <summary>治疗事件。</summary>
        public PlayerHealedEvent OnHealed => onHealed;

        /// <summary>死亡事件。</summary>
        public UnityEvent OnDead => onDead;

        private void Awake()
        {
            _stateMachine = GetComponent<PlayerStateMachine>();
            _originalName = gameObject.name;
            ResetHealth();
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
        }

        /// <summary>
        /// 接收 Hitbox 的完整命中信息，并转发到简单伤害接口。
        /// </summary>
        public void ReceiveHit(HitboxHitInfo hitInfo)
        {
            TakeDamage(Mathf.RoundToInt(hitInfo.DamageAmount), hitInfo.CombatTagToApply);
        }

        /// <summary>
        /// 接收伤害并同步玩家状态机。
        /// 有效伤害会扣除生命值；生命值归零进入 Dead，否则进入 Hit。
        /// </summary>
        public void TakeDamage(int damage, CombatTagDefinition tag)
        {
            if (_isDead)
            {
                return;
            }

            int appliedDamage = Mathf.Max(0, damage);

            if (appliedDamage <= 0)
            {
                return;
            }

            _currentHealth = Mathf.Max(0, _currentHealth - appliedDamage);

            if (logHealthChanges)
            {
                Debug.Log(
                    $"Player took {appliedDamage} damage, tag: {GetTagLogText(tag)}, hp: {_currentHealth}/{maxHealth}",
                    this);
            }

            onDamaged.Invoke(appliedDamage, tag);
            NotifyHealthChanged();

            if (_currentHealth <= 0)
            {
                Die();
                return;
            }

            if (requestHitStateOnDamage)
            {
                _stateMachine.RequestHit();
            }
        }

        /// <summary>
        /// 恢复生命值。
        /// 当前不处理复活；死亡后治疗会被忽略，后续如需复活应单独做 Revive 流程。
        /// </summary>
        public void Heal(int amount)
        {
            if (_isDead)
            {
                return;
            }

            int appliedHeal = Mathf.Max(0, amount);

            if (appliedHeal <= 0 || _currentHealth >= maxHealth)
            {
                return;
            }

            int previousHealth = _currentHealth;
            _currentHealth = Mathf.Min(maxHealth, _currentHealth + appliedHeal);
            int actualHeal = _currentHealth - previousHealth;

            if (logHealthChanges)
            {
                Debug.Log($"Player healed {actualHeal}, hp: {_currentHealth}/{maxHealth}", this);
            }

            onHealed.Invoke(actualHeal);
            NotifyHealthChanged();
        }

        /// <summary>
        /// 重置玩家生命值和死亡标记。
        /// 主要用于调试、重开战斗或训练场重置。
        /// </summary>
        public void ResetHealth()
        {
            _currentHealth = maxHealth;
            _isDead = false;
            NotifyHealthChanged();
        }

        private void Die()
        {
            if (_isDead)
            {
                return;
            }

            _isDead = true;
            UpdateDebugDisplay();

            if (logHealthChanges)
            {
                Debug.Log("Player died.", this);
            }

            onDead.Invoke();

            if (requestDeadStateOnDeath)
            {
                _stateMachine.RequestDead();
            }
        }

        private void NotifyHealthChanged()
        {
            UpdateDebugDisplay();
            onHealthChanged.Invoke(_currentHealth, maxHealth);
        }

        private void UpdateDebugDisplay()
        {
            if (!showHealthInName || string.IsNullOrEmpty(_originalName))
            {
                return;
            }

            string stateText = _isDead ? "Dead" : $"{_currentHealth}/{maxHealth}";
            gameObject.name = $"{_originalName} [{stateText}]";
        }

        private static string GetTagLogText(CombatTagDefinition tag)
        {
            return tag != null ? tag.TagId : "None";
        }
    }

    /// <summary>
    /// 玩家生命值变化事件。
    /// 参数依次为：当前生命值、最大生命值。
    /// </summary>
    [System.Serializable]
    public sealed class PlayerHealthChangedEvent : UnityEvent<int, int>
    {
    }

    /// <summary>
    /// 玩家受伤事件。
    /// 参数依次为：实际伤害值、命中战斗标签。
    /// </summary>
    [System.Serializable]
    public sealed class PlayerHealthDamagedEvent : UnityEvent<int, CombatTagDefinition>
    {
    }

    /// <summary>
    /// 玩家治疗事件。
    /// 参数为：实际治疗值。
    /// </summary>
    [System.Serializable]
    public sealed class PlayerHealedEvent : UnityEvent<int>
    {
    }
}
