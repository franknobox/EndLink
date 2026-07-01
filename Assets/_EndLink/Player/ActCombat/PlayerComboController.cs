using System.Collections.Generic;
using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 玩家普攻连段配置与运行时入口。
    /// 只决定当前是第几段、何时允许缓存下一段，以及每一段使用哪个动作资产；不读取输入，也不生成 Hitbox。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerComboController : MonoBehaviour
    {
        [Header("普攻连段")]
        [Tooltip("按顺序执行的普攻动作。空槽会回退使用 PlayerCombatDriver 的基础普攻，默认三个空槽即三段同动作连段。")]
        [SerializeField]
        private List<CombatActionDefinition> comboActions = new() { null, null, null };

        [Tooltip("动作归一化时间达到该值后，才允许缓存下一段普攻输入。")]
        [SerializeField, Range(0f, 1f)]
        private float inputWindowStart = 0.55f;

        [Tooltip("动作归一化时间超过该值后，不再接受本段的下一段输入。")]
        [SerializeField, Range(0f, 1f)]
        private float inputWindowEnd = 1f;

        private readonly PlayerComboSequence _sequence = new();
        private CombatActionDefinition _fallbackAction;

        /// <summary>当前连段的段数索引，从 0 开始。</summary>
        public int CurrentStepIndex => _sequence.CurrentStepIndex;

        /// <summary>当前段实际使用的动作资产。</summary>
        public CombatActionDefinition CurrentAction => ResolveAction(CurrentStepIndex);

        /// <summary>下一段实际使用的动作资产；没有下一段时为空。</summary>
        public CombatActionDefinition NextAction => _sequence.HasNext
            ? ResolveAction(CurrentStepIndex + 1)
            : null;

        /// <summary>是否已经在当前输入窗口中缓存了下一段。</summary>
        public bool HasQueuedNext => _sequence.HasQueuedNext;

        /// <summary>是否仍有下一段可执行。</summary>
        public bool HasNext => _sequence.HasNext;

        private void OnValidate()
        {
            inputWindowStart = Mathf.Clamp01(inputWindowStart);
            inputWindowEnd = Mathf.Clamp(inputWindowEnd, inputWindowStart, 1f);
        }

        /// <summary>从第一段开始一次新的普攻连段。</summary>
        public CombatActionDefinition BeginCombo(CombatActionDefinition fallbackAction)
        {
            _fallbackAction = fallbackAction;
            int stepCount = comboActions != null && comboActions.Count > 0 ? comboActions.Count : 1;
            _sequence.Begin(stepCount);
            return CurrentAction;
        }

        /// <summary>在不改变连段运行时状态的情况下，解析第一段将使用的动作。</summary>
        public CombatActionDefinition GetFirstAction(CombatActionDefinition fallbackAction)
        {
            if (comboActions != null && comboActions.Count > 0 && comboActions[0] != null)
            {
                return comboActions[0];
            }

            return fallbackAction;
        }

        /// <summary>在当前动作的合法窗口内尝试缓存下一段。</summary>
        public bool TryQueueNext(float normalizedActionTime)
        {
            return _sequence.TryQueueNext(normalizedActionTime, inputWindowStart, inputWindowEnd);
        }

        /// <summary>判断当前时间是否处于可缓存下一段的窗口。</summary>
        public bool CanQueueNext(float normalizedActionTime)
        {
            return _sequence.CanQueueNext(normalizedActionTime, inputWindowStart, inputWindowEnd);
        }

        /// <summary>消费已缓存输入并推进到下一段。</summary>
        public bool TryAdvance()
        {
            return _sequence.TryAdvance();
        }

        /// <summary>清除当前连段进度。</summary>
        public void ResetCombo()
        {
            _fallbackAction = null;
            _sequence.Reset();
        }

        private CombatActionDefinition ResolveAction(int stepIndex)
        {
            if (comboActions != null
                && stepIndex >= 0
                && stepIndex < comboActions.Count
                && comboActions[stepIndex] != null)
            {
                return comboActions[stepIndex];
            }

            return _fallbackAction;
        }
    }

    /// <summary>
    /// 不依赖场景的连段序列状态，只保存段数、当前索引和下一段缓存。
    /// </summary>
    public sealed class PlayerComboSequence
    {
        private int _stepCount;

        public int CurrentStepIndex { get; private set; }

        public bool HasQueuedNext { get; private set; }

        public bool HasNext => _stepCount > 0 && CurrentStepIndex + 1 < _stepCount;

        public void Begin(int stepCount)
        {
            _stepCount = Mathf.Max(1, stepCount);
            CurrentStepIndex = 0;
            HasQueuedNext = false;
        }

        public bool TryQueueNext(float normalizedTime, float windowStart, float windowEnd)
        {
            if (!CanQueueNext(normalizedTime, windowStart, windowEnd))
            {
                return false;
            }

            HasQueuedNext = true;
            return true;
        }

        public bool CanQueueNext(float normalizedTime, float windowStart, float windowEnd)
        {
            if (!HasNext || HasQueuedNext)
            {
                return false;
            }

            float start = Mathf.Clamp01(windowStart);
            float end = Mathf.Clamp(windowEnd, start, 1f);
            float time = Mathf.Clamp01(normalizedTime);
            return time >= start && time <= end;
        }

        public bool TryAdvance()
        {
            if (!HasQueuedNext || !HasNext)
            {
                return false;
            }

            CurrentStepIndex++;
            HasQueuedNext = false;
            return true;
        }

        public void Reset()
        {
            _stepCount = 0;
            CurrentStepIndex = 0;
            HasQueuedNext = false;
        }
    }
}
