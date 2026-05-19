using System.Collections;
using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 训练木桩敌人。
    /// 同时实现 IHitReceiver 和 IDamageable：Hitbox 给完整命中信息，木桩内部再转成简单伤害处理。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class EnemyDummy : MonoBehaviour, IHitReceiver, IDamageable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("受击反馈")]
        [Tooltip("受击时瞬间切换的颜色。当前使用浅红不透明色，适合胶囊白模阶段。")]
        [SerializeField]
        private Color hitColor = new Color(1f, 0.55f, 0.55f, 1f);

        [Tooltip("受击颜色保持时间。")]
        [SerializeField, Min(0.01f)]
        private float hitFlashDuration = 0.1f;

        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private Color _originalColor;
        private Coroutine _flashCoroutine;

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            _propertyBlock = new MaterialPropertyBlock();
            _originalColor = GetOriginalBaseColor();
        }

        /// <summary>
        /// 接收 Hitbox 的完整命中信息，并转发给简单伤害接口。
        /// </summary>
        public void ReceiveHit(HitboxHitInfo hitInfo)
        {
            TakeDamage(Mathf.RoundToInt(hitInfo.DamageAmount), hitInfo.TagToApply);
        }

        /// <summary>
        /// 接收伤害。当前木桩只打印标签并闪浅红，不扣血。
        /// </summary>
        public void TakeDamage(int damage, string tag)
        {
            Debug.Log($"EnemyDummy received hit tag: {tag}", this);

            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
            }

            _flashCoroutine = StartCoroutine(FlashHitColor());
        }

        private IEnumerator FlashHitColor()
        {
            SetBaseColor(hitColor);
            yield return new WaitForSeconds(hitFlashDuration);
            SetBaseColor(_originalColor);
            _flashCoroutine = null;
        }

        private Color GetOriginalBaseColor()
        {
            Material sharedMaterial = _meshRenderer.sharedMaterial;

            if (sharedMaterial != null && sharedMaterial.HasProperty(BaseColorId))
            {
                return sharedMaterial.GetColor(BaseColorId);
            }

            return Color.white;
        }

        private void SetBaseColor(Color color)
        {
            _meshRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, color);
            _meshRenderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
