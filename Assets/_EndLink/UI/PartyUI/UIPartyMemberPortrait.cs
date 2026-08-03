using EndLink.Party;
using UnityEngine;
using UnityEngine.UI;

namespace EndLink.UI
{
    /// <summary>
    /// 小队成员头像 UI。
    /// 当前归档版只保留成员可用状态提示：队友 Link Down 或主控死亡时灰化。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIPartyMemberPortrait : MonoBehaviour
    {
        [Header("小队绑定")]
        [Tooltip("小队管理器。为空时会在场景中自动查找。")]
        [SerializeField]
        private PartyManager partyManager;

        [Tooltip("该头像对应的小队成员槽位。")]
        [SerializeField]
        private UIPartyMemberSlot memberSlot = UIPartyMemberSlot.MainCharacter;

        [Header("显示组件")]
        [Tooltip("头像主体 Image。白模阶段可以先使用圆形占位图。")]
        [SerializeField]
        private Image portraitImage;

        [Header("颜色")]
        [Tooltip("成员正常可用时的头像颜色。")]
        [SerializeField]
        private Color normalColor = new(0.82f, 0.82f, 0.82f, 1f);

        [Tooltip("成员失去连接或死亡时的头像颜色。")]
        [SerializeField]
        private Color linkDownColor = new(0.35f, 0.35f, 0.35f, 0.65f);

        [Tooltip("未配置成员时的头像颜色。Prefab 预览阶段也会使用这个颜色。")]
        [SerializeField]
        private Color emptyColor = new(0.45f, 0.45f, 0.45f, 0.45f);

        [Header("刷新")]
        [Tooltip("是否每帧刷新成员状态。")]
        [SerializeField]
        private bool autoRefresh = true;

        /// <summary>当前头像对应的小队成员槽位。</summary>
        public UIPartyMemberSlot MemberSlot => memberSlot;

        private void Awake()
        {
            CacheReferences();
            RefreshNow();
        }

        private void Reset()
        {
            CacheReferences();
        }

        private void OnValidate()
        {
            CacheVisualReferences();
            RefreshNow();
        }

        private void Update()
        {
            if (autoRefresh)
            {
                RefreshState();
            }
        }

        /// <summary>
        /// 绑定小队管理器，并刷新头像状态。
        /// </summary>
        public void BindPartyManager(PartyManager manager)
        {
            partyManager = manager;
            RefreshNow();
        }

        /// <summary>
        /// 设置该头像对应的小队成员槽位。
        /// </summary>
        public void SetMemberSlot(UIPartyMemberSlot nextMemberSlot)
        {
            memberSlot = nextMemberSlot;
            RefreshNow();
        }

        /// <summary>
        /// 立即刷新头像颜色。
        /// </summary>
        public void RefreshNow()
        {
            CacheVisualReferences();
            RefreshState();
        }

        /// <summary>
        /// 只刷新头像颜色。
        /// </summary>
        public void RefreshState()
        {
            CacheReferences();

            bool hasMember = HasMember();
            bool isAlive = IsMemberAlive();
            if (portraitImage != null)
            {
                portraitImage.color = ResolvePortraitColor(hasMember, isAlive);
            }
        }

        private void CacheReferences()
        {
            if (partyManager == null)
            {
                partyManager = FindFirstObjectByType<PartyManager>();
            }

            CacheVisualReferences();
        }

        private void CacheVisualReferences()
        {
            if (portraitImage == null)
            {
                portraitImage = GetComponent<Image>();
            }

        }

        private bool HasMember()
        {
            if (partyManager == null)
            {
                return true;
            }

            return memberSlot switch
            {
                UIPartyMemberSlot.MainCharacter => partyManager.MainCharacter != null,
                UIPartyMemberSlot.AllySlotA => partyManager.AllySlotA != null && partyManager.AllySlotA.IsActive,
                UIPartyMemberSlot.AllySlotB => partyManager.AllySlotB != null && partyManager.AllySlotB.IsActive,
                _ => false
            };
        }

        private bool IsMemberAlive()
        {
            if (partyManager == null)
            {
                return true;
            }

            return memberSlot switch
            {
                UIPartyMemberSlot.MainCharacter => partyManager.IsMainCharacterAlive,
                UIPartyMemberSlot.AllySlotA => partyManager.AllySlotA != null && partyManager.AllySlotA.IsAlive,
                UIPartyMemberSlot.AllySlotB => partyManager.AllySlotB != null && partyManager.AllySlotB.IsAlive,
                _ => false
            };
        }

        private Color ResolvePortraitColor(bool hasMember, bool isAlive)
        {
            if (!hasMember)
            {
                return emptyColor;
            }

            return isAlive ? normalColor : linkDownColor;
        }

    }

    /// <summary>战斗 HUD 中的小队成员头像槽位。</summary>
    public enum UIPartyMemberSlot
    {
        MainCharacter = 0,
        AllySlotA = 1,
        AllySlotB = 2
    }
}
