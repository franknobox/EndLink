using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>玩家格挡判定结果。</summary>
    public enum PlayerGuardResult
    {
        None = 0,
        Blocked = 1,
        Parried = 2
    }

    /// <summary>
    /// 玩家正面格挡与短窗口弹反规则。
    /// 状态机负责开始和结束防御，本组件只判断命中方向、减伤与弹反反馈。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterHealth))]
    public sealed class PlayerGuardController : MonoBehaviour, IHitInterceptor
    {
        [Header("格挡弹反")]
        [Tooltip("进入防御后的弹反有效时间。窗口结束后仍可普通格挡。")]
        [SerializeField, Min(0f)]
        private float parryWindowDuration = 0.12f;

        [Tooltip("角色正前方可格挡的总角度。120 表示左右各 60 度。")]
        [SerializeField, Range(0f, 360f)]
        private float guardAngle = 120f;

        [Tooltip("普通格挡后保留的伤害倍率。0.2 表示承受原伤害的 20%。弹反始终完全化解伤害。")]
        [SerializeField, Range(0f, 1f)]
        private float blockedDamageMultiplier = 0.2f;

        private float _guardStartedAt;

        /// <summary>当前是否处于防御状态。</summary>
        public bool IsGuarding { get; private set; }

        /// <summary>当前防御已经持续的时间。</summary>
        public float GuardElapsedTime => IsGuarding ? Mathf.Max(0f, Time.time - _guardStartedAt) : 0f;

        private void OnDisable()
        {
            EndGuard();
        }

        private void OnValidate()
        {
            parryWindowDuration = Mathf.Max(0f, parryWindowDuration);
            guardAngle = Mathf.Clamp(guardAngle, 0f, 360f);
            blockedDamageMultiplier = Mathf.Clamp01(blockedDamageMultiplier);
        }

        /// <summary>开始防御并刷新本次弹反窗口。</summary>
        public void BeginGuard()
        {
            IsGuarding = true;
            _guardStartedAt = Time.time;
        }

        /// <summary>结束防御。</summary>
        public void EndGuard()
        {
            IsGuarding = false;
            _guardStartedAt = 0f;
        }

        /// <inheritdoc />
        public HitInterception InterceptHit(HitboxHitInfo hitInfo)
        {
            if (!IsGuarding)
            {
                return HitInterception.Continue;
            }

            Vector3 directionToAttacker = -hitInfo.HitDirection;
            PlayerGuardResult result = EvaluateGuard(
                GuardElapsedTime,
                parryWindowDuration,
                transform.forward,
                directionToAttacker,
                guardAngle);

            if (result == PlayerGuardResult.Parried)
            {
                NotifyParriedAttacker(hitInfo.Owner);
                return new HitInterception(true, 0f, false);
            }

            return result == PlayerGuardResult.Blocked
                ? new HitInterception(true, blockedDamageMultiplier, false)
                : HitInterception.Continue;
        }

        /// <summary>计算指定方向命中在当前时间点属于未防住、格挡或弹反。</summary>
        public static PlayerGuardResult EvaluateGuard(
            float guardElapsedTime,
            float parryWindowDuration,
            Vector3 defenderForward,
            Vector3 directionToAttacker,
            float guardAngle)
        {
            defenderForward.y = 0f;
            directionToAttacker.y = 0f;
            if (defenderForward.sqrMagnitude <= 0.0001f || directionToAttacker.sqrMagnitude <= 0.0001f)
            {
                return PlayerGuardResult.None;
            }

            float angle = Vector3.Angle(defenderForward, directionToAttacker);
            if (angle > Mathf.Clamp(guardAngle, 0f, 360f) * 0.5f)
            {
                return PlayerGuardResult.None;
            }

            return guardElapsedTime <= Mathf.Max(0f, parryWindowDuration)
                ? PlayerGuardResult.Parried
                : PlayerGuardResult.Blocked;
        }

        private void NotifyParriedAttacker(GameObject attacker)
        {
            ICombatParryReceiver receiver = attacker != null
                ? attacker.GetComponentInParent<ICombatParryReceiver>()
                : null;
            receiver?.ReceiveParry(gameObject);
        }
    }
}
