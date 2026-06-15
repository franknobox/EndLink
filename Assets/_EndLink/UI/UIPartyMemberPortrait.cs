using EndLink.Party;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EndLink.UI
{
    /// <summary>
    /// 小队成员头像 UI。
    /// 头像同时承担成员状态提示和连携窗口提示：连携窗口开启时高亮，队友 Link Down 或主控死亡时灰化。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIPartyMemberPortrait : MonoBehaviour
    {
        [Header("小队绑定")]
        [Tooltip("小队管理器。为空时会在场景中自动查找。")]
        [SerializeField]
        private PartyManager partyManager;

        [Tooltip("小队连携窗口上下文。为空时会从 PartyManager 所在物体上获取。")]
        [SerializeField]
        private PartyLinkContext linkContext;

        [Tooltip("该头像对应的小队成员槽位。")]
        [SerializeField]
        private UIPartyMemberSlot memberSlot = UIPartyMemberSlot.MainCharacter;

        [Header("显示组件")]
        [Tooltip("头像主体 Image。白模阶段可以先使用圆形占位图。")]
        [SerializeField]
        private Image portraitImage;

        [Tooltip("连携窗口可用时显示的高亮 Image，通常是头像外圈或叠加圆。")]
        [SerializeField]
        private Image highlightImage;

        [Tooltip("连携键位文本。主控/队友头像分别显示 1/2/3。")]
        [SerializeField]
        private TextMeshProUGUI keyLabelText;

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

        [Tooltip("连携窗口开启时的高亮颜色。透明度会随窗口剩余比例变化。")]
        [SerializeField]
        private Color linkReadyColor = new(1f, 0.88f, 0.35f, 0.85f);

        [Header("刷新")]
        [Tooltip("是否每帧刷新连携窗口高亮和成员状态。连携窗口剩余比例是连续变化的显示，适合逐帧刷新。")]
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
            ResolveLinkContext();
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
        /// 立即刷新头像键位、颜色和高亮。
        /// </summary>
        public void RefreshNow()
        {
            CacheVisualReferences();
            RefreshKeyLabel();
            RefreshState();
        }

        /// <summary>
        /// 只刷新头像颜色和连携高亮。
        /// </summary>
        public void RefreshState()
        {
            CacheReferences();

            bool hasMember = HasMember();
            bool isAlive = IsMemberAlive();
            bool linkReady = IsLinkReadyForMember(hasMember, isAlive);

            if (portraitImage != null)
            {
                portraitImage.color = ResolvePortraitColor(hasMember, isAlive);
            }

            if (highlightImage != null)
            {
                Color highlightColor = linkReadyColor;
                highlightColor.a *= linkReady ? ResolveLinkHighlightAlpha() : 0f;
                highlightImage.color = highlightColor;
                highlightImage.enabled = highlightColor.a > 0.01f;
            }
        }

        /// <summary>
        /// 只刷新头像上的连携键位文本。
        /// </summary>
        public void RefreshKeyLabel()
        {
            if (keyLabelText == null)
            {
                return;
            }

            keyLabelText.text = ResolveKeyLabel();
        }

        private void CacheReferences()
        {
            if (partyManager == null)
            {
                partyManager = FindFirstObjectByType<PartyManager>();
            }

            ResolveLinkContext();
            CacheVisualReferences();
        }

        private void ResolveLinkContext()
        {
            if (linkContext != null)
            {
                return;
            }

            if (partyManager != null)
            {
                linkContext = partyManager.GetComponent<PartyLinkContext>();
            }
        }

        private void CacheVisualReferences()
        {
            if (portraitImage == null)
            {
                portraitImage = GetComponent<Image>();
            }

            if (keyLabelText == null)
            {
                keyLabelText = GetComponentInChildren<TextMeshProUGUI>(true);
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
                UIPartyMemberSlot.AllySlotA => partyManager.AllySlotA != null && partyManager.AllySlotA.HasAlly,
                UIPartyMemberSlot.AllySlotB => partyManager.AllySlotB != null && partyManager.AllySlotB.HasAlly,
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

        private bool IsLinkReadyForMember(bool hasMember, bool isAlive)
        {
            return hasMember && isAlive && linkContext != null && linkContext.IsWindowOpen;
        }

        private float ResolveLinkHighlightAlpha()
        {
            if (linkContext == null)
            {
                return 0f;
            }

            return Mathf.Lerp(0.35f, 1f, linkContext.RemainingNormalized);
        }

        private Color ResolvePortraitColor(bool hasMember, bool isAlive)
        {
            if (!hasMember)
            {
                return emptyColor;
            }

            return isAlive ? normalColor : linkDownColor;
        }

        private string ResolveKeyLabel()
        {
            PartyCombatRouter router = partyManager != null ? partyManager.CombatRouter : null;
            if (router != null)
            {
                return router.GetKeyLabelForCommand(PartyCombatCommandType.LinkAttack, ResolveActorSlot());
            }

            return memberSlot switch
            {
                UIPartyMemberSlot.MainCharacter => "1",
                UIPartyMemberSlot.AllySlotA => "2",
                UIPartyMemberSlot.AllySlotB => "3",
                _ => string.Empty
            };
        }

        private PartyCombatActorSlot ResolveActorSlot()
        {
            return memberSlot switch
            {
                UIPartyMemberSlot.MainCharacter => PartyCombatActorSlot.MainCharacter,
                UIPartyMemberSlot.AllySlotA => PartyCombatActorSlot.AllySlotA,
                UIPartyMemberSlot.AllySlotB => PartyCombatActorSlot.AllySlotB,
                _ => PartyCombatActorSlot.MainCharacter
            };
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
