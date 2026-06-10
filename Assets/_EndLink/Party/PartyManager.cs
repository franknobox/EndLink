using System;
using System.Collections.Generic;
using EndLink.Ally;
using EndLink.Combat;
using UnityEngine;
using UnityEngine.Events;

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
        [Tooltip("第一个队友槽位，推荐偏移为左后方，例如 (-2, 0, -2.5)。")]
        [SerializeField]
        private PartyFormationSlot allySlotA = new();

        [Tooltip("第二个队友槽位，推荐偏移为右后方，例如 (2, 0, -2.5)。")]
        [SerializeField]
        private PartyFormationSlot allySlotB = new();

        [Header("动态站位")]
        [Tooltip("开启后，两个队友不再固定左后/右后位置，而是根据当前位置自动选择移动代价更低的站位。")]
        [SerializeField]
        private bool useDynamicFormationSlots = true;

        [Tooltip("动态站位重新评估间隔。数值越小越灵敏，但过低会让队友频繁抢位。")]
        [SerializeField, Min(0.05f)]
        private float formationEvaluateInterval = 0.35f;

        [Tooltip("新分配方案至少比当前方案少移动多少米，才允许交换站位。用于避免来回抖动。")]
        [SerializeField, Min(0f)]
        private float formationSwitchMinImprovement = 1f;

        [Tooltip("一次站位交换后，至少等待多久才允许下一次交换。")]
        [SerializeField, Min(0f)]
        private float formationSwitchCooldown = 1.2f;

        [Header("队友跟随参数")]
        [Tooltip("两个固定队友共用的跟随移动参数。PartyManager 会在初始化小队时写入各自的 AllyFollowMotor。")]
        [SerializeField]
        private PartyFollowSettings followSettings = new();

        [Header("战斗路由")]
        [Tooltip("小队战斗命令路由器。UI 和小队战斗系统通过这里读取当前键位路由。为空时会在场景中自动查找。")]
        [SerializeField]
        private PartyCombatRouter combatRouter;

        [Header("小队表现调度")]
        [Tooltip("小队进入战斗时触发。UI、镜头、语音和站位表现可以监听这里。")]
        [SerializeField]
        private UnityEvent onCombatStarted = new();

        [Tooltip("小队退出战斗时触发。")]
        [SerializeField]
        private UnityEvent onCombatEnded = new();

        [Tooltip("主控死亡或队友链接中断时触发，参数为成员根物体。")]
        [SerializeField]
        private PartyMemberEvent onMemberDead = new();

        [Header("调试")]
        [Tooltip("初始化小队时打印主控和队友槽位信息。")]
        [SerializeField]
        private bool logInitialization;

        private bool _formationSlotsSwapped;
        private float _nextFormationEvaluateTime;
        private float _nextFormationSwitchTime;

        /// <summary>小队进入战斗的代码事件。</summary>
        public event Action CombatStarted;

        /// <summary>小队退出战斗的代码事件。</summary>
        public event Action CombatEnded;

        /// <summary>主控死亡或队友链接中断的代码事件。</summary>
        public event Action<GameObject> MemberDead;

        /// <summary>固定主控角色。</summary>
        public Transform MainCharacter => mainCharacter;

        /// <summary>第一个队友槽位。</summary>
        public PartyFormationSlot AllySlotA => allySlotA;

        /// <summary>第二个队友槽位。</summary>
        public PartyFormationSlot AllySlotB => allySlotB;

        /// <summary>两个固定队友共用的跟随移动参数。</summary>
        public PartyFollowSettings FollowSettings => followSettings;

        /// <summary>当前是否启用动态站位槽位交换。</summary>
        public bool UseDynamicFormationSlots => useDynamicFormationSlots;

        /// <summary>是否已经配置固定主控。</summary>
        public bool HasMainCharacter => mainCharacter != null;

        /// <summary>固定主控当前是否可视为存活。</summary>
        public bool IsMainCharacterAlive =>
            mainCharacter != null && (!TryGetMainCharacterHealth(out CharacterHealth health) || !health.IsDead);

        /// <summary>已经配置到槽位上的队友数量，不判断 LinkDown / 死亡。</summary>
        public int ConfiguredAllyCount => CountConfiguredAllies();

        /// <summary>当前有效队友数量。定义为：已配置，并且未进入 LinkDown / 死亡状态。</summary>
        public int ActiveAllyCount => CountActiveAllies();

        /// <summary>当前存活队友数量，和 ActiveAllyCount 含义一致，方便调用方按语义选择。</summary>
        public int AliveAllyCount => ActiveAllyCount;

        /// <summary>小队进入战斗的 Inspector 事件。</summary>
        public UnityEvent OnCombatStarted => onCombatStarted;

        /// <summary>小队退出战斗的 Inspector 事件。</summary>
        public UnityEvent OnCombatEnded => onCombatEnded;

        /// <summary>主控死亡或队友链接中断的 Inspector 事件。</summary>
        public PartyMemberEvent OnMemberDead => onMemberDead;

        /// <summary>小队战斗命令路由器。</summary>
        public PartyCombatRouter CombatRouter
        {
            get
            {
                if (combatRouter == null)
                {
                    CacheReferences();
                }

                return combatRouter;
            }
        }

        private void Awake()
        {
            CacheReferences();
            EnsureFollowSettings();
            followSettings.Normalize();
        }

        private void OnEnable()
        {
            CombatEventsBus.Raised += HandleCombatEvent;
        }

        private void OnDisable()
        {
            CombatEventsBus.Raised -= HandleCombatEvent;
        }

        private void Reset()
        {
            CacheReferences();
            EnsureFollowSettings();
        }

        private void OnValidate()
        {
            EnsureFollowSettings();
            followSettings.Normalize();
            formationEvaluateInterval = Mathf.Max(0.05f, formationEvaluateInterval);
            formationSwitchMinImprovement = Mathf.Max(0f, formationSwitchMinImprovement);
            formationSwitchCooldown = Mathf.Max(0f, formationSwitchCooldown);

            if (Application.isPlaying)
            {
                ApplyFollowSettingsToAllies();
            }
        }

        private void Start()
        {
            InitializeParty();
        }

        private void LateUpdate()
        {
            EvaluateDynamicFormationSlots();
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

            EnsureFollowSettings();
            followSettings.Normalize();
            _formationSlotsSwapped = false;
            _nextFormationEvaluateTime = Time.time + formationEvaluateInterval;
            _nextFormationSwitchTime = Time.time;
            ApplySlot(allySlotA);
            ApplySlot(allySlotB);
            ApplyFormationAssignments();
        }

        /// <summary>
        /// 获取当前已配置的队友状态机。
        /// Ally AI、连携规则或调试工具需要查询队友列表时，可以先走这里。
        /// </summary>
        public IReadOnlyList<AllyStateMachine> GetAllies(List<AllyStateMachine> results)
        {
            results.Clear();

            AddAllyIfValid(results, allySlotA);
            AddAllyIfValid(results, allySlotB);

            return results;
        }

        /// <summary>
        /// 获取当前存活的队友状态机。
        /// 队友 AI、战斗 UI 和连携规则需要“可参与战斗的队友”时，优先用这个接口。
        /// </summary>
        public IReadOnlyList<AllyStateMachine> GetAliveAllies(List<AllyStateMachine> results)
        {
            results.Clear();

            AddAllyIfAlive(results, allySlotA);
            AddAllyIfAlive(results, allySlotB);

            return results;
        }

        /// <summary>判断指定队友是否属于当前小队，并且当前未进入 LinkDown / 死亡状态。</summary>
        public bool IsAllyAlive(AllyStateMachine ally)
        {
            if (ally == null)
            {
                return false;
            }

            return IsSlotAllyAlive(allySlotA, ally) || IsSlotAllyAlive(allySlotB, ally);
        }

        /// <summary>尝试获取主控角色的通用生命组件。</summary>
        public bool TryGetMainCharacterHealth(out CharacterHealth health)
        {
            health = null;

            if (mainCharacter == null)
            {
                return false;
            }

            return mainCharacter.TryGetComponent(out health);
        }

        /// <summary>
        /// 通知小队进入战斗表现状态。
        /// 它只负责广播，不决定战斗规则；UI、镜头、队友语音、站位表现都可以监听这里。
        /// </summary>
        public void NotifyCombatStarted()
        {
            onCombatStarted.Invoke();
            CombatStarted?.Invoke();
        }

        /// <summary>
        /// 通知小队退出战斗表现状态。
        /// </summary>
        public void NotifyCombatEnded()
        {
            onCombatEnded.Invoke();
            CombatEnded?.Invoke();
        }

        /// <summary>
        /// 通知主控死亡或队友链接中断。
        /// 参数使用成员根物体，方便 UI、镜头、语音和队伍槽位系统各自解析。
        /// </summary>
        public void NotifyMemberDead(GameObject member)
        {
            if (member == null)
            {
                return;
            }

            onMemberDead.Invoke(member);
            MemberDead?.Invoke(member);
        }

        private void HandleCombatEvent(CombatEvent eventData)
        {
            if (eventData.EventType != CombatEventType.Dead || eventData.Target == null)
            {
                return;
            }

            Transform deadTarget = eventData.Target.transform;
            if (IsSameOrChild(deadTarget, mainCharacter) || IsSlotMember(allySlotA, deadTarget) || IsSlotMember(allySlotB, deadTarget))
            {
                NotifyMemberDead(eventData.Target);
            }
        }

        private void ApplySlot(PartyFormationSlot slot)
        {
            if (slot == null || !slot.HasAlly)
            {
                return;
            }

            ApplyFollowSettings(slot);
            slot.Apply(mainCharacter);

            if (logInitialization)
            {
                Debug.Log(
                    $"初始化队友槽位：{slot.SlotName} -> {slot.AllyStateMachine.name}, offset={slot.FormationOffset}",
                    this);
            }
        }

        private void EvaluateDynamicFormationSlots()
        {
            if (!useDynamicFormationSlots || mainCharacter == null || Time.time < _nextFormationEvaluateTime)
            {
                return;
            }

            _nextFormationEvaluateTime = Time.time + formationEvaluateInterval;

            if (!CanEvaluateFormationSwap())
            {
                return;
            }

            Vector3 offsetA = allySlotA.FormationOffset;
            Vector3 offsetB = allySlotB.FormationOffset;
            Vector3 worldA = CalculateFormationWorldPosition(offsetA);
            Vector3 worldB = CalculateFormationWorldPosition(offsetB);

            Transform allyA = allySlotA.AllyTransform;
            Transform allyB = allySlotB.AllyTransform;

            float currentCost;
            float swappedCost;
            if (_formationSlotsSwapped)
            {
                currentCost = CalculateFormationCost(allyA, worldB, allyB, worldA);
                swappedCost = CalculateFormationCost(allyA, worldA, allyB, worldB);
            }
            else
            {
                currentCost = CalculateFormationCost(allyA, worldA, allyB, worldB);
                swappedCost = CalculateFormationCost(allyA, worldB, allyB, worldA);
            }

            if (Time.time < _nextFormationSwitchTime || swappedCost + formationSwitchMinImprovement >= currentCost)
            {
                return;
            }

            _formationSlotsSwapped = !_formationSlotsSwapped;
            _nextFormationSwitchTime = Time.time + formationSwitchCooldown;
            ApplyFormationAssignments();

            if (logInitialization)
            {
                Debug.Log(
                    $"动态交换队友站位：swapped={_formationSlotsSwapped}, currentCost={currentCost:F2}, swappedCost={swappedCost:F2}",
                    this);
            }
        }

        private void ApplyFollowSettingsToAllies()
        {
            ApplyFollowSettings(allySlotA);
            ApplyFollowSettings(allySlotB);
        }

        private void ApplyFollowSettings(PartyFormationSlot slot)
        {
            if (slot == null || !slot.HasAlly)
            {
                return;
            }

            AllyFollowMotor followMotor = slot.AllyStateMachine.FollowMotor;
            if (followMotor != null)
            {
                followMotor.ApplySettings(followSettings);
            }
        }

        private void ApplyFormationAssignments()
        {
            if (allySlotA == null || allySlotB == null)
            {
                return;
            }

            Vector3 offsetForA = _formationSlotsSwapped ? allySlotB.FormationOffset : allySlotA.FormationOffset;
            Vector3 offsetForB = _formationSlotsSwapped ? allySlotA.FormationOffset : allySlotB.FormationOffset;

            ApplyFormationOffset(allySlotA, offsetForA);
            ApplyFormationOffset(allySlotB, offsetForB);
        }

        private static void ApplyFormationOffset(PartyFormationSlot slot, Vector3 offset)
        {
            if (slot == null || !slot.HasAlly)
            {
                return;
            }

            AllyFollowMotor followMotor = slot.AllyStateMachine.FollowMotor;
            if (followMotor != null)
            {
                followMotor.SetFormationOffset(offset);
            }
        }

        private static void AddAllyIfValid(List<AllyStateMachine> results, PartyFormationSlot slot)
        {
            if (slot != null && slot.HasAlly)
            {
                results.Add(slot.AllyStateMachine);
            }
        }

        private static void AddAllyIfAlive(List<AllyStateMachine> results, PartyFormationSlot slot)
        {
            if (slot != null && slot.IsAlive)
            {
                results.Add(slot.AllyStateMachine);
            }
        }

        private int CountConfiguredAllies()
        {
            int count = 0;

            if (allySlotA != null && allySlotA.HasAlly)
            {
                count++;
            }

            if (allySlotB != null && allySlotB.HasAlly)
            {
                count++;
            }

            return count;
        }

        private int CountActiveAllies()
        {
            int count = 0;

            if (allySlotA != null && allySlotA.IsAlive)
            {
                count++;
            }

            if (allySlotB != null && allySlotB.IsAlive)
            {
                count++;
            }

            return count;
        }

        private static bool IsSlotAllyAlive(PartyFormationSlot slot, AllyStateMachine ally)
        {
            return slot != null && slot.AllyStateMachine == ally && slot.IsAlive;
        }

        private static bool IsSlotMember(PartyFormationSlot slot, Transform target)
        {
            return slot != null && IsSameOrChild(target, slot.AllyTransform);
        }

        private static bool IsSameOrChild(Transform target, Transform root)
        {
            return root != null && target != null && (target == root || target.IsChildOf(root));
        }

        private bool CanEvaluateFormationSwap()
        {
            return allySlotA != null
                && allySlotB != null
                && allySlotA.IsAlive
                && allySlotB.IsAlive
                && allySlotA.AllyTransform != null
                && allySlotB.AllyTransform != null
                && !IsFollowTargetInsideDeadZone(allySlotA)
                && !IsFollowTargetInsideDeadZone(allySlotB);
        }

        private static bool IsFollowTargetInsideDeadZone(PartyFormationSlot slot)
        {
            if (slot == null || !slot.HasAlly)
            {
                return false;
            }

            AllyFollowMotor followMotor = slot.AllyStateMachine.FollowMotor;
            return followMotor != null && followMotor.IsFollowTargetInsideDeadZone;
        }

        private Vector3 CalculateFormationWorldPosition(Vector3 localOffset)
        {
            Vector3 worldOffset = mainCharacter.TransformDirection(localOffset);
            worldOffset.y = 0f;

            Vector3 worldPosition = mainCharacter.position + worldOffset;
            worldPosition.y = mainCharacter.position.y;
            return worldPosition;
        }

        private static float CalculateFormationCost(
            Transform allyA,
            Vector3 targetA,
            Transform allyB,
            Vector3 targetB)
        {
            return CalculateFlatDistance(allyA.position, targetA) + CalculateFlatDistance(allyB.position, targetB);
        }

        private static float CalculateFlatDistance(Vector3 from, Vector3 to)
        {
            from.y = 0f;
            to.y = 0f;
            return Vector3.Distance(from, to);
        }

        private void CacheReferences()
        {
            if (combatRouter == null)
            {
                combatRouter = FindFirstObjectByType<PartyCombatRouter>();
            }
        }

        private void EnsureFollowSettings()
        {
            followSettings ??= new PartyFollowSettings();
        }
    }

    /// <summary>小队成员事件，参数为成员根物体。</summary>
    [Serializable]
    public sealed class PartyMemberEvent : UnityEvent<GameObject>
    {
    }
}
