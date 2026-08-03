using EndLink.Ally;
using EndLink.Combat;
using EndLink.Party;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EndLink.UI
{
    /// <summary>
    /// 战斗 UI 可以直接绑定的小队动作槽位。
    /// 这里描述的是“UI 上这个圆形按钮代表哪个队伍键位”，而不是具体 Action 资产。
    /// </summary>
    public enum UICombatActionSlotId
    {
        PlayerSkill = 0,
        AllySlotASkill = 1,
        AllySlotBSkill = 2,
        PartyUltimate = 6
    }

    /// <summary>
    /// 战斗动作槽位 UI 的最小冷却显示组件。
    /// 槽位绑定的是队伍键位槽，例如主控 Skill、队友 A Skill、队友 B Skill，而不是某个固定 Action 资产。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UICombatActionSlot : MonoBehaviour
    {
        [Header("槽位")]
        [Tooltip("小队管理器。为空时会在场景中自动查找。")]
        [SerializeField]
        private PartyManager partyManager;

        [Tooltip("该 UI 对应的小队键位槽。主动技能一般对应 PlayerSkill、AllySlotASkill、AllySlotBSkill。")]
        [SerializeField]
        private UICombatActionSlotId slot = UICombatActionSlotId.PlayerSkill;

        [Header("冷却染色")]
        [Tooltip("技能图标 Image。可以拖子物体上的 Icon Image。为空时会自动查找本物体或子物体上的第一个 Image。")]
        [SerializeField]
        private Image iconImage;

        [Tooltip("键位显示文本。可拖 TextMeshProUGUI；为空时只提供 KeyLabel 属性，不主动显示。")]
        [SerializeField]
        private TextMeshProUGUI keyLabelText;

        [Tooltip("冷却中施加到图标上的颜色。只要当前动作还在冷却，就直接使用该颜色；冷却结束后恢复图标原色。")]
        [SerializeField]
        private Color cooldownTintColor = new(0.45f, 0.45f, 0.45f, 0.65f);

        [Tooltip("是否每帧从对应角色槽位读取当前动作冷却。冷却是连续变化的显示，适合逐帧刷新。")]
        [SerializeField]
        private bool autoRefreshCooldown = true;

        [Tooltip("是否每帧刷新键位文本。键位通常只在绑定或配置变化时刷新，由上层 UIPartyCombatAction 统一驱动时可以关闭。")]
        [SerializeField]
        private bool autoRefreshKeyLabel = true;

        /// <summary>该 UI 对应的小队 UI 键位槽。</summary>
        public UICombatActionSlotId Slot => slot;

        /// <summary>该 UI 槽位映射到的小队战斗命令类型。</summary>
        public PartyCombatCommandType CommandType => ResolveCommandType(slot);

        /// <summary>该 UI 槽位映射到的小队角色槽位。</summary>
        public PartyCombatActorSlot ActorSlot => ResolveActorSlot(slot);

        /// <summary>当前解析到的战斗动作。由小队槽位和命令类型动态决定。</summary>
        public CombatActionDefinition CurrentAction => ResolveAction();

        /// <summary>当前槽位键位显示文本。由 PartyCombatRouter 当前配置决定。</summary>
        public string KeyLabel => ResolveKeyLabel();

        /// <summary>当前冷却比例，大于 0 时图标显示冷却颜色，等于 0 时恢复原色。</summary>
        public float CooldownNormalized { get; private set; }

        private Color originalIconColor = Color.white;
        private bool hasCachedOriginalIconColor;

        private void Awake()
        {
            CacheReferences();
            CacheIconIfNeeded();
            CacheOriginalIconColor();
            RefreshNow();
        }

        private void Reset()
        {
            CacheReferences();
            CacheIconIfNeeded();
            CacheOriginalIconColor();
        }

        private void OnValidate()
        {
            CooldownNormalized = Mathf.Clamp01(CooldownNormalized);
            CacheIconIfNeeded();
            ApplyCooldown();
            ApplyKeyLabel();
        }

        private void Update()
        {
            if (!autoRefreshCooldown && !autoRefreshKeyLabel)
            {
                return;
            }

            if (autoRefreshCooldown)
            {
                RefreshCooldown();
            }

            if (autoRefreshKeyLabel)
            {
                RefreshKeyLabel();
            }
        }

        /// <summary>
        /// 设置该 UI 对应的小队 UI 键位槽。
        /// </summary>
        public void SetSlot(UICombatActionSlotId nextSlot)
        {
            slot = nextSlot;
            RefreshNow();
        }

        /// <summary>
        /// 绑定小队管理器。上层 HUD 或动作栏应优先调用它，避免每个槽位各自全场景查找。
        /// </summary>
        public void BindPartyManager(PartyManager manager)
        {
            partyManager = manager;
            RefreshNow();
        }

        /// <summary>
        /// 设置是否由本槽位自己每帧刷新冷却。
        /// 当 UIPartyCombatAction 统一管理刷新时，应关闭它。
        /// </summary>
        public void SetAutoRefreshCooldown(bool enabled)
        {
            autoRefreshCooldown = enabled;
        }

        /// <summary>
        /// 设置是否由本槽位自己每帧刷新键位文本。
        /// </summary>
        public void SetAutoRefreshKeyLabel(bool enabled)
        {
            autoRefreshKeyLabel = enabled;
        }

        /// <summary>
        /// 立即从当前小队配置读取键位、动作和冷却状态，并刷新显示。
        /// </summary>
        public void RefreshNow()
        {
            RefreshCooldown();
            RefreshKeyLabel();
        }

        /// <summary>
        /// 只刷新冷却显示。
        /// 上层 HUD 每帧驱动时应优先调用它，避免反复刷新键位文本。
        /// </summary>
        public void RefreshCooldown()
        {
            SetCooldown(ResolveCooldownNormalized());
        }

        /// <summary>
        /// 只刷新键位文本。
        /// 键位绑定变化、槽位重新绑定或 Inspector 配置改变时调用即可。
        /// </summary>
        public void RefreshKeyLabel()
        {
            ApplyKeyLabel();
        }

        /// <summary>
        /// 设置冷却染色比例。
        /// normalized 约定为 1 表示刚进入冷却，0 表示冷却结束。
        /// </summary>
        public void SetCooldown(float normalized)
        {
            CooldownNormalized = Mathf.Clamp01(normalized);
            ApplyCooldown();
        }

        /// <summary>
        /// 清除冷却染色。
        /// </summary>
        public void ClearCooldown()
        {
            SetCooldown(0f);
        }

        private void CacheReferences()
        {
            if (partyManager == null)
            {
                partyManager = FindFirstObjectByType<PartyManager>();
            }
        }

        private void CacheIconIfNeeded()
        {
            if (iconImage != null)
            {
                return;
            }

            if (TryGetComponent(out Image selfImage))
            {
                iconImage = selfImage;
                return;
            }

            iconImage = GetComponentInChildren<Image>(true);
        }

        private void CacheOriginalIconColor()
        {
            if (iconImage == null)
            {
                return;
            }

            originalIconColor = iconImage.color;
            hasCachedOriginalIconColor = true;
        }

        private CombatActionDefinition ResolveAction()
        {
            return CommandType switch
            {
                PartyCombatCommandType.Skill => ResolveSkillAction(),
                _ => null
            };
        }

        private CombatActionDefinition ResolveSkillAction()
        {
            return ActorSlot switch
            {
                PartyCombatActorSlot.MainCharacter => ResolvePlayerCombatDriver()?.SkillAction,
                PartyCombatActorSlot.AllySlotA => ResolveAllyCombatDriver(PartyCombatActorSlot.AllySlotA)?.SkillAction,
                PartyCombatActorSlot.AllySlotB => ResolveAllyCombatDriver(PartyCombatActorSlot.AllySlotB)?.SkillAction,
                _ => null
            };
        }

        private float ResolveCooldownNormalized()
        {
            CombatActionDefinition currentAction = ResolveAction();
            ICombatActionExecutor actionExecutor = ResolveActionExecutor();
            return actionExecutor?.GetCooldownNormalized(currentAction) ?? 0f;
        }

        private string ResolveKeyLabel()
        {
            PartyCombatRouter combatRouter = ResolveCombatRouter();

            return combatRouter != null
                ? combatRouter.GetKeyLabelForCommand(CommandType, ActorSlot)
                : string.Empty;
        }

        private PartyCombatRouter ResolveCombatRouter()
        {
            if (partyManager == null)
            {
                CacheReferences();
            }

            return partyManager != null ? partyManager.CombatRouter : null;
        }

        private PlayerCombatDriver ResolvePlayerCombatDriver()
        {
            if (partyManager == null)
            {
                CacheReferences();
            }

            return partyManager != null
                && partyManager.MainCharacter != null
                && partyManager.MainCharacter.TryGetComponent(out PlayerCombatDriver combatDriver)
                    ? combatDriver
                    : null;
        }

        private AllyCombatDriver ResolveAllyCombatDriver(PartyCombatActorSlot slot)
        {
            if (partyManager == null)
            {
                CacheReferences();
            }

            AllyStateMachine stateMachine = slot switch
            {
                PartyCombatActorSlot.AllySlotA => partyManager != null && partyManager.AllySlotA?.IsActive == true
                    ? partyManager.AllySlotA.AllyStateMachine
                    : null,
                PartyCombatActorSlot.AllySlotB => partyManager != null && partyManager.AllySlotB?.IsActive == true
                    ? partyManager.AllySlotB.AllyStateMachine
                    : null,
                _ => null
            };

            return stateMachine != null ? stateMachine.CombatDriver : null;
        }

        private ICombatActionExecutor ResolveActionExecutor()
        {
            return ActorSlot switch
            {
                PartyCombatActorSlot.MainCharacter => ResolvePlayerCombatDriver(),
                PartyCombatActorSlot.AllySlotA => ResolveAllyCombatDriver(PartyCombatActorSlot.AllySlotA),
                PartyCombatActorSlot.AllySlotB => ResolveAllyCombatDriver(PartyCombatActorSlot.AllySlotB),
                _ => null
            };
        }

        private static PartyCombatCommandType ResolveCommandType(UICombatActionSlotId uiSlot)
        {
            return uiSlot switch
            {
                UICombatActionSlotId.PlayerSkill => PartyCombatCommandType.Skill,
                UICombatActionSlotId.AllySlotASkill => PartyCombatCommandType.Skill,
                UICombatActionSlotId.AllySlotBSkill => PartyCombatCommandType.Skill,
                UICombatActionSlotId.PartyUltimate => PartyCombatCommandType.Ultimate,
                _ => PartyCombatCommandType.Skill
            };
        }

        private static PartyCombatActorSlot ResolveActorSlot(UICombatActionSlotId uiSlot)
        {
            return uiSlot switch
            {
                UICombatActionSlotId.PlayerSkill => PartyCombatActorSlot.MainCharacter,
                UICombatActionSlotId.AllySlotASkill => PartyCombatActorSlot.AllySlotA,
                UICombatActionSlotId.AllySlotBSkill => PartyCombatActorSlot.AllySlotB,
                UICombatActionSlotId.PartyUltimate => PartyCombatActorSlot.Party,
                _ => PartyCombatActorSlot.MainCharacter
            };
        }

        private void ApplyCooldown()
        {
            if (iconImage == null)
            {
                return;
            }

            if (!hasCachedOriginalIconColor)
            {
                CacheOriginalIconColor();
            }

            iconImage.color = CooldownNormalized > 0f ? cooldownTintColor : originalIconColor;
        }

        private void ApplyKeyLabel()
        {
            if (keyLabelText == null)
            {
                return;
            }

            keyLabelText.text = KeyLabel;
        }
    }
}
