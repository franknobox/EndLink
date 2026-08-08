using UnityEngine;
using UnityEngine.Events;

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
        private const int FeedbackArcPointCount = 25;
        private static readonly Color GuardFeedbackColor = new(0.25f, 0.75f, 1f, 0.8f);
        private static readonly Color BlockFeedbackColor = new(0.35f, 1f, 1f, 1f);
        private static readonly Color ParryFeedbackColor = new(1f, 0.85f, 0.2f, 1f);

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

        [Header("白模反馈")]
        [Tooltip("没有正式动画和 VFX 时，是否在角色正面自动生成简易防御弧。")]
        [SerializeField]
        private bool showFallbackVisual = true;

        [Tooltip("简易防御弧相对玩家根物体的本地位置。")]
        [SerializeField]
        private Vector3 feedbackLocalOffset = new(0f, 1.1f, 0.65f);

        [Tooltip("简易防御弧的显示半径。")]
        [SerializeField, Min(0.1f)]
        private float feedbackRadius = 0.8f;

        [Header("反馈事件")]
        [Tooltip("进入防御状态时触发，后续可接防御动画、循环特效或音效。")]
        [SerializeField]
        private UnityEvent onGuardStarted = new();

        [Tooltip("退出防御状态时触发。")]
        [SerializeField]
        private UnityEvent onGuardEnded = new();

        [Tooltip("普通格挡成功时触发。")]
        [SerializeField]
        private UnityEvent onBlocked = new();

        [Tooltip("弹反成功时触发。")]
        [SerializeField]
        private UnityEvent onParried = new();

        private float _guardStartedAt;
        private LineRenderer _feedbackRenderer;
        private Material _feedbackMaterial;
        private float _feedbackFlashEndsAt;
        private PlayerGuardResult _feedbackFlashResult;
        private CharacterHealth _characterHealth;

        /// <summary>当前是否处于防御状态。</summary>
        public bool IsGuarding { get; private set; }

        /// <summary>当前防御已经持续的时间。</summary>
        public float GuardElapsedTime => IsGuarding ? Mathf.Max(0f, Time.time - _guardStartedAt) : 0f;

        /// <summary>进入防御状态事件。</summary>
        public UnityEvent OnGuardStarted => onGuardStarted;

        /// <summary>退出防御状态事件。</summary>
        public UnityEvent OnGuardEnded => onGuardEnded;

        /// <summary>普通格挡成功事件。</summary>
        public UnityEvent OnBlocked => onBlocked;

        /// <summary>弹反成功事件。</summary>
        public UnityEvent OnParried => onParried;

        private void Awake()
        {
            _characterHealth = GetComponent<CharacterHealth>();
        }

        private void OnEnable()
        {
            _characterHealth ??= GetComponent<CharacterHealth>();
            _characterHealth?.RegisterHitInterceptor(this);
        }

        private void Update()
        {
            RefreshFallbackVisual();
        }

        private void OnDisable()
        {
            _characterHealth?.UnregisterHitInterceptor(this);
            EndGuard();
            SetFallbackVisualVisible(false);
        }

        private void OnDestroy()
        {
            if (_feedbackMaterial != null)
            {
                Destroy(_feedbackMaterial);
            }
        }

        private void OnValidate()
        {
            parryWindowDuration = Mathf.Max(0f, parryWindowDuration);
            guardAngle = Mathf.Clamp(guardAngle, 0f, 360f);
            blockedDamageMultiplier = Mathf.Clamp01(blockedDamageMultiplier);
            feedbackRadius = Mathf.Max(0.1f, feedbackRadius);
        }

        /// <summary>开始防御并刷新本次弹反窗口。</summary>
        public void BeginGuard()
        {
            if (IsGuarding)
            {
                return;
            }

            IsGuarding = true;
            _guardStartedAt = Time.time;
            onGuardStarted?.Invoke();
            RefreshFallbackVisual(true);
        }

        /// <summary>结束防御。</summary>
        public void EndGuard()
        {
            if (!IsGuarding)
            {
                return;
            }

            IsGuarding = false;
            _guardStartedAt = 0f;
            onGuardEnded?.Invoke();
            RefreshFallbackVisual(true);
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
                PlayGuardFeedback(PlayerGuardResult.Parried);
                onParried?.Invoke();
                return new HitInterception(HitOutcome.Parried, 0f, false);
            }

            if (result == PlayerGuardResult.Blocked)
            {
                PlayGuardFeedback(PlayerGuardResult.Blocked);
                onBlocked?.Invoke();
                return new HitInterception(HitOutcome.Blocked, blockedDamageMultiplier, false);
            }

            return HitInterception.Continue;
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

        private void PlayGuardFeedback(PlayerGuardResult result)
        {
            _feedbackFlashResult = result;
            _feedbackFlashEndsAt = Time.time + (result == PlayerGuardResult.Parried ? 0.22f : 0.14f);
            RefreshFallbackVisual(true);
        }

        private void RefreshFallbackVisual(bool forceRebuild = false)
        {
            if (!showFallbackVisual)
            {
                SetFallbackVisualVisible(false);
                return;
            }

            bool isFlashing = Time.time < _feedbackFlashEndsAt;
            bool shouldShow = IsGuarding || isFlashing;
            if (!shouldShow)
            {
                SetFallbackVisualVisible(false);
                return;
            }

            EnsureFallbackVisual();
            if (_feedbackRenderer == null)
            {
                return;
            }

            if (forceRebuild)
            {
                RebuildFeedbackArc();
            }

            Color color = GuardFeedbackColor;
            float width = 0.045f;
            float scale = 1f;

            if (isFlashing && _feedbackFlashResult == PlayerGuardResult.Parried)
            {
                color = ParryFeedbackColor;
                width = 0.13f;
                scale = 1.18f;
            }
            else if (isFlashing && _feedbackFlashResult == PlayerGuardResult.Blocked)
            {
                color = BlockFeedbackColor;
                width = 0.085f;
                scale = 1.08f;
            }

            _feedbackRenderer.enabled = true;
            _feedbackRenderer.startColor = color;
            _feedbackRenderer.endColor = color;
            _feedbackRenderer.startWidth = width;
            _feedbackRenderer.endWidth = width;
            _feedbackRenderer.transform.localScale = Vector3.one * scale;
        }

        private void EnsureFallbackVisual()
        {
            if (_feedbackRenderer != null)
            {
                return;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                return;
            }

            GameObject feedbackObject = new("Guard Feedback (Runtime)");
            feedbackObject.hideFlags = HideFlags.DontSave;
            feedbackObject.layer = gameObject.layer;
            feedbackObject.transform.SetParent(transform, false);
            feedbackObject.transform.localPosition = feedbackLocalOffset;

            _feedbackRenderer = feedbackObject.AddComponent<LineRenderer>();
            _feedbackRenderer.useWorldSpace = false;
            _feedbackRenderer.loop = false;
            _feedbackRenderer.alignment = LineAlignment.View;
            _feedbackRenderer.textureMode = LineTextureMode.Stretch;
            _feedbackRenderer.numCapVertices = 3;
            _feedbackRenderer.numCornerVertices = 3;
            _feedbackRenderer.positionCount = FeedbackArcPointCount;

            _feedbackMaterial = new Material(shader)
            {
                hideFlags = HideFlags.DontSave
            };
            _feedbackRenderer.sharedMaterial = _feedbackMaterial;
            RebuildFeedbackArc();
        }

        private void RebuildFeedbackArc()
        {
            if (_feedbackRenderer == null)
            {
                return;
            }

            _feedbackRenderer.transform.localPosition = feedbackLocalOffset;
            for (int i = 0; i < FeedbackArcPointCount; i++)
            {
                float normalizedPoint = i / (FeedbackArcPointCount - 1f);
                float angle = Mathf.Lerp(-75f, 75f, normalizedPoint) * Mathf.Deg2Rad;
                Vector3 localPoint = new(
                    Mathf.Sin(angle) * feedbackRadius,
                    0f,
                    Mathf.Cos(angle) * feedbackRadius * 0.85f);
                _feedbackRenderer.SetPosition(i, localPoint);
            }
        }

        private void SetFallbackVisualVisible(bool visible)
        {
            if (_feedbackRenderer == null)
            {
                return;
            }

            _feedbackRenderer.enabled = visible;
            if (!visible)
            {
                _feedbackRenderer.transform.localScale = Vector3.one;
            }
        }
    }
}
