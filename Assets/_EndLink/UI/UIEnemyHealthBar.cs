using EndLink.Combat;
using EndLink.Enemies;
using UnityEngine;
using UnityEngine.UI;

namespace EndLink.UI
{
    /// <summary>
    /// 敌人头顶血条控制器。
    /// 负责把 World Space 血条跟随到敌人头顶、面向相机，并给敌人血条套用默认半透明暗红色样式。
    /// 实际血量填充仍交给 UIHealthBar 读取 EnemyHealth，避免重复维护扣血显示逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIEnemyHealthBar : MonoBehaviour
    {
        [Header("绑定")]
        [Tooltip("要显示的敌人生命组件。为空时会从父物体查找 EnemyHealth。")]
        [SerializeField]
        private EnemyHealth enemyHealth;

        [Tooltip("敌人的统一战斗目标。为空时会从 EnemyHealth 所在物体查找 CombatTarget。")]
        [SerializeField]
        private CombatTarget combatTarget;

        [Tooltip("血条跟随的参考点。优先级高于 CombatTarget.LockPoint。为空时使用 CombatTarget.LockPoint 或 EnemyHealth Transform。")]
        [SerializeField]
        private Transform followAnchor;

        [Tooltip("血条朝向参考相机。为空时会使用 Camera.main。")]
        [SerializeField]
        private Camera viewCamera;

        [Tooltip("绑定为空时，是否从父物体自动查找 EnemyHealth。")]
        [SerializeField]
        private bool autoFindInParent = true;

        [Header("位置")]
        [Tooltip("血条相对跟随点的世界坐标偏移。胶囊白模通常使用 Y=1.7 到 2.2。")]
        [SerializeField]
        private Vector3 worldOffset = new(0f, 2f, 0f);

        [Tooltip("是否让血条始终面向相机。")]
        [SerializeField]
        private bool faceCamera = true;

        [Header("显示")]
        [Tooltip("血条 Canvas。建议使用 World Space。为空时从当前物体查找。")]
        [SerializeField]
        private Canvas worldCanvas;

        [Tooltip("控制整个血条显隐的 CanvasGroup。为空时从当前物体查找。")]
        [SerializeField]
        private CanvasGroup canvasGroup;

        [Tooltip("通用血条组件。负责按 EnemyHealth 刷新填充比例。")]
        [SerializeField]
        private UIHealthBar healthBar;

        [Tooltip("血条背景 Image。")]
        [SerializeField]
        private Image backgroundImage;

        [Tooltip("血条填充 Image。")]
        [SerializeField]
        private Image fillImage;

        [Tooltip("血条背景颜色。默认是更暗的半透明红黑色。")]
        [SerializeField]
        private Color backgroundColor = new(0.08f, 0.01f, 0.01f, 0.42f);

        [Tooltip("血条填充颜色。默认是半透明暗红色。")]
        [SerializeField]
        private Color fillColor = new(0.36f, 0.02f, 0.02f, 0.68f);

        [Tooltip("满血时是否隐藏。敌人头顶血条通常开启，只在受伤后显示。")]
        [SerializeField]
        private bool hideWhenFull = true;

        [Tooltip("死亡时是否隐藏。")]
        [SerializeField]
        private bool hideWhenDead = true;

        [Tooltip("是否每帧刷新显隐。用于处理敌人重置血量但未发出事件的情况。")]
        [SerializeField]
        private bool refreshVisibilityEveryFrame = true;

        [Header("Canvas")]
        [Tooltip("是否在 Awake/OnValidate 中把 Canvas 配置成 World Space。")]
        [SerializeField]
        private bool configureWorldCanvas = true;

        [Tooltip("World Space Canvas 的排序层级。需要压过普通世界物体时可以调高。")]
        [SerializeField]
        private int sortingOrder = 20;

        private EnemyHealth _subscribedEnemyHealth;
        private bool _started;

        private void Awake()
        {
            CacheReferences();
            ConfigureCanvas();
            ApplyStyle();
        }

        private void OnEnable()
        {
            CacheReferences();
            Subscribe();
            BindHealthBar();
            if (_started)
            {
                RefreshNow();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Reset()
        {
            CacheReferences();
            ConfigureCanvas();
            ApplyStyle();
        }

        private void OnValidate()
        {
            CacheReferences();
            ConfigureCanvas();
            ApplyStyle();
        }

        private void LateUpdate()
        {
            UpdateWorldTransform();

            if (refreshVisibilityEveryFrame)
            {
                ApplyVisibility(ResolveShouldShow());
            }
        }

        private void Start()
        {
            _started = true;
            RefreshNow();
        }

        /// <summary>
        /// 运行时绑定敌人生命来源。
        /// 用于动态生成血条或后续对象池复用。
        /// </summary>
        public void Bind(EnemyHealth nextEnemyHealth)
        {
            if (enemyHealth == nextEnemyHealth)
            {
                RefreshNow();
                return;
            }

            Unsubscribe();
            enemyHealth = nextEnemyHealth;
            combatTarget = enemyHealth != null ? enemyHealth.GetComponent<CombatTarget>() : null;
            Subscribe();
            BindHealthBar();
            RefreshNow();
        }

        /// <summary>
        /// 立即刷新血条样式、数值和显隐。
        /// </summary>
        public void RefreshNow()
        {
            ApplyStyle();
            BindHealthBar();

            if (healthBar != null)
            {
                healthBar.RefreshNow();
            }

            ApplyVisibility(ResolveShouldShow());
        }

        private void CacheReferences()
        {
            if (worldCanvas == null)
            {
                worldCanvas = GetComponent<Canvas>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (healthBar == null)
            {
                healthBar = GetComponentInChildren<UIHealthBar>(true);
            }

            if (backgroundImage == null)
            {
                Image[] images = GetComponentsInChildren<Image>(true);
                if (images.Length > 0)
                {
                    backgroundImage = images[0];
                }
            }

            if (fillImage == null && backgroundImage != null)
            {
                Image[] images = backgroundImage.GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i] != backgroundImage)
                    {
                        fillImage = images[i];
                        break;
                    }
                }
            }

            if (enemyHealth == null && autoFindInParent)
            {
                enemyHealth = GetComponentInParent<EnemyHealth>();
            }

            if (combatTarget == null && enemyHealth != null)
            {
                combatTarget = enemyHealth.GetComponent<CombatTarget>();
            }
        }

        private void ConfigureCanvas()
        {
            if (!configureWorldCanvas || worldCanvas == null)
            {
                return;
            }

            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.overrideSorting = true;
            worldCanvas.sortingOrder = sortingOrder;

            if (viewCamera != null)
            {
                worldCanvas.worldCamera = viewCamera;
            }
        }

        private void ApplyStyle()
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = backgroundColor;
                backgroundImage.raycastTarget = false;
            }

