using System;
using EndLink.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EndLink.Party
{
    /// <summary>
    /// 小队战斗命令类型。
    /// 这里描述玩家发出的命令语义，不描述动作数据本身。
    /// </summary>
    public enum PartyCombatCommandType
    {
        Skill = 0,
        /// <summary>
        /// 连携技请求。
        /// 这不是普通快捷键释放，后续必须由连携机制确认当前存在合法连携窗口后才能执行。
        /// </summary>
        LinkAttack = 1,
        Ultimate = 2
    }

    /// <summary>
    /// 小队战斗命令目标槽位。
    /// </summary>
    public enum PartyCombatActorSlot
    {
        MainCharacter = 0,
        AllySlotA = 1,
        AllySlotB = 2,
        Party = 3
    }

    /// <summary>
    /// 一次玩家主动发出的战斗命令。
    /// 后续 Skill / Link / Ultimate 系统可以订阅 PartyCombatRouter.CommandRequested 来执行具体动作。
    /// LinkAttack 类型只表示玩家请求使用连携槽位，不代表动作可以直接释放。
    /// </summary>
    public readonly struct PartyCombatCommand
    {
        public PartyCombatCommand(
            PartyCombatCommandType commandType,
            PartyCombatActorSlot actorSlot,
            GameObject actor,
            float timeStamp)
        {
            CommandType = commandType;
            ActorSlot = actorSlot;
            Actor = actor;
            TimeStamp = timeStamp;
        }

        public PartyCombatCommandType CommandType { get; }

        public PartyCombatActorSlot ActorSlot { get; }

        public GameObject Actor { get; }

        public float TimeStamp { get; }
    }

    /// <summary>
    /// 小队战斗命令路由器。
    /// 负责把 PlayerInputReader 中的战斗输入翻译成“主控/队友/全队”的命令请求，不直接执行 Hitbox、伤害或状态机逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyCombatRouter : MonoBehaviour
    {
        [Header("组件引用")]
        [Tooltip("玩家输入读取器。为空时会在场景中自动查找。")]
        [SerializeField]
        private PlayerInputReader inputReader;

        [Tooltip("固定小队管理器。为空时会在场景中自动查找。")]
        [SerializeField]
        private PartyManager partyManager;

        [Header("主动技能键位")]
        [Tooltip("主控主动技能键位。默认 Q。")]
        [SerializeField]
        private Key playerSkillKey = Key.Q;

        [Tooltip("队友 A 主动技能键位。默认 E。")]
        [SerializeField]
        private Key allySlotASkillKey = Key.E;

        [Tooltip("队友 B 主动技能键位。默认 F。")]
        [SerializeField]
        private Key allySlotBSkillKey = Key.F;

        [Header("连携请求键位")]
        [Tooltip("主控连携请求键位。默认 1。按下后只发出请求，必须由连携机制确认可释放。")]
        [SerializeField]
        private Key playerLinkAttackKey = Key.Digit1;

        [Tooltip("队友 A 连携请求键位。默认 2。按下后只发出请求，必须由连携机制确认可释放。")]
        [SerializeField]
        private Key allySlotALinkAttackKey = Key.Digit2;

        [Tooltip("队友 B 连携请求键位。默认 3。按下后只发出请求，必须由连携机制确认可释放。")]
        [SerializeField]
        private Key allySlotBLinkAttackKey = Key.Digit3;

        [Header("调试")]
        [Tooltip("是否在收到战斗命令时打印调试日志。第一版还未接具体技能执行，建议开启便于确认输入和路由。")]
        [SerializeField]
        private bool logCommands = true;

        /// <summary>战斗命令请求事件。</summary>
        public static event Action<PartyCombatCommand> CommandRequested;

        private void Awake()
        {
            CacheReferences();
        }

        private void Start()
        {
            ApplyInputBindings();
        }

        private void Reset()
        {
            CacheReferences();
        }

        private void OnValidate()
        {
            if (Application.isPlaying && enabled)
            {
                ApplyInputBindings();
            }
        }

        private void Update()
        {
            if (inputReader == null)
            {
                return;
            }

            if (inputReader.ConsumePlayerSkillPressed())
            {
                RouteCommand(PartyCombatCommandType.Skill, PartyCombatActorSlot.MainCharacter);
            }

            if (inputReader.ConsumeAllySlotASkillPressed())
            {
                RouteCommand(PartyCombatCommandType.Skill, PartyCombatActorSlot.AllySlotA);
            }

            if (inputReader.ConsumeAllySlotBSkillPressed())
            {
                RouteCommand(PartyCombatCommandType.Skill, PartyCombatActorSlot.AllySlotB);
            }

            if (inputReader.ConsumePlayerLinkAttackPressed())
            {
                RouteCommand(PartyCombatCommandType.LinkAttack, PartyCombatActorSlot.MainCharacter);
            }

            if (inputReader.ConsumeAllySlotALinkAttackPressed())
            {
                RouteCommand(PartyCombatCommandType.LinkAttack, PartyCombatActorSlot.AllySlotA);
            }

            if (inputReader.ConsumeAllySlotBLinkAttackPressed())
            {
                RouteCommand(PartyCombatCommandType.LinkAttack, PartyCombatActorSlot.AllySlotB);
            }

            if (inputReader.ConsumePartyUltimatePressed())
            {
                RouteCommand(PartyCombatCommandType.Ultimate, PartyCombatActorSlot.Party);
            }
        }

        /// <summary>
        /// 把 Inspector 中配置的 Q/E/F 和 1/2/3 键位覆盖到运行时 InputAction。
        /// </summary>
        public void ApplyInputBindings()
        {
            if (inputReader == null)
            {
                CacheReferences();
            }

            if (inputReader == null)
            {
                return;
            }

            inputReader.ApplyPartyCombatKeyboardBindings(
                playerSkillKey,
                allySlotASkillKey,
                allySlotBSkillKey,
                playerLinkAttackKey,
                allySlotALinkAttackKey,
                allySlotBLinkAttackKey);
        }

        private void CacheReferences()
        {
            if (inputReader == null)
            {
                inputReader = FindFirstObjectByType<PlayerInputReader>();
            }

            if (partyManager == null)
            {
                partyManager = FindFirstObjectByType<PartyManager>();
            }
        }

        private void RouteCommand(PartyCombatCommandType commandType, PartyCombatActorSlot actorSlot)
        {
            GameObject actor = ResolveActor(actorSlot);

            if (actor == null && actorSlot != PartyCombatActorSlot.Party)
            {
                LogCommand($"ignored {commandType} for {actorSlot}: actor not found");
                return;
            }

            PartyCombatCommand command = new(commandType, actorSlot, actor, Time.time);
            CommandRequested?.Invoke(command);
            LogCommand($"command={commandType}, slot={actorSlot}, actor={GetObjectName(actor)}");
        }

        private GameObject ResolveActor(PartyCombatActorSlot actorSlot)
        {
            if (partyManager == null)
            {
                CacheReferences();
            }

            if (partyManager == null)
            {
                return null;
            }

            return actorSlot switch
            {
                PartyCombatActorSlot.MainCharacter => partyManager.MainCharacter != null
                    ? partyManager.MainCharacter.gameObject
                    : null,
                PartyCombatActorSlot.AllySlotA => partyManager.AllySlotA != null
                    && partyManager.AllySlotA.AllyStateMachine != null
                        ? partyManager.AllySlotA.AllyStateMachine.gameObject
                        : null,
                PartyCombatActorSlot.AllySlotB => partyManager.AllySlotB != null
                    && partyManager.AllySlotB.AllyStateMachine != null
                        ? partyManager.AllySlotB.AllyStateMachine.gameObject
                        : null,
                PartyCombatActorSlot.Party => partyManager.gameObject,
                _ => null
            };
        }

        private void LogCommand(string message)
        {
            if (logCommands)
            {
                Debug.Log($"PartyCombatRouter: {message}", this);
            }
        }

        private static string GetObjectName(UnityEngine.Object target)
        {
            return target != null ? target.name : "None";
        }
    }
}
