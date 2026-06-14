using EndLink.Party;
using UnityEngine;

namespace EndLink.UI
{
    /// <summary>
    /// 小队战斗动作 UI 管理器。
    /// 负责把 HUD 上的技能槽、连携槽和终链奥义槽绑定到固定三人小队的战斗命令槽位。
    /// 它只刷新 UI，不执行技能、不判断连携是否合法。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIPartyCombatAction : MonoBehaviour
    {
        [Header("组件引用")]
        [Tooltip("小队管理器。为空时会在场景中自动查找。")]
        [SerializeField]
        private PartyManager partyManager;

        [Tooltip("启用时会从子物体自动收集 UICombatActionSlot，并按槽位类型补全下方引用。")]
        [SerializeField]
        private bool autoCollectChildSlots = true;

        [Header("主动技能槽")]
        [Tooltip("主控主动技能 UI 槽，默认对应 Q。")]
        [SerializeField]
        private UICombatActionSlot playerSkillSlot;

        [Tooltip("队友 A 主动技能 UI 槽，默认对应 E。")]
        [SerializeField]
        private UICombatActionSlot allySlotASkillSlot;

        [Tooltip("队友 B 主动技能 UI 槽，默认对应 F。")]
        [SerializeField]
        private UICombatActionSlot allySlotBSkillSlot;

        [Header("连携槽")]
        [Tooltip("主控连携请求 UI 槽，默认对应 1。")]
        [SerializeField]
        private UICombatActionSlot playerLinkAttackSlot;

        [Tooltip("队友 A 连携请求 UI 槽，默认对应 2。")]
        [SerializeField]
        private UICombatActionSlot allySlotALinkAttackSlot;

        [Tooltip("队友 B 连携请求 UI 槽，默认对应 3。")]
        [SerializeField]
        private UICombatActionSlot allySlotBLinkAttackSlot;

        [Header("终链奥义槽")]
        [Tooltip("全队终链奥义 UI 槽，默认对应 V。")]
        [SerializeField]
        private UICombatActionSlot partyUltimateSlot;

        [Header("刷新")]
        [Tooltip("是否由本组件在 LateUpdate 统一刷新所有动作槽的连续显示，例如冷却。键位和绑定只在 Bind 或 RefreshNow 时刷新。")]
        [SerializeField]
        private bool autoRefresh = true;

        [Tooltip("是否关闭子槽位自己的每帧刷新，改由本组件统一驱动。建议开启。")]
        [SerializeField]
        private bool driveChildSlotsManually = true;

        /// <summary>当前绑定的小队管理器。</summary>
        public PartyManager PartyManager => partyManager;

        private PartyCombatRouter _subscribedRouter;

        private void Awake()
        {
            CacheReferences();
            BindSlots();
        }

        private void OnEnable()
        {
            SubscribeRouterEvents();
        }

        private void OnDisable()
        {
            UnsubscribeRouterEvents();
        }

        private void Reset()
        {
            CacheReferences();
            CollectChildSlots();
        }

        private void OnValidate()
        {
            if (autoCollectChildSlots)
            {
                CollectChildSlots();
            }
        }

        private void LateUpdate()
        {
            if (autoRefresh)
            {
                RefreshCooldowns();
            }
        }

        /// <summary>
        /// 绑定小队管理器，并同步给所有动作槽。
        /// </summary>
        public void BindPartyManager(PartyManager manager)
        {
            UnsubscribeRouterEvents();
            partyManager = manager;
            BindSlots();
            SubscribeRouterEvents();
        }

        /// <summary>
        /// 立即刷新所有已配置的动作槽。
        /// </summary>
        public void RefreshNow()
        {
            RefreshSlot(playerSkillSlot);
            RefreshSlot(allySlotASkillSlot);
            RefreshSlot(allySlotBSkillSlot);
            RefreshSlot(playerLinkAttackSlot);
            RefreshSlot(allySlotALinkAttackSlot);
            RefreshSlot(allySlotBLinkAttackSlot);
            RefreshSlot(partyUltimateSlot);
        }

        /// <summary>
        /// 只刷新所有动作槽的冷却显示。
        /// 供 HUD 每帧驱动，避免每帧重复刷新键位文本和静态绑定信息。
        /// </summary>
        public void RefreshCooldowns()
        {
            RefreshSlotCooldown(playerSkillSlot);
            RefreshSlotCooldown(allySlotASkillSlot);
            RefreshSlotCooldown(allySlotBSkillSlot);
            RefreshSlotCooldown(playerLinkAttackSlot);
            RefreshSlotCooldown(allySlotALinkAttackSlot);
            RefreshSlotCooldown(allySlotBLinkAttackSlot);
            RefreshSlotCooldown(partyUltimateSlot);
        }

        /// <summary>
        /// 只刷新键位文本等静态槽位显示。
        /// </summary>
        public void RefreshStaticSlots()
        {
            RefreshSlotKeyLabel(playerSkillSlot);
            RefreshSlotKeyLabel(allySlotASkillSlot);
            RefreshSlotKeyLabel(allySlotBSkillSlot);
            RefreshSlotKeyLabel(playerLinkAttackSlot);
            RefreshSlotKeyLabel(allySlotALinkAttackSlot);
            RefreshSlotKeyLabel(allySlotBLinkAttackSlot);
            RefreshSlotKeyLabel(partyUltimateSlot);
        }


        /// <summary>
        /// 获取指定小队动作 UI 槽。
        /// </summary>
        public UICombatActionSlot GetSlot(UICombatActionSlotId slotId)
        {
            return slotId switch
            {
                UICombatActionSlotId.PlayerSkill => playerSkillSlot,
                UICombatActionSlotId.AllySlotASkill => allySlotASkillSlot,
                UICombatActionSlotId.AllySlotBSkill => allySlotBSkillSlot,
                UICombatActionSlotId.PlayerLinkAttack => playerLinkAttackSlot,
                UICombatActionSlotId.AllySlotALinkAttack => allySlotALinkAttackSlot,
                UICombatActionSlotId.AllySlotBLinkAttack => allySlotBLinkAttackSlot,
                UICombatActionSlotId.PartyUltimate => partyUltimateSlot,
                _ => null
            };
        }

        private void CacheReferences()
        {
            if (partyManager == null)
            {
                partyManager = FindFirstObjectByType<PartyManager>();
            }
        }

        private void CollectChildSlots()
        {
            UICombatActionSlot[] childSlots = GetComponentsInChildren<UICombatActionSlot>(true);
            for (int i = 0; i < childSlots.Length; i++)
            {
                UICombatActionSlot childSlot = childSlots[i];
                AssignSlotReference(childSlot);
            }
        }

        private void AssignSlotReference(UICombatActionSlot childSlot)
        {
            if (childSlot == null)
            {
                return;
            }

            switch (childSlot.Slot)
            {
                case UICombatActionSlotId.PlayerSkill:
                    playerSkillSlot ??= childSlot;
                    break;
                case UICombatActionSlotId.AllySlotASkill:
                    allySlotASkillSlot ??= childSlot;
                    break;
                case UICombatActionSlotId.AllySlotBSkill:
                    allySlotBSkillSlot ??= childSlot;
                    break;
                case UICombatActionSlotId.PlayerLinkAttack:
                    playerLinkAttackSlot ??= childSlot;
                    break;
                case UICombatActionSlotId.AllySlotALinkAttack:
                    allySlotALinkAttackSlot ??= childSlot;
                    break;
                case UICombatActionSlotId.AllySlotBLinkAttack:
                    allySlotBLinkAttackSlot ??= childSlot;
                    break;
                case UICombatActionSlotId.PartyUltimate:
                    partyUltimateSlot ??= childSlot;
                    break;
            }
        }

        private void BindSlots()
        {
            if (autoCollectChildSlots)
            {
                CollectChildSlots();
            }

            ConfigureSlot(playerSkillSlot, UICombatActionSlotId.PlayerSkill);
            ConfigureSlot(allySlotASkillSlot, UICombatActionSlotId.AllySlotASkill);
            ConfigureSlot(allySlotBSkillSlot, UICombatActionSlotId.AllySlotBSkill);
            ConfigureSlot(playerLinkAttackSlot, UICombatActionSlotId.PlayerLinkAttack);
            ConfigureSlot(allySlotALinkAttackSlot, UICombatActionSlotId.AllySlotALinkAttack);
            ConfigureSlot(allySlotBLinkAttackSlot, UICombatActionSlotId.AllySlotBLinkAttack);
            ConfigureSlot(partyUltimateSlot, UICombatActionSlotId.PartyUltimate);
        }

        private void ConfigureSlot(UICombatActionSlot actionSlot, UICombatActionSlotId slotId)
        {
            if (actionSlot == null)
            {
                return;
            }

            actionSlot.SetSlot(slotId);
            actionSlot.BindPartyManager(partyManager);

            if (driveChildSlotsManually)
            {
                actionSlot.SetAutoRefreshCooldown(false);
                actionSlot.SetAutoRefreshKeyLabel(false);
            }

            actionSlot.RefreshNow();
        }

        private void SubscribeRouterEvents()
        {
            PartyCombatRouter router = partyManager != null ? partyManager.CombatRouter : null;
            if (router == null || _subscribedRouter == router)
            {
                return;
            }

            UnsubscribeRouterEvents();
            _subscribedRouter = router;
            _subscribedRouter.KeyBindingsChanged += HandleKeyBindingsChanged;
        }

        private void UnsubscribeRouterEvents()
        {
            if (_subscribedRouter == null)
            {
                return;
            }

            _subscribedRouter.KeyBindingsChanged -= HandleKeyBindingsChanged;
            _subscribedRouter = null;
        }

        private void HandleKeyBindingsChanged()
        {
            RefreshStaticSlots();
        }

        private static void RefreshSlot(UICombatActionSlot actionSlot)
        {
            if (actionSlot != null)
            {
                actionSlot.RefreshNow();
            }
        }

        private static void RefreshSlotCooldown(UICombatActionSlot actionSlot)
        {
            if (actionSlot != null)
            {
                actionSlot.RefreshCooldown();
            }
        }

        private static void RefreshSlotKeyLabel(UICombatActionSlot actionSlot)
        {
            if (actionSlot != null)
            {
                actionSlot.RefreshKeyLabel();
            }
        }
    }
}
