using System;
using EndLink.Ally;
using EndLink.Combat;
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
    [RequireComponent(typeof(PartyLinkContext))]
    public sealed class PartyCombatRouter : MonoBehaviour
    {
        [Header("组件引用")]
        [Tooltip("玩家输入读取器。为空时会在场景中自动查找。")]
        [SerializeField]
        private PlayerInputReader inputReader;

        [Tooltip("固定小队管理器。为空时会在场景中自动查找。")]
        [SerializeField]
        private PartyManager partyManager;

        [Tooltip("小队连携窗口上下文。负责判断连携是否解锁、解析目标并在成功释放后消费窗口。")]
        [SerializeField]
        private PartyLinkContext linkContext;

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

        /// <summary>当前绑定的小队连携窗口上下文。</summary>
        public PartyLinkContext LinkContext => linkContext;

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

        /// <summary>
        /// 获取指定战斗命令槽位当前使用的键位。
        /// 主要供 UI 和调试工具读取，实际输入仍由 PlayerInputReader 负责。
        /// </summary>
        public Key GetKeyForCommand(PartyCombatCommandType commandType, PartyCombatActorSlot actorSlot)
        {
            return commandType switch
            {
                PartyCombatCommandType.Skill => actorSlot switch
                {
                    PartyCombatActorSlot.MainCharacter => playerSkillKey,
                    PartyCombatActorSlot.AllySlotA => allySlotASkillKey,
                    PartyCombatActorSlot.AllySlotB => allySlotBSkillKey,
                    _ => Key.None
                },
                PartyCombatCommandType.LinkAttack => actorSlot switch
                {
                    PartyCombatActorSlot.MainCharacter => playerLinkAttackKey,
                    PartyCombatActorSlot.AllySlotA => allySlotALinkAttackKey,
                    PartyCombatActorSlot.AllySlotB => allySlotBLinkAttackKey,
                    _ => Key.None
                },
                PartyCombatCommandType.Ultimate => actorSlot == PartyCombatActorSlot.Party ? Key.V : Key.None,
                _ => Key.None
            };
        }

        /// <summary>
        /// 获取指定战斗命令槽位当前使用的键位显示文本。
        /// </summary>
        public string GetKeyLabelForCommand(PartyCombatCommandType commandType, PartyCombatActorSlot actorSlot)
        {
            Key key = GetKeyForCommand(commandType, actorSlot);
            return key == Key.None ? string.Empty : KeyToDisplayLabel(key);
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

            if (linkContext == null)
            {
                linkContext = GetComponent<PartyLinkContext>();
            }

            if (linkContext == null && partyManager != null)
            {
                linkContext = partyManager.GetComponent<PartyLinkContext>();
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
            ExecuteImmediateCommand(command);
            LogCommand($"command={commandType}, slot={actorSlot}, actor={GetObjectName(actor)}");
        }

        private void ExecuteImmediateCommand(PartyCombatCommand command)
        {
            switch (command.CommandType)
            {
                case PartyCombatCommandType.Skill:
                    ExecuteSkillCommand(command);
                    break;
                case PartyCombatCommandType.LinkAttack:
                    ExecuteLinkAttackCommand(command);
                    break;
                case PartyCombatCommandType.Ultimate:
                    LogCommand("ultimate request queued for future limit break system");
                    break;
            }
        }

        private void ExecuteSkillCommand(PartyCombatCommand command)
        {
            switch (command.ActorSlot)
            {
                case PartyCombatActorSlot.MainCharacter:
                    ExecutePlayerSkill(command.Actor);
                    break;
                case PartyCombatActorSlot.AllySlotA:
                case PartyCombatActorSlot.AllySlotB:
                    ExecuteAllySkill(command.Actor);
                    break;
                default:
                    LogCommand($"ignored Skill for {command.ActorSlot}: unsupported skill actor slot");
                    break;
            }
        }

        private void ExecutePlayerSkill(GameObject actor)
        {
            if (actor == null || !actor.TryGetComponent(out PlayerStateMachine stateMachine))
            {
                LogCommand($"ignored player Skill: PlayerStateMachine not found on {GetObjectName(actor)}");
                return;
            }

            bool requested = stateMachine.RequestSkill();
            LogCommand($"request player Skill state result={requested}, actor={GetObjectName(actor)}");
        }

        private void ExecuteAllySkill(GameObject actor)
        {
            if (actor == null || !actor.TryGetComponent(out AllyStateMachine stateMachine))
            {
                LogCommand($"ignored ally Skill: AllyStateMachine not found on {GetObjectName(actor)}");
                return;
            }

            AllyCombatDriver combatDriver = stateMachine.CombatDriver;
            if (combatDriver == null)
            {
                LogCommand($"ignored ally Skill: AllyCombatDriver not found on {GetObjectName(actor)}");
                return;
            }

            Transform target = ResolveAllySkillTarget(actor);
            bool requested = stateMachine.RequestAction(combatDriver.SkillAction, target);
            LogCommand($"request ally Skill action result={requested}, actor={GetObjectName(actor)}, target={GetObjectName(target)}");
        }

        private static Transform ResolveAllySkillTarget(GameObject actor)
        {
            if (actor == null)
            {
                return null;
            }

            if (actor.TryGetComponent(out AllyStateMachine stateMachine)
                && stateMachine.CurrentAssistTarget != null)
            {
                return stateMachine.CurrentAssistTarget;
            }

            if (actor.TryGetComponent(out AllyBrain brain) && brain.CurrentTarget != null)
            {
                return brain.CurrentTarget;
            }

            return null;
        }

        private void ExecuteLinkAttackCommand(PartyCombatCommand command)
        {
            if (linkContext == null)
            {
                CacheReferences();
            }

            if (linkContext == null || !linkContext.IsWindowOpen)
            {
                LogCommand($"ignored LinkAttack for {command.ActorSlot}: link window is closed");
                return;
            }

            Transform target = linkContext.ResolveLinkTarget();
            if (target == null)
            {
                LogCommand($"ignored LinkAttack for {command.ActorSlot}: no valid reaction or soft-lock target");
                return;
            }

            bool executed = command.ActorSlot switch
            {
                PartyCombatActorSlot.MainCharacter => RequestPlayerLinkAction(command.Actor, target),
                PartyCombatActorSlot.AllySlotA => RequestAllyLinkAction(command.Actor, target),
                PartyCombatActorSlot.AllySlotB => RequestAllyLinkAction(command.Actor, target),
                _ => false
            };

            if (!executed)
            {
                LogCommand(
                    $"LinkAttack request rejected, slot={command.ActorSlot}, actor={GetObjectName(command.Actor)}, target={GetObjectName(target)}");
                return;
            }

            linkContext.ConsumeWindow();
            LogCommand(
                $"LinkAttack accepted and window consumed, slot={command.ActorSlot}, actor={GetObjectName(command.Actor)}, target={GetObjectName(target)}");
        }

        private bool RequestPlayerLinkAction(GameObject actor, Transform target)
        {
            if (actor == null
                || !actor.TryGetComponent(out PlayerStateMachine stateMachine)
                || !actor.TryGetComponent(out PlayerCombatDriver combatDriver))
            {
                return false;
            }

            return stateMachine.RequestAction(combatDriver.LinkAction, target);
        }

        private static bool RequestAllyLinkAction(GameObject actor, Transform target)
        {
            if (actor == null || !actor.TryGetComponent(out AllyStateMachine stateMachine))
            {
                return false;
            }

            AllyCombatDriver combatDriver = stateMachine.CombatDriver;
            ICombatActionExecutor actionExecutor = combatDriver;
            if (actionExecutor == null || !actionExecutor.CanExecute(combatDriver.LinkAction))
            {
                return false;
            }

            return stateMachine.RequestAction(combatDriver.LinkAction, target);
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

        private static string KeyToDisplayLabel(Key key)
        {
            return key switch
            {
                Key.Digit0 => "0",
                Key.Digit1 => "1",
                Key.Digit2 => "2",
                Key.Digit3 => "3",
                Key.Digit4 => "4",
                Key.Digit5 => "5",
                Key.Digit6 => "6",
                Key.Digit7 => "7",
                Key.Digit8 => "8",
                Key.Digit9 => "9",
                _ => key.ToString().ToUpperInvariant()
            };
        }
    }
}
