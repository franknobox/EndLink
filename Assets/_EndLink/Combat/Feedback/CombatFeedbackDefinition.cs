using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 一次战斗反馈的可复用数据配置。
    /// 动作、格挡、协议反应和奥义只引用该资产，不直接操作时间、相机、手柄、音频或 VFX。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CombatFeedback_",
        menuName = "EndLink/Combat/Combat Feedback Definition")]
    public sealed class CombatFeedbackDefinition : ScriptableObject
    {
        [Header("Hitstop")]
        [Tooltip("命中时是否短暂降低全局时间速度。只在场景中存在 CombatFeedbackDispatcher 时生效。")]
        [SerializeField]
        private bool enableHitstop = true;

        [Tooltip("Hitstop 持续的真实时间，不受 Time.timeScale 影响。轻击通常可从 0.03-0.06 秒开始调试。")]
        [SerializeField, Min(0f)]
        private float hitstopDuration = 0.045f;

        [Tooltip("Hitstop 期间相对原时间速度的倍率。0 表示完全暂停，0.05 表示保留极慢推进。")]
        [SerializeField, Range(0f, 1f)]
        private float hitstopTimeScale = 0.05f;

        [Header("镜头冲击")]
        [Tooltip("是否发出 Cinemachine Impulse。具体波形、持续时间和传播方式由场景中的 CinemachineImpulseSource 统一配置。")]
        [SerializeField]
        private bool enableCameraImpulse = true;

        [Tooltip("本次镜头冲击强度。方向默认与攻击命中方向相反。")]
        [SerializeField, Min(0f)]
        private float cameraImpulseForce = 0.35f;

        [Header("手柄震动")]
        [Tooltip("是否驱动当前 Gamepad 的双马达。没有连接手柄时会被安全忽略。")]
        [SerializeField]
        private bool enableRumble = true;

        [Tooltip("低频马达强度，主要表现沉重冲击。")]
        [SerializeField, Range(0f, 1f)]
        private float lowFrequencyRumble = 0.15f;

        [Tooltip("高频马达强度，主要表现锐利命中。")]
        [SerializeField, Range(0f, 1f)]
        private float highFrequencyRumble = 0.45f;

        [Tooltip("震动持续的真实时间，不受 Time.timeScale 影响。")]
        [SerializeField, Min(0f)]
        private float rumbleDuration = 0.08f;

        [Header("音效")]
        [Tooltip("反馈触发时通过统一 AudioSource 播放的一次性音效。为空表示不播放。")]
        [SerializeField]
        private AudioClip audioClip;

        [Tooltip("本次音效的音量倍率。")]
        [SerializeField, Range(0f, 1f)]
        private float audioVolume = 1f;

        [Header("VFX")]
        [Tooltip("反馈触发时生成在命中点的 VFX prefab。ParticleSystem 与 Visual Effect Graph prefab 都可通过 GameObject 接入。")]
        [SerializeField]
        private GameObject vfxPrefab;

        [Tooltip("生成后自动销毁的时间。小于等于 0 表示由 VFX 自己管理生命周期。")]
        [SerializeField, Min(0f)]
        private float vfxLifetime = 2f;

        [Tooltip("相对命中方向旋转后的局部位置偏移。")]
        [SerializeField]
        private Vector3 vfxLocalOffset;

        [Tooltip("是否让 VFX 的本地 Z 轴朝向攻击命中方向。关闭时使用 prefab 原始旋转。")]
        [SerializeField]
        private bool alignVfxToHitDirection = true;

        public bool HasHitstop => enableHitstop && hitstopDuration > 0f && hitstopTimeScale < 1f;
        public float HitstopDuration => Mathf.Max(0f, hitstopDuration);
        public float HitstopTimeScale => Mathf.Clamp01(hitstopTimeScale);
        public bool HasCameraImpulse => enableCameraImpulse && cameraImpulseForce > 0f;
        public float CameraImpulseForce => Mathf.Max(0f, cameraImpulseForce);
        public bool HasRumble => enableRumble
            && rumbleDuration > 0f
            && (lowFrequencyRumble > 0f || highFrequencyRumble > 0f);
        public float LowFrequencyRumble => Mathf.Clamp01(lowFrequencyRumble);
        public float HighFrequencyRumble => Mathf.Clamp01(highFrequencyRumble);
        public float RumbleDuration => Mathf.Max(0f, rumbleDuration);
        public AudioClip AudioClip => audioClip;
        public float AudioVolume => Mathf.Clamp01(audioVolume);
        public GameObject VfxPrefab => vfxPrefab;
        public float VfxLifetime => Mathf.Max(0f, vfxLifetime);
        public Vector3 VfxLocalOffset => vfxLocalOffset;
        public bool AlignVfxToHitDirection => alignVfxToHitDirection;

        private void OnValidate()
        {
            hitstopDuration = Mathf.Max(0f, hitstopDuration);
            hitstopTimeScale = Mathf.Clamp01(hitstopTimeScale);
            cameraImpulseForce = Mathf.Max(0f, cameraImpulseForce);
            lowFrequencyRumble = Mathf.Clamp01(lowFrequencyRumble);
            highFrequencyRumble = Mathf.Clamp01(highFrequencyRumble);
            rumbleDuration = Mathf.Max(0f, rumbleDuration);
            audioVolume = Mathf.Clamp01(audioVolume);
            vfxLifetime = Mathf.Max(0f, vfxLifetime);
        }
    }
}
