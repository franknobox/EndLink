using System;
using EndLink.Combat;
using UnityEngine;
using UnityEngine.Events;

namespace EndLink.Party
{
    /// <summary>
    /// 小队终链奥义上下文。
    /// 只负责维护全队共享的协同率、奥义是否就绪，以及奥义释放后的消耗事件。
    /// 具体奥义表现、时停、镜头和伤害效果后续由监听者接入。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyUltimateContext : MonoBehaviour
    {
        [Header("协同率")]
        [Tooltip("协同率上限。达到该值后，终链奥义进入可释放状态。")]
        [SerializeField, Min(1f)]
        private float maxSynergyRate = 100f;

        [Tooltip("组件启用或重置时的初始协同率。默认 0。")]
        [SerializeField, Min(0f)]
        private float initialSynergyRate;

        [Tooltip("启用组件时是否把协同率重置为 Initial Synergy Rate。通常在单场战斗测试中保持开启。")]
        [SerializeField]
        private bool resetOnEnable = true;

        [Header("事件")]
        [Tooltip("协同率变化时触发。参数依次为当前协同率和最大协同率。")]
        [SerializeField]
        private PartySynergyChangedEvent onSynergyChanged = new PartySynergyChangedEvent();

        [Tooltip("协同率首次达到上限，终链奥义进入可释放状态时触发。")]
        [SerializeField]
        private UnityEvent onUltimateReady = new UnityEvent();

        [Tooltip("终链奥义被成功释放并消耗协同率时触发。")]
        [SerializeField]
        private UnityEvent onUltimateConsumed = new UnityEvent();

        [Header("调试")]
        [Tooltip("是否打印协同率变化和终链奥义消耗日志。")]
        [SerializeField]
        private bool logChanges;

        private float _currentSynergyRate;
        private bool _wasReady;
        private bool _initialized;

        /// <summary>协同率变化事件。参数依次为当前协同率和最大协同率。</summary>
        public event Action<float, float> SynergyChanged;

        /// <summary>终链奥义首次进入可释放状态。</summary>
        public event Action UltimateReady;

        /// <summary>终链奥义被成功释放并消耗协同率。</summary>
        public event Action UltimateConsumed;

        /// <summary>当前协同率。</summary>
        public float CurrentSynergyRate => _currentSynergyRate;

        /// <summary>协同率上限。</summary>
        public float MaxSynergyRate => maxSynergyRate;

        /// <summary>协同率归一化进度，0 表示空，1 表示终链奥义可释放。</summary>
        public float SynergyNormalized => maxSynergyRate > 0f
            ? Mathf.Clamp01(_currentSynergyRate / maxSynergyRate)
            : 0f;

        /// <summary>终链奥义当前是否满足释放条件。</summary>
        public bool CanUseUltimate => _currentSynergyRate >= maxSynergyRate;

        private void OnEnable()
        {
            if (resetOnEnable || !_initialized)
            {
                _initialized = true;
                SetSynergy(initialSynergyRate, false);
                return;
            }

            NotifySynergyChanged();
        }

        private void OnValidate()
        {
            maxSynergyRate = Mathf.Max(1f, maxSynergyRate);
            initialSynergyRate = Mathf.Clamp(initialSynergyRate, 0f, maxSynergyRate);
        }

        /// <summary>
        /// 增加协同率，并在首次达到上限时广播奥义就绪事件。
        /// 返回本次实际增加的协同率，已经满值时可能返回 0。
        /// </summary>
        public float AddSynergy(float amount)
        {
            if (amount <= 0f)
            {
                return 0f;
            }

            float previousValue = _currentSynergyRate;
            SetSynergy(_currentSynergyRate + amount, true);
            return _currentSynergyRate - previousValue;
        }

        /// <summary>
        /// 从成功释放的连携动作上读取协同率收益。
        /// 只有 LinkAttack 类型的动作会产生协同率，避免普通技能误充能。
        /// </summary>
        public float AddSynergyFromLinkAction(CombatActionDefinition actionDefinition)
        {
            if (actionDefinition == null || actionDefinition.ActionType != CombatActionType.LinkAttack)
            {
                return 0f;
            }

            return AddSynergy(actionDefinition.SynergyGainOnLink);
        }

        /// <summary>
        /// 尝试释放终链奥义。
        /// 第一版只负责消耗就绪状态，不要求目标，也不执行具体表现。
        /// </summary>
        public bool ConsumeUltimate()
        {
            if (!CanUseUltimate)
            {
                return false;
            }

            SetSynergy(0f, false);
            onUltimateConsumed.Invoke();
            UltimateConsumed?.Invoke();
            LogState("ultimate consumed");
            return true;
        }

        /// <summary>把协同率恢复到初始值，主要供调试或战斗开始时重置。</summary>
        public void ResetSynergy()
        {
            SetSynergy(initialSynergyRate, false);
        }

        private void SetSynergy(float value, bool allowReadyEvent)
        {
            bool wasReady = _wasReady;
            _currentSynergyRate = Mathf.Clamp(value, 0f, maxSynergyRate);
            _wasReady = CanUseUltimate;
            NotifySynergyChanged();
            LogState($"synergy={_currentSynergyRate:F1}/{maxSynergyRate:F1}");

            if (allowReadyEvent && _wasReady && !wasReady)
            {
                onUltimateReady.Invoke();
                UltimateReady?.Invoke();
                LogState("ultimate ready");
            }
        }

        private void NotifySynergyChanged()
        {
            onSynergyChanged.Invoke(_currentSynergyRate, maxSynergyRate);
            SynergyChanged?.Invoke(_currentSynergyRate, maxSynergyRate);
        }

        private void LogState(string message)
        {
            if (logChanges)
            {
                Debug.Log($"PartyUltimateContext: {message}", this);
            }
        }
    }

    /// <summary>Inspector 可绑定的协同率变化事件。</summary>
    [Serializable]
    public sealed class PartySynergyChangedEvent : UnityEvent<float, float>
    {
    }
}
