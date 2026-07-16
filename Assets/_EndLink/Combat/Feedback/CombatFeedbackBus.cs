using System;
using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>一次战斗反馈请求，只描述播放什么、在哪里播放以及事件来源。</summary>
    public readonly struct CombatFeedbackRequest
    {
        public CombatFeedbackRequest(
            CombatFeedbackDefinition definition,
            Vector3 position,
            Vector3 direction,
            GameObject source = null,
            GameObject target = null)
        {
            Definition = definition;
            Position = position;
            Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
            Source = source;
            Target = target;
        }

        public CombatFeedbackDefinition Definition { get; }
        public Vector3 Position { get; }
        public Vector3 Direction { get; }
        public GameObject Source { get; }
        public GameObject Target { get; }
    }

    /// <summary>
    /// 战斗反馈请求总线。
    /// 玩法系统只提交请求，场景中的 CombatFeedbackDispatcher 负责执行具体表现。
    /// </summary>
    public static class CombatFeedbackBus
    {
        /// <summary>全局反馈请求；订阅者必须在 OnEnable/OnDisable 中成对订阅和退订。</summary>
        public static event Action<CombatFeedbackRequest> Requested;

        /// <summary>提交一次反馈请求。配置为空时不会广播。</summary>
        public static void Raise(
            CombatFeedbackDefinition definition,
            Vector3 position,
            Vector3 direction,
            GameObject source = null,
            GameObject target = null)
        {
            if (definition == null)
            {
                return;
            }

            Requested?.Invoke(new CombatFeedbackRequest(
                definition,
                position,
                direction,
                source,
                target));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticEvents()
        {
            Requested = null;
        }
    }
}
