using System;
using System.Collections;
using EndLink.Combat;
using UnityEngine;
using UnityEngine.Events;

namespace EndLink.World
{
    /// <summary>
    /// 通用武器环境交互组件。
    /// 推荐挂在具体功能物体的交互子物体上，与用于接收武器 Hitbox 的 Collider 放在一起。
    /// 本组件只负责形态合法性、命中进度和触发节流，实际门、电梯或机关功能由父级 IObjFunction 执行。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ObjInteractable : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("交互规则")]
        [Tooltip("固定的环境交互类型。类型会直接决定允许触发的 A、B、C 武器形态。")]
        [SerializeField]
        private ObjInteractionType interactionType = ObjInteractionType.TriggerDevice;

        [Tooltip("负责执行实际功能的组件，必须实现 IObjFunction。为空时会自动从当前物体及父级查找。")]
        [SerializeField]
        private MonoBehaviour functionTarget;

        [Tooltip("是否允许当前交互子物体接收武器交互。关闭后不会累计进度或执行功能。")]
        [SerializeField]
        private bool interactionEnabled = true;

        [Header("触发控制")]
        [Tooltip("网络结构和可破坏物触发功能所需的有效命中次数。其他类型固定按一次命中处理。")]
        [SerializeField, Min(1)]
        private int requiredHitCount = 1;

        [Tooltip("两次有效命中之间的最短间隔，用于避免同一段攻击的重叠判定被重复累计。")]
        [SerializeField, Min(0f)]
        private float minHitInterval = 0.1f;

        [Tooltip("开启后，功能首次成功执行后永久停止接收交互，直到外部调用 ResetInteraction。")]
        [SerializeField]
        private bool triggerOnce;

        [Header("成功反馈")]
        [Tooltip("功能成功执行时覆盖到交互物视觉上的闪白颜色。")]
        [SerializeField]
        private Color flashColor = Color.white;

        [Tooltip("成功闪白持续时间，使用非缩放时间，因此不会被 Hitstop 截断。")]
        [SerializeField, Min(0f)]
        private float flashDuration = 0.12f;

        [Header("事件")]
        [Tooltip("接受一次合法形态命中并更新进度后触发。参数依次为当前进度和所需进度。")]
        [SerializeField]
        private ObjInteractionProgressEvent onProgressChanged = new();

        [Tooltip("父级功能成功执行后触发，参数为发起交互的角色对象。")]
        [SerializeField]
        private ObjInteractionTriggeredEvent onTriggered = new();

        private IObjFunction _function;
        private int _currentHitCount;
        private float _nextAcceptedHitTime;
        private bool _hasTriggered;
        private Renderer[] _feedbackRenderers = Array.Empty<Renderer>();
        private MaterialPropertyBlock[] _originalPropertyBlocks = Array.Empty<MaterialPropertyBlock>();
        private Coroutine _flashRoutine;

        /// <summary>当前配置的六类交互类型。</summary>
        public ObjInteractionType InteractionType => interactionType;

        /// <summary>该类型要求使用的固定武器形态。</summary>
        public PlayerWeaponForm RequiredForm => ObjInteractionTypeUtility.GetRequiredForm(interactionType);

        /// <summary>当前已经累计的有效命中次数。</summary>
        public int CurrentHitCount => _currentHitCount;

        /// <summary>当前类型实际需要的有效命中次数。</summary>
        public int RequiredHitCount => ObjInteractionTypeUtility.UsesHitProgress(interactionType)
            ? Mathf.Max(1, requiredHitCount)
            : 1;

        /// <summary>当前是否允许继续接收交互。</summary>
        public bool IsInteractionEnabled => interactionEnabled && (!_hasTriggered || !triggerOnce);

        /// <summary>有效命中进度变化事件。</summary>
        public ObjInteractionProgressEvent OnProgressChanged => onProgressChanged;

        /// <summary>功能成功执行事件。</summary>
        public ObjInteractionTriggeredEvent OnTriggered => onTriggered;

        private void Awake()
        {
            CacheFunction();
            CacheFeedbackRenderers();
            if (_function == null)
            {
                Debug.LogError(
                    $"ObjInteractable '{name}' 没有找到实现 IObjFunction 的功能组件。",
                    this);
            }
        }

        private void Reset()
        {
            requiredHitCount = 1;
            minHitInterval = 0.1f;
            CacheFunction();
            CacheFeedbackRenderers();
        }

        private void OnValidate()
        {
            requiredHitCount = Mathf.Max(1, requiredHitCount);
            minHitInterval = Mathf.Max(0f, minHitInterval);
            flashDuration = Mathf.Max(0f, flashDuration);
            CacheFunction();
        }

