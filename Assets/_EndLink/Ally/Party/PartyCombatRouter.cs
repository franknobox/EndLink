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
        Ultimate = 1
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
    /// 外部系统可以订阅 PartyCombatRouter.CommandRequested 做 UI、音效、调试或额外表现；
    /// 当前 Router 自身只保留旧技能与终链奥义的基础路由；连携命令入口已停用。
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
    /// 负责把 PlayerInputReader 中的战斗输入翻译成“主控/队友/全队”的命令请求。
    /// 它不生成 Hitbox、不结算伤害；旧技能会转发给角色状态机，终链奥义会转发给小队奥义上下文。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PartyUltimateContext))]
    public sealed class PartyCombatRouter : MonoBehaviour
    {
        [Header("组件引用")]
        [Tooltip("玩家输入读取器。为空时会在场景中自动查找。")]
        [SerializeField]
        private PlayerInputReader inputReader;

        [Tooltip("固定小队管理器。为空时会在场景中自动查找。")]
        [SerializeField]
        private PartyManager partyManager;

        [Tooltip("小队终链奥义上下文。负责协同率、奥义就绪和奥义消耗；不执行具体奥义表现。")]
        [SerializeField]
        private PartyUltimateContext ultimateContext;

        [Header("旧主动技能键位（当前停用）")]
        [Tooltip("旧主控主动技能入口。当前默认不绑定，后续技能键位由新版单人战斗方案重新确定。")]
        [SerializeField]
        private Key playerSkillKey = Key.None;

        [Tooltip("旧队友 A 主动技能入口。当前默认不绑定。")]
        [SerializeField]
        private Key allySlotASkillKey = Key.None;

        [Tooltip("旧队友 B 主动技能入口。当前默认不绑定。")]
        [SerializeField]
        private Key allySlotBSkillKey = Key.None;

        [Header("终链奥义键位")]
        [Tooltip("全队终链奥义键位。默认 V。")]
        [SerializeField]
        private Key partyUltimateKey = Key.V;

        [Header("调试")]
        [Tooltip("是否在收到旧技能、动作请求或终链奥义请求时打印调试日志。")]
        [SerializeField]
        private bool logCommands = true;

        /// <summary>战斗命令请求事件。</summary>
        public static event Action<PartyCombatCommand> CommandRequested;

        /// <summary>当前仍启用的战斗键位绑定变化事件，供 UI 刷新键位文本。</summary>
        public event Action KeyBindingsChanged;

        /// <summary>当前绑定的小队终链奥义上下文。</summary>
        public PartyUltimateContext UltimateContext => ultimateContext;

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

            if (inputReader.ConsumePartyUltimatePressed())
            {
                RouteCommand(PartyCombatCommandType.Ultimate, PartyCombatActorSlot.Party);
            }
        }

        /// <summary>
        /// 把 Inspector 中仍保留的旧主动技能和终链奥义键位覆盖到运行时 InputAction。
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
                partyUltimateKey);

            KeyBindingsChanged?.Invoke();
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
                PartyCombatCommandType.Ultimate => actorSlot == PartyCombatActorSlot.Party ? partyUltimateKey : Key.None,
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

            if (ultimateContext == null)
            {
                ultimateContext = GetComponent<PartyUltimateContext>();
            }

            if (ultimateContext == null && partyManager != null)
            {
                ultimateContext = partyManager.GetComponent<PartyUltimateContext>();
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
                case PartyCombatCommandType.Ultimate:
                    ExecuteUltimateCommand(command);
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

        private void ExecuteUltimateCommand(PartyCombatCommand command)
        {
            if (ultimateContext == null)
            {
                CacheReferences();
            }

            if (ultimateContext == null)
            {
                LogCommand("ignored Ultimate: PartyUltimateContext not found");
                return;
            }

            if (!ultimateContext.CanUseUltimate)
            {
                LogCommand(
                    $"ignored Ultimate: synergy={ultimateContext.CurrentSynergyRate:F1}/{ultimateContext.MaxSynergyRate:F1}");
                return;
            }

            if (ultimateContext.ConsumeUltimate())
            {
                LogCommand("Ultimate consumed: EndLink Ultimate effect is not implemented yet");
            }
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
                    && partyManager.AllySlotA.IsActive
                        ? partyManager.AllySlotA.AllyStateMachine.gameObject
                        : null,
                PartyCombatActorSlot.AllySlotB => partyManager.AllySlotB != null
                    && partyManager.AllySlotB.IsActive
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
