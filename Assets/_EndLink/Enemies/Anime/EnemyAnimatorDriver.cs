using System.Collections.Generic;
using EndLink.Combat;
using EndLink.Core;
using UnityEngine;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人玩法状态到 Animator 的轻量桥接层。
    /// 同步移动、敌人大状态和动作触发信息，并确保 Animator 能把 Clip 事件转发给战斗 Driver。
    /// 不负责 AI 决策、动作合法性或具体 Hitbox 生成。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyStateMachine))]
    public sealed class EnemyAnimatorDriver : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("敌人 Animator。为空时优先从 EnemyActor 的 Body Root 下查找，再从当前敌人的子物体中查找。")]
        [SerializeField]
        private Animator animator;

        [Tooltip("敌人移动能力。为空时自动读取 EnemyActor 或当前物体上的 EnemyMotorBase；无移动能力的敌人可以留空。")]
        [SerializeField]
        private EnemyMotorBase motor;

        [Tooltip("敌人战斗执行器。为空时自动读取 EnemyActor 或当前物体上的 EnemyCombatDriver；不攻击的敌人可以留空。")]
        [SerializeField]
        private EnemyCombatDriver combatDriver;

        [Header("连续参数")]
        [Tooltip("当前水平移动速度使用的 Float 参数名。为空则不写入。")]
        [SerializeField]
        private string moveSpeedParameter = CombatAnimatorParams.MoveSpeed;

        [Tooltip("当前是否正在移动使用的 Bool 参数名。为空则不写入。")]
        [SerializeField]
        private string isMovingParameter = CombatAnimatorParams.IsMoving;

        [Tooltip("当前移动是否属于战斗观察或攻击准备机动使用的 Bool 参数名。可用于在追击 Run 和观察横移 Move 之间切换。")]
        [SerializeField]
        private string isCombatManeuverParameter = CombatAnimatorParams.IsCombatManeuver;

        [Tooltip("当前敌人大状态 ID 使用的 Int 参数名。数值对应 EnemyStateId。为空则不写入。")]
        [SerializeField]
        private string stateIdParameter = CombatAnimatorParams.StateId;

        [Tooltip("当前是否死亡使用的 Bool 参数名。为空则不写入。")]
        [SerializeField]
        private string isDeadParameter = CombatAnimatorParams.IsDead;

        [Header("动作参数")]
        [Tooltip("动作标识使用的 Int 参数名。写入 ActionId 字符串的稳定 Animator Hash。为空则不写入。")]
        [SerializeField]
        private string actionIdParameter = CombatAnimatorParams.ActionId;

        [Tooltip("动作类型使用的 Int 参数名。数值对应 CombatActionType。为空则不写入。")]
        [SerializeField]
        private string actionTypeParameter = CombatAnimatorParams.ActionType;

        [Tooltip("敌人成功开始一次战斗动作时触发的 Trigger 参数名。为空则不触发。")]
        [SerializeField]
        private string actionTriggerParameter = CombatAnimatorParams.ActionTrigger;

        [Header("状态触发器")]
        [Tooltip("进入 Hit 状态时触发的 Trigger 参数名。为空则不触发。")]
        [SerializeField]
        private string hitTriggerParameter = CombatAnimatorParams.HitTrigger;

        [Tooltip("进入 Dead 状态时触发的 Trigger 参数名。为空则不触发。")]
        [SerializeField]
        private string deadTriggerParameter = CombatAnimatorParams.DeadTrigger;

        [Header("调试")]
        [Tooltip("Animator 缺少参数时是否打印一次警告。接入正式 Animator Controller 时可开启检查协议。")]
        [SerializeField]
        private bool warnMissingParameters;

        private readonly HashSet<int> _parameterHashes = new();
        private readonly HashSet<int> _warnedParameterHashes = new();
        private EnemyStateMachine _stateMachine;
        private RuntimeAnimatorController _cachedController;
        private EnemyStateId _lastStateId = EnemyStateId.None;

        /// <summary>当前桥接的 Animator。</summary>
        public Animator Animator => animator;

        /// <summary>最近一次同步到 Animator 的敌人大状态。</summary>
        public EnemyStateId LastStateId => _lastStateId;

        private void Awake()
        {
            ResolveReferences();
            RebuildParameterCache();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeCombatDriver();
            _lastStateId = EnemyStateId.None;
        }

        private void OnDisable()
        {
            UnsubscribeCombatDriver();
        }

        private void OnValidate()
        {
            if (animator != null)
            {
                RebuildParameterCache();
            }
        }

        private void Update()
        {
            RefreshAnimatorParameters();
        }

        /// <summary>
        /// 运行时替换视觉模型或 Animator 后重新绑定。
        /// </summary>
        public void SetAnimator(Animator targetAnimator)
        {
            animator = targetAnimator;
            EnsureAnimationEventReceiver();
            RebuildParameterCache();
        }

        /// <summary>
        /// 立即把当前移动和状态信息同步到 Animator。
        /// </summary>
        public void RefreshAnimatorParameters()
        {
            if (animator == null || _stateMachine == null)
            {
                return;
            }

            if (animator.runtimeAnimatorController != _cachedController)
            {
                RebuildParameterCache();
            }

            EnemyStateId currentStateId = _stateMachine.CurrentStateId;
            float currentSpeed = motor != null ? motor.CurrentSpeed : 0f;
            bool isMoving = motor != null && motor.IsMoving;
            bool isCombatManeuver = currentStateId == EnemyStateId.Combat
                && isMoving
                && IsCombatManeuverPhase(_stateMachine.CurrentCombatPhase);

            SetFloatIfExists(moveSpeedParameter, currentSpeed);
            SetBoolIfExists(isMovingParameter, isMoving);
            SetBoolIfExists(isCombatManeuverParameter, isCombatManeuver);
            SetIntegerIfExists(stateIdParameter, (int)currentStateId);
            SetBoolIfExists(isDeadParameter, currentStateId == EnemyStateId.Dead);

            if (currentStateId != _lastStateId)
            {
                TriggerStateEnter(currentStateId);
                _lastStateId = currentStateId;
            }
        }

        private void ResolveReferences()
        {
            _stateMachine ??= GetComponent<EnemyStateMachine>();
            EnemyActor actor = GetComponent<EnemyActor>();

            if (motor == null)
            {
                motor = actor != null && actor.Motor != null
                    ? actor.Motor
                    : GetComponent<EnemyMotorBase>();
            }

            if (combatDriver == null)
            {
                combatDriver = actor != null && actor.CombatDriver != null
                    ? actor.CombatDriver
                    : GetComponent<EnemyCombatDriver>();
            }

            if (animator == null)
            {
                Transform visualRoot = actor != null ? actor.BodyRoot : null;
                animator = visualRoot != null
                    ? visualRoot.GetComponentInChildren<Animator>(true)
                    : GetComponentInChildren<Animator>(true);
            }

            EnsureAnimationEventReceiver();
        }

        private void EnsureAnimationEventReceiver()
        {
            if (animator == null)
            {
                return;
            }

            if (!animator.TryGetComponent(out CombatAnimationEventReceiver receiver))
            {
                receiver = animator.gameObject.AddComponent<CombatAnimationEventReceiver>();
            }

            receiver.RefreshListeners();
        }

        private void SubscribeCombatDriver()
        {
            if (combatDriver != null)
            {
                combatDriver.ActionStarted -= HandleActionStarted;
                combatDriver.ActionStarted += HandleActionStarted;
            }
        }

        private void UnsubscribeCombatDriver()
        {
            if (combatDriver != null)
            {
                combatDriver.ActionStarted -= HandleActionStarted;
            }
        }

        private void HandleActionStarted(CombatActionDefinition actionDefinition)
        {
            if (animator == null || actionDefinition == null)
            {
                return;
            }

            if (animator.runtimeAnimatorController != _cachedController)
            {
                RebuildParameterCache();
            }

            int actionId = string.IsNullOrWhiteSpace(actionDefinition.ActionId)
                ? 0
                : Animator.StringToHash(actionDefinition.ActionId);

            SetIntegerIfExists(actionIdParameter, actionId);
            SetIntegerIfExists(actionTypeParameter, (int)actionDefinition.ActionType);
            SetTriggerIfExists(actionTriggerParameter);
        }

        private static bool IsCombatManeuverPhase(EnemyCombatPhase phase)
        {
            return phase == EnemyCombatPhase.Position
                || phase == EnemyCombatPhase.Prepare;
        }

        private void TriggerStateEnter(EnemyStateId stateId)
        {
            switch (stateId)
            {
                case EnemyStateId.Hit:
                    SetTriggerIfExists(hitTriggerParameter);
                    break;
                case EnemyStateId.Dead:
                    SetTriggerIfExists(deadTriggerParameter);
                    break;
            }
        }

        private void RebuildParameterCache()
        {
            _parameterHashes.Clear();
            _warnedParameterHashes.Clear();
            _cachedController = animator != null ? animator.runtimeAnimatorController : null;

            if (animator == null)
            {
                return;
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                _parameterHashes.Add(parameter.nameHash);
            }
        }

        private void SetFloatIfExists(string parameterName, float value)
        {
            if (TryGetParameterHash(parameterName, out int hash))
            {
                animator.SetFloat(hash, value);
            }
        }

        private void SetBoolIfExists(string parameterName, bool value)
        {
            if (TryGetParameterHash(parameterName, out int hash))
            {
                animator.SetBool(hash, value);
            }
        }

        private void SetIntegerIfExists(string parameterName, int value)
        {
            if (TryGetParameterHash(parameterName, out int hash))
            {
                animator.SetInteger(hash, value);
            }
        }

        private void SetTriggerIfExists(string parameterName)
        {
            if (TryGetParameterHash(parameterName, out int hash))
            {
                animator.SetTrigger(hash);
            }
        }

        private bool TryGetParameterHash(string parameterName, out int hash)
        {
            hash = 0;

            if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            hash = Animator.StringToHash(parameterName);
            if (_parameterHashes.Contains(hash))
            {
                return true;
            }

            WarnMissingParameter(parameterName, hash);
            return false;
        }

        private void WarnMissingParameter(string parameterName, int hash)
        {
            if (!warnMissingParameters || !_warnedParameterHashes.Add(hash))
            {
                return;
            }

            Debug.LogWarning($"EnemyAnimatorDriver 找不到 Animator 参数：{parameterName}", this);
        }
    }
}
