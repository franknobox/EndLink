using System.Collections.Generic;
using System.Text;
using EndLink.Ally;
using EndLink.Combat;
using EndLink.Party;
using TMPro;
using UnityEngine;

namespace EndLink.UI
{
    /// <summary>
    /// 运行时 HUD 调试日志面板。
    /// 轻量显示已有战斗事件和队友调试事件，方便 Play Mode 中观察当前战斗现场。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HUDDebugLogPanel : MonoBehaviour
    {
        [Header("显示组件")]
        [Tooltip("用于显示日志内容的 TextMeshProUGUI。")]
        [SerializeField]
        private TextMeshProUGUI logText;

        [Header("筛选")]
        [Tooltip("是否显示战斗动作、命中、死亡和协议反应等战斗事件。")]
        [SerializeField]
        private bool showCombat = true;

        [Tooltip("是否显示队友状态机、AI 和助战调试日志。")]
        [SerializeField]
        private bool showAlly = true;

        [Tooltip("是否显示小队命令路由日志。")]
        [SerializeField]
        private bool showParty = true;

        [Tooltip("是否显示伤害事件。伤害可能较频繁，默认关闭。")]
        [SerializeField]
        private bool showDamage;

        [Tooltip("是否显示标签添加、移除和过期事件。标签事件可能较频繁，默认关闭。")]
        [SerializeField]
        private bool showTag;

        [Header("限制")]
        [Tooltip("最多保留多少行日志。")]
        [SerializeField, Min(1)]
        private int maxLines = 20;

        [Tooltip("是否在启用时清空已有日志。")]
        [SerializeField]
        private bool clearOnEnable = true;

        [Tooltip("是否把进入 HUD 面板的日志同步输出到 Unity Console。默认关闭，避免刷屏。")]
        [SerializeField]
        private bool mirrorToConsole;

        private readonly Queue<string> _lines = new();
        private readonly StringBuilder _builder = new();

        private void Awake()
        {
            CacheReferences();
            RefreshText();
        }

        private void OnEnable()
        {
            CacheReferences();

            if (clearOnEnable)
            {
                Clear();
            }

            CombatEventsBus.Raised += HandleCombatEvent;
            AllyDebugLog.Raised += HandleAllyDebugLog;
            PartyCombatRouter.CommandRequested += HandlePartyCommandRequested;
        }

        private void OnDisable()
        {
            CombatEventsBus.Raised -= HandleCombatEvent;
            AllyDebugLog.Raised -= HandleAllyDebugLog;
            PartyCombatRouter.CommandRequested -= HandlePartyCommandRequested;
        }

        private void Reset()
        {
            CacheReferences();
        }

        private void OnValidate()
        {
            maxLines = Mathf.Max(1, maxLines);
            CacheReferences();
            TrimOverflow();
            RefreshText();
        }

        /// <summary>
        /// 清空 HUD 日志。
        /// </summary>
        public void Clear()
        {
            _lines.Clear();
            RefreshText();
        }

        private void CacheReferences()
        {
            if (logText == null)
            {
                logText = GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        private void HandleCombatEvent(CombatEvent eventData)
        {
            if (!ShouldShowCombatEvent(eventData.EventType))
            {
                return;
            }

            AppendLine(FormatCombatEvent(eventData));
        }

        private void HandleAllyDebugLog(AllyDebugEntry entry)
        {
            if (!showAlly)
            {
                return;
            }

            AppendLine(
                $"[{entry.TimeStamp:F1}] Ally/{entry.Category} {GetObjectName(entry.Ally)}: {entry.Message}");
        }

        private void HandlePartyCommandRequested(PartyCombatCommand command)
        {
            if (!showParty)
            {
                return;
            }

            AppendLine(
                $"[{command.TimeStamp:F1}] Party {command.CommandType}/{command.ActorSlot}: {GetObjectName(command.Actor)}");
        }

        private bool ShouldShowCombatEvent(CombatEventType eventType)
        {
            return eventType switch
            {
                CombatEventType.Damaged => showDamage,
                CombatEventType.TagAdded => showTag,
                CombatEventType.TagRemoved => showTag,
                CombatEventType.TagExpired => showTag,
                _ => showCombat
            };
        }

        private static string FormatCombatEvent(CombatEvent eventData)
        {
            return eventData.EventType switch
            {
                CombatEventType.ActionStarted =>
                    $"[{eventData.TimeStamp:F1}] Combat Action {GetObjectName(eventData.Source)} -> {GetObjectName(eventData.Target)} {GetActionName(eventData.ActionDefinition)}",
                CombatEventType.HitLanded =>
                    $"[{eventData.TimeStamp:F1}] Combat Hit {GetObjectName(eventData.Source)} -> {GetObjectName(eventData.Target)} {GetHitResult(eventData)}",
                CombatEventType.HitResolved =>
                    $"[{eventData.TimeStamp:F1}] Hit Resolved {GetObjectName(eventData.Source)} -> {GetObjectName(eventData.Target)} {GetHitResult(eventData)}",
                CombatEventType.Damaged =>
                    $"[{eventData.TimeStamp:F1}] Damage {GetObjectName(eventData.Source)} -> {GetObjectName(eventData.Target)} {eventData.DamageAmount:0} {eventData.DamageType}",
                CombatEventType.Dead =>
                    $"[{eventData.TimeStamp:F1}] Combat Dead {GetObjectName(eventData.Target)}",
                CombatEventType.TagAdded =>
                    $"[{eventData.TimeStamp:F1}] Tag + {GetTagName(eventData.CombatTag)} x{eventData.CombatTagStackCount} {GetObjectName(eventData.Source)} -> {GetObjectName(eventData.Target)}",
                CombatEventType.TagRemoved =>
                    $"[{eventData.TimeStamp:F1}] Tag - {GetTagName(eventData.CombatTag)} {GetObjectName(eventData.Target)}",
                CombatEventType.TagExpired =>
                    $"[{eventData.TimeStamp:F1}] Tag expired {GetTagName(eventData.CombatTag)} {GetObjectName(eventData.Target)}",
                CombatEventType.ReactionTriggered =>
                    $"[{eventData.TimeStamp:F1}] Combat Reaction {GetRuleName(eventData.ReactionRule)} target={GetObjectName(eventData.Target)}",
                _ =>
                    $"[{eventData.TimeStamp:F1}] Combat {eventData.EventType}"
            };
        }

        private void AppendLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            _lines.Enqueue(line);
            TrimOverflow();
            RefreshText();

            if (mirrorToConsole)
            {
                Debug.Log($"[HUDDebug] {line}", this);
            }
        }

        private void TrimOverflow()
        {
            while (_lines.Count > maxLines)
            {
                _lines.Dequeue();
            }
        }

        private void RefreshText()
        {
            if (logText == null)
            {
                return;
            }

            _builder.Clear();
            foreach (string line in _lines)
            {
                _builder.AppendLine(line);
            }

            logText.text = _builder.Length > 0 ? _builder.ToString() : "Debug Info";
        }

        private static string GetObjectName(GameObject gameObject)
        {
            return gameObject != null ? gameObject.name : "None";
        }

        private static string GetActionName(CombatActionDefinition actionDefinition)
        {
            return actionDefinition != null ? actionDefinition.ActionId : "None";
        }

        private static string GetTagName(CombatTagDefinition combatTag)
        {
            return combatTag != null ? combatTag.TagId : "None";
        }

        private static string GetRuleName(CombatTagCombinationRule reactionRule)
        {
            return reactionRule != null ? reactionRule.name : "None";
        }

        private static string GetHitResult(CombatEvent eventData)
        {
            return eventData.HasHitResolution
                ? $"{eventData.HitResolution.Outcome} dmg={eventData.HitResolution.AppliedDamage}"
                : $"dmg={eventData.DamageAmount:0}";
        }
    }
}
