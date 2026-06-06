using EndLink.Core;
using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 玩家战斗驱动器。
    /// 只保存玩家可释放的动作槽位，并按照 CombatActionDefinition 执行动作表现和 Hitbox 判定。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCombatDriver : MonoBehaviour
    {
        [Header("动作槽位")]
        [Tooltip("玩家普攻动作。鼠标左键会由玩家状态机触发该动作。")]
        [SerializeField]
        private CombatActionDefinition basicAttackAction;

        [Tooltip("玩家主动技能动作。后续由 PartyCombatRouter 的 Q 命令触发。")]
        [SerializeField]
        private CombatActionDefinition skillAction;

        [Tooltip("玩家连携技动作配置。不能被普通输入直接释放，必须由后续连携机制确认窗口后调用。")]
        [SerializeField]
        private CombatActionDefinition linkAction;

        private PlayerTargeting _targeting;
        private PlayerController _playerController;
        private float _nextActionTime;
        private float _lastActionCooldown;
        private CombatActionDefinition _lastCooldownAction;

        /// <summary>玩家普攻动作。</summary>
        public CombatActionDefinition BasicAttackAction => basicAttackAction;

        /// <summary>玩家主动技能动作。</summary>
        public CombatActionDefinition SkillAction => skillAction;

        /// <summary>玩家连携技动作配置。实际释放必须由连携机制授权。</summary>
        public CombatActionDefinition LinkAction => linkAction;

        /// <summary>当前是否可以释放下一次动作。</summary>
        public bool CanAttack => Time.time >= _nextActionTime;

        /// <summary>当前动作冷却剩余时间，单位秒。</summary>
        public float ActionCooldownRemaining => Mathf.Max(0f, _nextActionTime - Time.time);

        /// <summary>最近一次成功执行动作写入的冷却总时长，单位秒。</summary>
        public float ActionCooldownDuration => _lastActionCooldown;

        /// <summary>当前动作冷却归一化进度，1 表示刚进入冷却，0 表示冷却结束。</summary>
        public float ActionCooldownNormalized
        {
            get
            {
                return _lastActionCooldown > 0f
                    ? Mathf.Clamp01(ActionCooldownRemaining / _lastActionCooldown)
                    : 0f;
            }
        }

        /// <summary>当前是否处于动作冷却中。</summary>
        public bool IsActionCoolingDown => ActionCooldownRemaining > 0f;

        /// <summary>
        /// 查询指定动作当前的冷却归一化进度。
        /// 当前第一版玩家只有一个动作锁，但 UI 需要知道“这个槽位自己的动作”是否在冷却，避免普攻冷却染灰技能槽。
        /// </summary>
        public float GetActionCooldownNormalized(CombatActionDefinition actionDefinition)
        {
            if (actionDefinition == null || _lastCooldownAction != actionDefinition)
            {
                return 0f;
            }

            return ActionCooldownNormalized;
        }

        private void Awake()
        {
            TryGetComponent(out _targeting);
            TryGetComponent(out _playerController);
        }

        /// <summary>
        /// 执行普攻动作。
        /// </summary>
        public bool ExecuteAttack()
        {
            return ExecuteAction(basicAttackAction);
        }

        /// <summary>
        /// 判断指定动作当前是否具备最基础的执行条件。
        /// 状态机可以用它决定是否接受动作请求，避免请求成功后才发现动作仍在冷却或缺少 Hitbox。
        /// </summary>
        public bool CanExecuteAction(CombatActionDefinition actionDefinition)
        {
            return actionDefinition != null
                && actionDefinition.HitboxPrefab != null
                && CanAttack;
        }

        /// <summary>
        /// 执行指定玩家动作。
        /// 该方法不判断玩家状态机是否允许出手，只负责动作资源、冷却和 Hitbox 执行。
        /// </summary>
        public bool ExecuteAction(CombatActionDefinition actionDefinition, Transform targetOverride = null)
        {
            if (actionDefinition == null)
            {
                Debug.LogWarning("PlayerCombatDriver 缺少动作配置，无法执行动作。", this);
                return false;
            }

            if (!CanAttack)
            {
                return false;
            }

            if (actionDefinition.HitboxPrefab == null)
            {
                Debug.LogWarning($"PlayerCombatDriver 的动作 {actionDefinition.ActionId} 缺少 Hitbox Prefab。", this);
                return false;
            }

            Vector3 attackForward = ResolveAttackForward(targetOverride);
            FaceAttackDirection(attackForward);
            Vector3 hitboxForward = ResolveCurrentForward(attackForward);
            Vector3 spawnPosition = transform.position
                + hitboxForward * actionDefinition.HitboxSpawnDistance
                + Vector3.up * actionDefinition.HitboxSpawnHeight;
            Quaternion spawnRotation = Quaternion.LookRotation(hitboxForward, Vector3.up);

            GameObject hitboxInstance = Instantiate(actionDefinition.HitboxPrefab, spawnPosition, spawnRotation);
            ConfigureHitbox(hitboxInstance, actionDefinition);

            GameObject actionTarget = targetOverride != null
                ? targetOverride.gameObject
                : GetCurrentTargetObject();
            CombatEventsBus.RaiseActionStarted(gameObject, actionTarget, actionDefinition);
            _lastCooldownAction = actionDefinition;
            _lastActionCooldown = actionDefinition.Cooldown;
            _nextActionTime = Time.time + _lastActionCooldown;
            return true;
        }

        private void FaceAttackDirection(Vector3 attackForward)
        {
            if (_playerController == null)
            {
                TryGetComponent(out _playerController);
            }

            if (_playerController != null)
            {
                _playerController.FaceDirection(attackForward, true);
                return;
            }

            if (attackForward.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(attackForward, Vector3.up);
            }
        }

        private Vector3 ResolveCurrentForward(Vector3 fallbackForward)
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude > 0.0001f)
            {
                return forward.normalized;
            }

            return fallbackForward.sqrMagnitude > 0.0001f
                ? fallbackForward.normalized
                : Vector3.forward;
        }

        private Vector3 ResolveAttackForward(Transform targetOverride)
        {
            Transform attackTarget = ResolveLockPoint(targetOverride);
            if (attackTarget == null && _targeting != null && _targeting.HasTarget)
            {
                attackTarget = _targeting.CurrentLockPoint;
            }

            if (attackTarget != null)
            {
                Vector3 toTarget = attackTarget.position - transform.position;
                toTarget.y = 0f;

                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    return toTarget.normalized;
                }
            }

            Vector3 forward = transform.forward;
            forward.y = 0f;

            return forward.sqrMagnitude > 0.0001f
                ? forward.normalized
                : Vector3.forward;
        }

        private static Transform ResolveLockPoint(Transform target)
        {
            return target != null
                && CombatTargetUtility.TryResolve(target, out ICombatTarget combatTarget)
                    ? combatTarget.LockPoint
                    : target;
        }

        private void ConfigureHitbox(GameObject hitboxInstance, CombatActionDefinition actionDefinition)
        {
            if (hitboxInstance.TryGetComponent(out HitboxBase hitbox))
            {
                hitbox.Initialize(gameObject);
                hitbox.Configure(
                    actionDefinition.FlatDamage,
                    actionDefinition.DamageType,
                    actionDefinition.KnockbackForce,
                    actionDefinition.CombatTagToApply,
                    actionDefinition.CombatTagDuration,
                    actionDefinition.CombatTagStackCount,
                    actionDefinition);
            }
        }

        private GameObject GetCurrentTargetObject()
        {
            if (_targeting == null || !_targeting.HasTarget)
            {
                return null;
            }

            return _targeting.CurrentTarget != null ? _targeting.CurrentTarget.gameObject : null;
        }
    }
}
