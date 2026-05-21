using System.Collections.Generic;
using EndLink.Ally;
using UnityEngine;

namespace EndLink.Party
{
    /// <summary>
    /// 固定三人小队管理器。
    /// 第一版只管理一个固定主控和两个固定队友，不处理主控切换、入队离队或复杂编队。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyManager : MonoBehaviour
    {
        [Header("固定主控")]
        [Tooltip("三人小队的固定主控角色。相机、输入和玩家状态机仍然绑定这个角色。")]
        [SerializeField]
        private Transform mainCharacter;

        [Header("队友槽位")]
        [Tooltip("第一个队友槽位，推荐偏移为左后方，例如 (-1.5, 0, -2.5)。")]
        [SerializeField]
        private PartyFormationSlot allySlotA = new();

        [Tooltip("第二个队友槽位，推荐偏移为右后方，例如 (1.5, 0, -2.5)。")]
        [SerializeField]
        private PartyFormationSlot allySlotB = new();

        [Header("调试")]
        [Tooltip("初始化小队时打印主控和队友槽位信息。")]
        [SerializeField]
        private bool logInitialization;

        /// <summary>固定主控角色。</summary>
        public Transform MainCharacter => mainCharacter;

        /// <summary>第一个队友槽位。</summary>
        public PartyFormationSlot AllySlotA => allySlotA;

        /// <summary>第二个队友槽位。</summary>
        public PartyFormationSlot AllySlotB => allySlotB;

        private void Start()
        {
            InitializeParty();
        }

        /// <summary>
        /// 初始化固定三人小队。
        /// 会把主控设置为两个队友的跟随目标，并把槽位偏移写入各自的 AllyFollowMotor。
        /// </summary>
        public void InitializeParty()
        {
            if (mainCharacter == null)
            {
                Debug.LogWarning("PartyManager 缺少 mainCharacter，无法初始化队友跟随。", this);
                return;
            }

            ApplySlot(allySlotA);
            ApplySlot(allySlotB);
        }

        /// <summary>
        /// 获取当前已配置的队友状态机。
        /// 后续 Ally AI、连携规则或调试工具需要查询队友列表时，可以先走这里。
        /// </summary>
        public IReadOnlyList<AllyStateMachine> GetAllies(List<AllyStateMachine> results)
        {
            results.Clear();

            AddAllyIfValid(results, allySlotA);
            AddAllyIfValid(results, allySlotB);

            return results;
        }

        private void ApplySlot(PartyFormationSlot slot)
        {
            if (slot == null || !slot.HasAlly)
            {
                return;
            }

            slot.Apply(mainCharacter);

            if (logInitialization)
            {
                Debug.Log(
                    $"初始化队友槽位：{slot.SlotName} -> {slot.AllyStateMachine.name}, offset={slot.FormationOffset}",
                    this);
            }
        }

        private static void AddAllyIfValid(List<AllyStateMachine> results, PartyFormationSlot slot)
        {
            if (slot != null && slot.HasAlly)
            {
                results.Add(slot.AllyStateMachine);
            }
        }
    }
}