            if (fillImage != null)
            {
                fillImage.color = fillColor;
                fillImage.raycastTarget = false;
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = 0;
            }
        }

        private void BindHealthBar()
        {
            if (healthBar != null && enemyHealth != null)
            {
                healthBar.BindEnemyHealth(enemyHealth);
            }
        }

        private void Subscribe()
        {
            if (enemyHealth == null || _subscribedEnemyHealth == enemyHealth)
            {
                return;
            }

            if (_subscribedEnemyHealth != null)
            {
                Unsubscribe();
            }

            enemyHealth.OnDamaged.AddListener(HandleEnemyDamaged);
            enemyHealth.OnDead.AddListener(HandleEnemyDead);
            _subscribedEnemyHealth = enemyHealth;
        }

        private void Unsubscribe()
        {
            if (_subscribedEnemyHealth == null)
            {
                return;
            }

            _subscribedEnemyHealth.OnDamaged.RemoveListener(HandleEnemyDamaged);
            _subscribedEnemyHealth.OnDead.RemoveListener(HandleEnemyDead);
            _subscribedEnemyHealth = null;
        }

        private void HandleEnemyDamaged(int damageAmount, CombatTagDefinition tag)
        {
            RefreshNow();
        }

        private void HandleEnemyDead()
        {
            RefreshNow();
        }

        private void UpdateWorldTransform()
        {
            Transform anchor = ResolveFollowAnchor();
            if (anchor != null)
            {
                transform.position = anchor.position + worldOffset;
            }

            if (!faceCamera)
            {
                return;
            }

            Camera camera = ResolveViewCamera();
            if (camera == null)
            {
                return;
            }

            transform.LookAt(
                transform.position + camera.transform.rotation * Vector3.forward,
                camera.transform.rotation * Vector3.up);
        }

        private Transform ResolveFollowAnchor()
        {
            if (followAnchor != null)
            {
                return followAnchor;
            }

            if (combatTarget != null)
            {
                return combatTarget.LockPoint;
            }

            return enemyHealth != null ? enemyHealth.transform : null;
        }

        private Camera ResolveViewCamera()
        {
            if (viewCamera != null)
            {
                return viewCamera;
            }

            viewCamera = Camera.main;
            if (worldCanvas != null && worldCanvas.worldCamera == null)
            {
                worldCanvas.worldCamera = viewCamera;
            }

            return viewCamera;
        }

        private bool ResolveShouldShow()
        {
            if (enemyHealth == null)
            {
                return false;
            }

            if (hideWhenDead && enemyHealth.IsDead)
            {
                return false;
            }

            if (hideWhenFull && enemyHealth.CurrentHealth >= enemyHealth.MaxHealth)
            {
                return false;
            }

            return true;
        }

        private void ApplyVisibility(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                return;
            }

            if (worldCanvas != null)
            {
                worldCanvas.enabled = visible;
            }
        }
    }
}
