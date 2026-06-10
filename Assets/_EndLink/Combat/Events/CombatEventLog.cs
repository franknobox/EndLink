using System.Text;
using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 战斗事件日志监听器。
    /// 用于在 Console 中观察事件流；需要表格化查看时可以配合 Combat Monitor 使用。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatEventLog : MonoBehaviour
    {
        [Tooltip("是否打印事件日志。")]
        [SerializeField]
        private bool logEvents;

        [Tooltip("是否打印 Source 和 Target 名称。")]
        [SerializeField]
        private bool includeActors = true;

        [Tooltip("是否打印动作和标签信息。")]
        [SerializeField]
        private bool includeActionAndTag = true;

        [Tooltip("是否打印伤害值。")]
        [SerializeField]
        private bool includeDamage = true;

        private readonly StringBuilder _builder = new();

        private void OnEnable()
        {
            CombatEventsBus.Raised += OnCombatEventRaised;
        }

        private void OnDisable()
        {
            CombatEventsBus.Raised -= OnCombatEventRaised;
        }

        private void OnCombatEventRaised(CombatEvent eventData)
        {
            if (!logEvents)
            {
                return;
            }

            _builder.Clear();
            _builder.Append("[CombatEvent] ");
            _builder.Append(eventData.EventType);

            if (includeActors)
            {
                _builder.Append(" | source=");
                _builder.Append(GetObjectName(eventData.Source));
                _builder.Append(" | target=");
                _builder.Append(GetObjectName(eventData.Target));
            }

            if (includeActionAndTag)
            {
                _builder.Append(" | action=");
                _builder.Append(eventData.ActionDefinition != null ? eventData.ActionDefinition.ActionId : "None");
                _builder.Append(" | tag=");
                _builder.Append(eventData.CombatTag != null ? eventData.CombatTag.TagId : "None");
            }

            if (includeDamage)
            {
                _builder.Append(" | damage=");
                _builder.Append(eventData.DamageAmount);
                _builder.Append(" ");
                _builder.Append(eventData.DamageType);
            }

            Debug.Log(_builder.ToString(), this);
        }

        private static string GetObjectName(Object targetObject)
        {
            return targetObject != null ? targetObject.name : "None";
        }
    }
}
