using System;
using EndLink.Ally;
using UnityEngine;

namespace EndLink.Party
{
    /// <summary>
    /// 固定三人小队里的一个队友槽位配置。
    /// 只保存“哪个队友”和“站在主控哪里”，不负责执行状态切换或移动。
    /// </summary>
    [Serializable]
    public sealed class PartyFormationSlot
    {
        [Tooltip("槽位名称，只用于 Inspector 和调试日志，例如 Ally A / Ally B。")]
        [SerializeField]
        private string slotName = "Ally";

        [Tooltip("该槽位绑定的队友状态机。队友根物体上应挂 AllyStateMachine。")]
        [SerializeField]
        private AllyStateMachine allyStateMachine;

        [Tooltip("该队友相对主控角色的本地队形偏移。X 是左右，Z 是前后。")]
        [SerializeField]
        private Vector3 formationOffset = new Vector3(-2f, 0f, -2.5f);

        /// <summary>槽位名称。</summary>
        public string SlotName => slotName;

        /// <summary>槽位绑定的队友状态机。</summary>
        public AllyStateMachine AllyStateMachine => allyStateMachine;

        /// <summary>槽位队形偏移。</summary>
        public Vector3 FormationOffset => formationOffset;

        /// <summary>该槽位是否已经绑定了队友。</summary>
        public bool HasAlly => allyStateMachine != null;

        /// <summary>
        /// 把槽位配置应用到队友。
        /// PartyManager 调用它完成固定小队初始化。
        /// </summary>
        public void Apply(Transform mainCharacter)
        {
            if (allyStateMachine == null)
            {
                return;
            }

            AllyFollowMotor followMotor = allyStateMachine.FollowMotor;
            if (followMotor != null)
            {
                followMotor.SetFormationOffset(formationOffset);
            }

            allyStateMachine.SetFollowTarget(mainCharacter);
        }
    }
}