        private void OnDisable()
        {
            if (_flashRoutine == null)
            {
                return;
            }

            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
            RestoreFeedbackColors();
        }

        /// <summary>判断指定武器形态是否符合当前交互类型的固定规则。</summary>
        public bool CanReceive(PlayerWeaponForm weaponForm)
        {
            return IsInteractionEnabled
                && weaponForm == ObjInteractionTypeUtility.GetRequiredForm(interactionType);
        }

        /// <summary>
        /// 接收一次由玩家武器 Hitbox 提交的环境交互。
        /// 返回 true 表示本次命中被交互系统接受；累计过程尚未完成时也会返回 true。
        /// </summary>
        public bool TryReceive(ObjInteractionContext context)
        {
            if (!CanReceive(context.WeaponForm)
                || context.Interactor == null
                || Time.time < _nextAcceptedHitTime)
            {
                return false;
            }

            CacheFunction();
            if (_function == null)
            {
                return false;
            }

            _nextAcceptedHitTime = Time.time + minHitInterval;
            _currentHitCount = Mathf.Min(_currentHitCount + 1, RequiredHitCount);
            onProgressChanged?.Invoke(_currentHitCount, RequiredHitCount);

            if (_currentHitCount < RequiredHitCount)
            {
                return true;
            }

            if (!_function.TryExecute(context))
            {
                return false;
            }

            _currentHitCount = 0;
            _hasTriggered = true;
            PlaySuccessFlash();
            onProgressChanged?.Invoke(0, RequiredHitCount);
            onTriggered?.Invoke(context.Interactor);
            return true;
        }

        /// <summary>启用或关闭当前交互入口，不修改已经累计的命中进度。</summary>
        public void SetInteractionEnabled(bool enabled)
        {
            interactionEnabled = enabled;
        }

        /// <summary>清除命中进度、一次性触发状态和命中节流，用于机关重置或场景重开。</summary>
        public void ResetInteraction()
        {
            _currentHitCount = 0;
            _nextAcceptedHitTime = 0f;
            _hasTriggered = false;
            onProgressChanged?.Invoke(0, RequiredHitCount);
        }

        private void CacheFunction()
        {
            if (functionTarget is IObjFunction assignedFunction)
            {
                _function = assignedFunction;
                return;
            }

            _function = null;
            MonoBehaviour[] candidates = GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                MonoBehaviour candidate = candidates[i];
                if (candidate == null || ReferenceEquals(candidate, this) || candidate is not IObjFunction function)
                {
                    continue;
                }

                functionTarget = candidate;
                _function = function;
                return;
            }
        }

        private void CacheFeedbackRenderers()
        {
            _feedbackRenderers = GetComponentsInChildren<Renderer>(true);
            _originalPropertyBlocks = new MaterialPropertyBlock[_feedbackRenderers.Length];
        }

        private void PlaySuccessFlash()
        {
            if (flashDuration <= 0f || _feedbackRenderers.Length == 0)
            {
                return;
            }

            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
                RestoreFeedbackColors();
            }

            ApplyFlashColor();
            _flashRoutine = StartCoroutine(RestoreFeedbackAfterDelay());
        }

        private void ApplyFlashColor()
        {
            for (int i = 0; i < _feedbackRenderers.Length; i++)
            {
                Renderer targetRenderer = _feedbackRenderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                MaterialPropertyBlock originalBlock = new();
                targetRenderer.GetPropertyBlock(originalBlock);
                _originalPropertyBlocks[i] = originalBlock;

                MaterialPropertyBlock flashBlock = new();
                targetRenderer.GetPropertyBlock(flashBlock);
                flashBlock.SetColor(BaseColorId, flashColor);
                flashBlock.SetColor(ColorId, flashColor);
                targetRenderer.SetPropertyBlock(flashBlock);
            }
        }

        private IEnumerator RestoreFeedbackAfterDelay()
        {
            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            RestoreFeedbackColors();
            _flashRoutine = null;
        }

        private void RestoreFeedbackColors()
        {
            for (int i = 0; i < _feedbackRenderers.Length; i++)
            {
                Renderer targetRenderer = _feedbackRenderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.SetPropertyBlock(_originalPropertyBlocks[i]);
                _originalPropertyBlocks[i] = null;
            }
        }
    }

    /// <summary>交互命中进度事件。</summary>
    [Serializable]
    public sealed class ObjInteractionProgressEvent : UnityEvent<int, int>
    {
    }

    /// <summary>交互功能成功触发事件。</summary>
    [Serializable]
    public sealed class ObjInteractionTriggeredEvent : UnityEvent<GameObject>
    {
    }
}
