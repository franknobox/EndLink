using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EndLink.Combat
{
    /// <summary>
    /// 场景级战斗反馈执行器。
    /// 统一消费反馈请求并驱动 Hitstop、Cinemachine Impulse、手柄震动、音效与 VFX。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(CinemachineImpulseSource))]
    public sealed class CombatFeedbackDispatcher : MonoBehaviour
    {
        [Header("通道开关")]
        [Tooltip("是否执行 CombatFeedbackDefinition 中的 Hitstop 配置。")]
        [SerializeField]
        private bool enableHitstop = true;

        [Tooltip("是否发出 Cinemachine Impulse。相机仍需添加 Cinemachine Impulse Listener 才能看到震动。")]
        [SerializeField]
        private bool enableCameraImpulse = true;

        [Tooltip("是否驱动当前连接的 Gamepad 双马达。")]
        [SerializeField]
        private bool enableRumble = true;

        [Tooltip("是否通过统一 AudioSource 播放反馈音效。")]
        [SerializeField]
        private bool enableAudio = true;

        [Tooltip("是否生成反馈配置中的 VFX prefab。")]
        [SerializeField]
        private bool enableVfx = true;

        [Header("执行引用")]
        [Tooltip("发出镜头冲击的 Cinemachine Impulse Source。为空时读取同物体组件。波形与持续时间在该组件中统一调整。")]
        [SerializeField]
        private CinemachineImpulseSource impulseSource;

        [Tooltip("播放一次性战斗音效的 AudioSource。为空时读取同物体组件。")]
        [SerializeField]
        private AudioSource audioSource;

        [Tooltip("运行时 VFX 的可选父节点。为空时生成在场景根级，避免跟随移动对象改变命中位置。")]
        [SerializeField]
        private Transform vfxRoot;

        private static CombatFeedbackDispatcher _activeDispatcher;

        private bool _subscribed;
        private bool _hitstopActive;
        private float _hitstopEndsAt;
        private float _hitstopScale = 1f;
        private float _restoreTimeScale = 1f;
        private float _restoreFixedDeltaTime = 0.02f;
        private float _appliedTimeScale = 1f;
        private float _appliedFixedDeltaTime = 0.02f;

        private Gamepad _rumbleGamepad;
        private float _rumbleEndsAt;
        private float _lowFrequencyRumble;
        private float _highFrequencyRumble;

        private void Reset()
        {
            ResolveComponents();
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
            }
        }

        private void Awake()
        {
            ResolveComponents();
        }

        private void OnEnable()
        {
            if (_activeDispatcher != null && _activeDispatcher != this)
            {
                Debug.LogWarning(
                    "场景中只能启用一个 CombatFeedbackDispatcher，当前重复组件已自动禁用。",
                    this);
                enabled = false;
                return;
            }

            _activeDispatcher = this;
            CombatFeedbackBus.Requested += HandleFeedbackRequest;
            _subscribed = true;
        }

        private void Update()
        {
            float realtime = Time.realtimeSinceStartup;
            if (_hitstopActive && realtime >= _hitstopEndsAt)
            {
                RestoreHitstop();
            }

            if (_rumbleGamepad != null && realtime >= _rumbleEndsAt)
            {
                StopRumble();
            }
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                CombatFeedbackBus.Requested -= HandleFeedbackRequest;
                _subscribed = false;
            }

            RestoreHitstop();
            StopRumble();

            if (_activeDispatcher == this)
            {
                _activeDispatcher = null;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                StopRumble();
            }
        }

        /// <summary>直接播放一条反馈请求，供调试工具或后续局部表现系统复用。</summary>
        public void Play(CombatFeedbackRequest request)
        {
            HandleFeedbackRequest(request);
        }

        private void HandleFeedbackRequest(CombatFeedbackRequest request)
        {
            CombatFeedbackDefinition definition = request.Definition;
            if (definition == null)
            {
                return;
            }

            if (enableHitstop && definition.HasHitstop)
            {
                StartHitstop(definition.HitstopDuration, definition.HitstopTimeScale);
            }

            if (enableCameraImpulse && definition.HasCameraImpulse)
            {
                PlayCameraImpulse(request, definition.CameraImpulseForce);
            }

            if (enableRumble && definition.HasRumble)
            {
                StartRumble(definition);
            }

            if (enableAudio && definition.AudioClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(definition.AudioClip, definition.AudioVolume);
            }

            if (enableVfx && definition.VfxPrefab != null)
            {
                SpawnVfx(request, definition);
            }
        }

        private void StartHitstop(float duration, float relativeTimeScale)
        {
            if (duration <= 0f || Time.timeScale <= 0f)
            {
                return;
            }

            if (!_hitstopActive)
            {
                _hitstopActive = true;
                _restoreTimeScale = Time.timeScale;
                _restoreFixedDeltaTime = Time.fixedDeltaTime;
                _hitstopScale = Mathf.Clamp01(relativeTimeScale);
            }
            else
            {
                // 重叠反馈保留更强的停顿，并延长到所有请求中最晚结束的时间。
                _hitstopScale = Mathf.Min(_hitstopScale, Mathf.Clamp01(relativeTimeScale));
            }

            _hitstopEndsAt = Mathf.Max(
                _hitstopEndsAt,
                Time.realtimeSinceStartup + duration);

            _appliedTimeScale = _restoreTimeScale * _hitstopScale;
            _appliedFixedDeltaTime = Mathf.Max(
                0.0001f,
                _restoreFixedDeltaTime * Mathf.Max(0.01f, _hitstopScale));
            Time.timeScale = _appliedTimeScale;
            Time.fixedDeltaTime = _appliedFixedDeltaTime;
        }

        private void RestoreHitstop()
        {
            if (!_hitstopActive)
            {
                return;
            }

            // 若暂停或其他系统在 Hitstop 期间接管了时间速度，不覆盖对方的新状态。
            if (Mathf.Approximately(Time.timeScale, _appliedTimeScale))
            {
                Time.timeScale = _restoreTimeScale;
            }

            if (Mathf.Approximately(Time.fixedDeltaTime, _appliedFixedDeltaTime))
            {
                Time.fixedDeltaTime = _restoreFixedDeltaTime;
            }

            _hitstopActive = false;
            _hitstopEndsAt = 0f;
            _hitstopScale = 1f;
        }

        private void PlayCameraImpulse(CombatFeedbackRequest request, float force)
        {
            if (impulseSource == null || force <= 0f)
            {
                return;
            }

            Vector3 impulseDirection = request.Direction.sqrMagnitude > 0.0001f
                ? -request.Direction
                : impulseSource.DefaultVelocity;

            if (impulseDirection.sqrMagnitude <= 0.0001f)
            {
                impulseDirection = Vector3.down;
            }

            impulseSource.GenerateImpulseAtPositionWithVelocity(
                request.Position,
                impulseDirection.normalized * force);
        }

        private void StartRumble(CombatFeedbackDefinition definition)
        {
            if (_rumbleGamepad != null && Time.realtimeSinceStartup >= _rumbleEndsAt)
            {
                StopRumble();
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad == null)
            {
                return;
            }

            if (_rumbleGamepad != null && _rumbleGamepad != gamepad)
            {
                StopRumble();
            }

            _rumbleGamepad = gamepad;
            _lowFrequencyRumble = Mathf.Max(
                _lowFrequencyRumble,
                definition.LowFrequencyRumble);
            _highFrequencyRumble = Mathf.Max(
                _highFrequencyRumble,
                definition.HighFrequencyRumble);
            _rumbleEndsAt = Mathf.Max(
                _rumbleEndsAt,
                Time.realtimeSinceStartup + definition.RumbleDuration);

            _rumbleGamepad.SetMotorSpeeds(_lowFrequencyRumble, _highFrequencyRumble);
        }

        private void StopRumble()
        {
            if (_rumbleGamepad != null && _rumbleGamepad.added)
            {
                _rumbleGamepad.SetMotorSpeeds(0f, 0f);
            }

            _rumbleGamepad = null;
            _rumbleEndsAt = 0f;
            _lowFrequencyRumble = 0f;
            _highFrequencyRumble = 0f;
        }

        private void SpawnVfx(
            CombatFeedbackRequest request,
            CombatFeedbackDefinition definition)
        {
            Quaternion rotation = Quaternion.identity;
            if (definition.AlignVfxToHitDirection && request.Direction.sqrMagnitude > 0.0001f)
            {
                rotation = Quaternion.LookRotation(request.Direction, Vector3.up);
            }

            Vector3 position = request.Position + rotation * definition.VfxLocalOffset;
            GameObject instance = Instantiate(
                definition.VfxPrefab,
                position,
                rotation,
                vfxRoot);

            if (definition.VfxLifetime > 0f)
            {
                Destroy(instance, definition.VfxLifetime);
            }
        }

        private void ResolveComponents()
        {
            impulseSource ??= GetComponent<CinemachineImpulseSource>();
            audioSource ??= GetComponent<AudioSource>();
        }
    }
}
