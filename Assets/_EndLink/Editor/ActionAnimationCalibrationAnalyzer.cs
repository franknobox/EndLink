using System;
using System.Collections.Generic;
using System.Linq;
using EndLink.Combat;
using EndLink.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EndLink.Editor
{
    internal static class ActionAnimationCalibrationAnalyzer
    {
        internal const string HitboxStartEvent = nameof(CombatAnimationEventReceiver.OnActionHitboxStart);
        internal const string HitboxEndEvent = nameof(CombatAnimationEventReceiver.OnActionHitboxEnd);
        internal const string CanCancelEvent = nameof(CombatAnimationEventReceiver.OnActionCanCancel);
        internal const string ActionEndEvent = nameof(CombatAnimationEventReceiver.OnActionEnd);

        private const float DurationTolerance = 0.05f;

        internal static List<AnimatorStateRecord> CollectStates(AnimatorController controller)
        {
            List<AnimatorStateRecord> states = new();
            if (controller == null)
            {
                return states;
            }

            AnimatorControllerLayer[] layers = controller.layers;
            for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
            {
                CollectStatesRecursive(
                    layers[layerIndex].stateMachine,
                    layers[layerIndex].name,
                    layerIndex,
                    states);
            }

            CollectIncomingTransitions(controller, states);
            return states;
        }

        internal static List<CalibrationIssue> Analyze(
            CombatActionDefinition action,
            AnimationClip clip,
            AnimatorController controller,
            AnimatorStateRecord state)
        {
            List<CalibrationIssue> issues = new();
            if (action == null)
            {
                issues.Add(CalibrationIssue.Error("尚未指定 Combat Action。"));
                return issues;
            }

            AnalyzeAction(action, issues);
            if (clip == null)
            {
                issues.Add(CalibrationIssue.Error("尚未指定用于校准的 Animation Clip。"));
                return issues;
            }

            AnimationEvent[] events = GetSortedEvents(clip);
            AnalyzeClip(action, clip, state, events, issues);
            AnalyzeEvents(action, clip, events, issues);
            AnalyzeAnimator(action, clip, controller, state, issues);
            return issues;
        }

        internal static AnimationEvent[] GetSortedEvents(AnimationClip clip)
        {
            return clip == null
                ? Array.Empty<AnimationEvent>()
                : AnimationUtility.GetAnimationEvents(clip)
                    .OrderBy(animationEvent => animationEvent.time)
                    .ToArray();
        }

        internal static float GetEffectiveClipDuration(AnimationClip clip, AnimatorStateRecord state)
        {
            if (clip == null)
            {
                return 0f;
            }

            if (state == null || state.State == null || state.State.speedParameterActive)
            {
                return clip.length;
            }

            float speed = Mathf.Abs(state.State.speed);
            return speed > 0.0001f ? clip.length / speed : clip.length;
        }

        internal static bool MotionContainsClip(Motion motion, AnimationClip clip)
        {
            if (motion == null || clip == null)
            {
                return false;
            }

            if (motion == clip)
            {
                return true;
            }

            if (motion is not BlendTree blendTree)
            {
                return false;
            }

            foreach (ChildMotion child in blendTree.children)
            {
                if (MotionContainsClip(child.motion, clip))
                {
                    return true;
                }
            }

            return false;
        }

        internal static AnimationClip FindFirstClip(Motion motion)
        {
            if (motion is AnimationClip clip)
            {
                return clip;
            }

            if (motion is BlendTree blendTree)
            {
                foreach (ChildMotion child in blendTree.children)
                {
                    AnimationClip childClip = FindFirstClip(child.motion);
                    if (childClip != null)
                    {
                        return childClip;
                    }
                }
            }

            return null;
        }

        internal static bool HasExactActionMapping(AnimatorStateRecord state, CombatActionDefinition action)
        {
            if (state == null || action == null || string.IsNullOrWhiteSpace(action.ActionId))
            {
                return false;
            }

            int actionHash = Animator.StringToHash(action.ActionId);
            return state.IncomingTransitions.Any(transition =>
                HasEqualsCondition(transition, CombatAnimatorParams.ActionId, actionHash));
        }

        internal static bool HasActionTypeMapping(AnimatorStateRecord state, CombatActionDefinition action)
        {
            return state != null
                && action != null
                && state.IncomingTransitions.Any(transition =>
                    HasEqualsCondition(
                        transition,
                        CombatAnimatorParams.ActionType,
                        (int)action.ActionType));
        }

        internal static bool HasActionTrigger(AnimatorStateRecord state)
        {
            return state != null && state.IncomingTransitions.Any(transition =>
                transition.conditions.Any(condition =>
                    condition.parameter == CombatAnimatorParams.ActionTrigger
                    && condition.mode == AnimatorConditionMode.If));
        }

        private static void AnalyzeAction(CombatActionDefinition action, List<CalibrationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(action.ActionId))
            {
                issues.Add(CalibrationIssue.Error("Action Id 为空，Animator 无法建立稳定动作映射。"));
            }

            if (action.HitboxPrefab == null)
            {
                issues.Add(CalibrationIssue.Error("Action 没有配置 Hitbox Prefab。"));
                return;
            }

            Collider[] colliders = action.HitboxPrefab.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                issues.Add(CalibrationIssue.Error("Hitbox Prefab 中没有 Collider。"));
            }

            if (action.HitboxPrefab.GetComponentInChildren<HitboxBase>(true) == null)
            {
                issues.Add(CalibrationIssue.Error("Hitbox Prefab 中没有 HitboxBase。"));
            }
        }

        private static void AnalyzeClip(
            CombatActionDefinition action,
            AnimationClip clip,
            AnimatorStateRecord state,
            AnimationEvent[] events,
            List<CalibrationIssue> issues)
        {
            if (clip.length <= 0f)
            {
                issues.Add(CalibrationIssue.Error("Animation Clip 时长为 0。"));
                return;
            }

            if (clip.isLooping)
            {
                issues.Add(CalibrationIssue.Warning("战斗动作 Clip 开启了循环，请确认这不是持续动作。"));
            }

            float effectiveDuration = GetEffectiveClipDuration(clip, state);
            float durationDelta = action.TotalDuration - effectiveDuration;
            float relativeDelta = Mathf.Abs(durationDelta) / Mathf.Max(0.01f, effectiveDuration);
            if (Mathf.Abs(durationDelta) > DurationTolerance && relativeDelta > 0.05f)
            {
                issues.Add(CalibrationIssue.Warning(
                    $"Action 总时长 {action.TotalDuration:0.###}s 与 State 有效 Clip 时长 "
                    + $"{effectiveDuration:0.###}s 相差 {durationDelta:+0.###;-0.###;0}s。"));
            }

            AnimationEvent actionEnd = events.LastOrDefault(animationEvent =>
                animationEvent.functionName == ActionEndEvent);
            if (action.TimingSource == CombatActionTimingSource.AnimationEventDriven
                && actionEnd != null
                && action.TotalDuration + DurationTolerance < actionEnd.time)
            {
                issues.Add(CalibrationIssue.Error(
                    $"Action 安全超时 {action.TotalDuration:0.###}s 早于 ActionEnd 事件 "
                    + $"{actionEnd.time:0.###}s，运行时会提前结束动作。"));
            }

            if (state?.State != null && state.State.speedParameterActive)
            {
                issues.Add(CalibrationIssue.Info(
                    $"State 使用速度参数 {state.State.speedParameter}，无法静态确定实际播放时长；当前按原始 Clip 时长比较。"));
            }
            else if (state?.State != null && Mathf.Abs(state.State.speed) <= 0.0001f)
            {
                issues.Add(CalibrationIssue.Error("Animator State Speed 为 0，动作不会推进。"));
            }
        }

        private static void AnalyzeEvents(
            CombatActionDefinition action,
            AnimationClip clip,
            AnimationEvent[] events,
            List<CalibrationIssue> issues)
        {
            AnimationEvent[] combatEvents = events.Where(IsCombatEvent).ToArray();
            foreach (AnimationEvent animationEvent in combatEvents)
            {
                if (animationEvent.time < 0f || animationEvent.time > clip.length + DurationTolerance)
                {
                    issues.Add(CalibrationIssue.Error(
                        $"事件 {animationEvent.functionName} 位于 Clip 范围外：{animationEvent.time:0.###}s。"));
                }
            }

            if (action.TimingSource != CombatActionTimingSource.AnimationEventDriven)
            {
                if (combatEvents.Length > 0)
                {
                    issues.Add(CalibrationIssue.Info(
                        "当前 Action 是 Data Driven；Clip 中的动作事件不会驱动该 Action 的判定时序。"));
                }

                return;
            }

            int hitboxStartCount = CountEvents(events, HitboxStartEvent);
            int hitboxEndCount = CountEvents(events, HitboxEndEvent);
            int canCancelCount = CountEvents(events, CanCancelEvent);
            int actionEndCount = CountEvents(events, ActionEndEvent);

            if (hitboxStartCount == 0)
            {
                issues.Add(CalibrationIssue.Error($"Clip 缺少 {HitboxStartEvent}，动作不会生成判定。"));
            }

            if (hitboxEndCount == 0)
            {
                issues.Add(CalibrationIssue.Error($"Clip 缺少 {HitboxEndEvent}。"));
            }

            if (canCancelCount == 0)
            {
                issues.Add(CalibrationIssue.Warning($"Clip 缺少 {CanCancelEvent}，动作期间不会开放取消窗口。"));
            }

            if (actionEndCount == 0)
            {
                issues.Add(CalibrationIssue.Error($"Clip 缺少 {ActionEndEvent}，只能依赖 Action 安全超时结束。"));
            }
            else if (actionEndCount > 1)
            {
                issues.Add(CalibrationIssue.Warning($"Clip 包含 {actionEndCount} 个 {ActionEndEvent}，通常只应保留最后一个。"));
            }

            ValidateEventOrder(events, issues);
        }

        private static void AnalyzeAnimator(
            CombatActionDefinition action,
            AnimationClip clip,
            AnimatorController controller,
            AnimatorStateRecord state,
            List<CalibrationIssue> issues)
        {
            if (controller == null)
            {
                issues.Add(CalibrationIssue.Warning("尚未指定 Animator Controller，无法检查 Action 到 State 的映射。"));
                return;
            }

            if (state == null || state.State == null)
            {
                issues.Add(CalibrationIssue.Error("Controller 中尚未选择 Animator State。"));
                return;
            }

            if (!MotionContainsClip(state.State.motion, clip))
            {
                issues.Add(CalibrationIssue.Error($"State {state.Path} 没有使用当前 Clip。"));
            }

            bool exactMapping = HasExactActionMapping(state, action);
            bool typeMapping = HasActionTypeMapping(state, action);
            bool actionTrigger = HasActionTrigger(state);

            if (!actionTrigger)
            {
                issues.Add(CalibrationIssue.Warning(
                    $"State {state.Path} 的入口没有使用 {CombatAnimatorParams.ActionTrigger} 条件。"));
            }

            if (exactMapping)
            {
                issues.Add(CalibrationIssue.Info("Animator State 使用 ActionId 精确映射当前 Action。"));
            }
            else if (typeMapping)
            {
                issues.Add(CalibrationIssue.Warning(
                    "Animator State 仅按 ActionType 映射；同类型 Action 增加后会共用这一状态。"));
            }
            else
            {
                issues.Add(CalibrationIssue.Error(
                    "所选 State 的入口条件既不匹配当前 ActionId，也不匹配 ActionType。"));
            }
        }

        private static void ValidateEventOrder(AnimationEvent[] events, List<CalibrationIssue> issues)
        {
            bool hitboxOpen = false;
            float lastActionEndTime = -1f;
            foreach (AnimationEvent animationEvent in events)
            {
                switch (animationEvent.functionName)
                {
                    case HitboxStartEvent:
                        if (hitboxOpen)
                        {
                            issues.Add(CalibrationIssue.Warning(
                                $"{animationEvent.time:0.###}s 再次开始判定前没有 HitboxEnd；运行时会强制关闭上一判定。"));
                        }

                        hitboxOpen = true;
                        break;
                    case HitboxEndEvent:
                        if (!hitboxOpen)
                        {
                            issues.Add(CalibrationIssue.Warning(
                                $"{animationEvent.time:0.###}s 的 HitboxEnd 前没有对应 HitboxStart。"));
                        }

                        hitboxOpen = false;
                        break;
                    case ActionEndEvent:
                        lastActionEndTime = animationEvent.time;
                        if (hitboxOpen)
                        {
                            issues.Add(CalibrationIssue.Warning(
                                "ActionEnd 触发时判定窗口仍处于开启状态；运行时会在动作结束时回收判定。"));
                            hitboxOpen = false;
                        }

                        break;
                }
            }

            if (hitboxOpen)
            {
                issues.Add(CalibrationIssue.Warning("最后一个 HitboxStart 没有后续 HitboxEnd。"));
            }

            if (lastActionEndTime >= 0f)
            {
                AnimationEvent laterCombatEvent = events.FirstOrDefault(animationEvent =>
                    IsCombatEvent(animationEvent)
                    && animationEvent.functionName != ActionEndEvent
                    && animationEvent.time > lastActionEndTime + 0.0001f);
                if (laterCombatEvent != null)
                {
                    issues.Add(CalibrationIssue.Error(
                        $"{laterCombatEvent.functionName} 位于 ActionEnd 之后，不会可靠执行。"));
                }
            }
        }

        private static int CountEvents(AnimationEvent[] events, string functionName)
        {
            return events.Count(animationEvent => animationEvent.functionName == functionName);
        }

        private static bool IsCombatEvent(AnimationEvent animationEvent)
        {
            return animationEvent.functionName == HitboxStartEvent
                || animationEvent.functionName == HitboxEndEvent
                || animationEvent.functionName == CanCancelEvent
                || animationEvent.functionName == ActionEndEvent;
        }

        private static bool HasEqualsCondition(
            AnimatorStateTransition transition,
            string parameterName,
            int expectedValue)
        {
            return transition != null && transition.conditions.Any(condition =>
                condition.parameter == parameterName
                && condition.mode == AnimatorConditionMode.Equals
                && Mathf.RoundToInt(condition.threshold) == expectedValue);
        }

        private static void CollectStatesRecursive(
            AnimatorStateMachine stateMachine,
            string path,
            int layerIndex,
            List<AnimatorStateRecord> states)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                states.Add(new AnimatorStateRecord(child.state, $"{path}/{child.state.name}", layerIndex));
            }

            foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines)
            {
                CollectStatesRecursive(
                    child.stateMachine,
                    $"{path}/{child.stateMachine.name}",
                    layerIndex,
                    states);
            }
        }

        private static void CollectIncomingTransitions(
            AnimatorController controller,
            List<AnimatorStateRecord> states)
        {
            Dictionary<AnimatorState, AnimatorStateRecord> lookup = states.ToDictionary(record => record.State);
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                CollectTransitionsRecursive(layer.stateMachine, lookup);
            }
        }

        private static void CollectTransitionsRecursive(
            AnimatorStateMachine stateMachine,
            IReadOnlyDictionary<AnimatorState, AnimatorStateRecord> lookup)
        {
            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
            {
                AddIncomingTransition(transition, lookup);
            }

            foreach (AnimatorTransition transition in stateMachine.entryTransitions)
            {
                if (transition.destinationState != null
                    && lookup.TryGetValue(transition.destinationState, out AnimatorStateRecord record))
                {
                    record.HasEntryTransition = true;
                }
            }

            foreach (ChildAnimatorState child in stateMachine.states)
            {
                foreach (AnimatorStateTransition transition in child.state.transitions)
                {
                    AddIncomingTransition(transition, lookup);
                }
            }

            foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines)
            {
                CollectTransitionsRecursive(child.stateMachine, lookup);
            }
        }

        private static void AddIncomingTransition(
            AnimatorStateTransition transition,
            IReadOnlyDictionary<AnimatorState, AnimatorStateRecord> lookup)
        {
            if (transition != null
                && transition.destinationState != null
                && lookup.TryGetValue(transition.destinationState, out AnimatorStateRecord record))
            {
                record.IncomingTransitions.Add(transition);
            }
        }
    }

    internal sealed class AnimatorStateRecord
    {
        internal AnimatorStateRecord(AnimatorState state, string path, int layerIndex)
        {
            State = state;
            Path = path;
            LayerIndex = layerIndex;
        }

        internal AnimatorState State { get; }

        internal string Path { get; }

        internal int LayerIndex { get; }

        internal List<AnimatorStateTransition> IncomingTransitions { get; } = new();

        internal bool HasEntryTransition { get; set; }
    }

    internal enum CalibrationSeverity
    {
        Info,
        Warning,
        Error
    }

    internal readonly struct CalibrationIssue
    {
        private CalibrationIssue(CalibrationSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }

        internal CalibrationSeverity Severity { get; }

        internal string Message { get; }

        internal static CalibrationIssue Info(string message) => new(CalibrationSeverity.Info, message);

        internal static CalibrationIssue Warning(string message) => new(CalibrationSeverity.Warning, message);

        internal static CalibrationIssue Error(string message) => new(CalibrationSeverity.Error, message);
    }
}
