using System.IO;
using EndLink.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EndLink.Editor
{
    /// <summary>
    /// 当前仍在使用的战斗 HUD UGUI 预制体生成入口。
    /// 旧版小队状态、动作槽和奥义面板已经归档，不再由该工具重复生成。
    /// </summary>
    public static class CombatHUDPrefabGenerator
    {
        private const string PrefabFolder = "Assets/_EndLink/UI/Prefabs";
        private const string GeneratedFolder = "Assets/_EndLink/UI/Generated";
        private const string DebugPanelPath = PrefabFolder + "/PF_DebugPanel.prefab";
        private const string EnemyHealthBarPath = PrefabFolder + "/PF_EnemyHealthBar.prefab";
        private const string SquareSpritePath = GeneratedFolder + "/UI_Square64.png";

        [MenuItem("EndLink/UI/Combat HUD/Create Current Prefabs")]
        public static void CreateCurrentPrefabs()
        {
            EnsureFolder(PrefabFolder);
            bool hasExistingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DebugPanelPath) != null
                || AssetDatabase.LoadAssetAtPath<GameObject>(EnemyHealthBarPath) != null;

            if (hasExistingPrefab
                && !EditorUtility.DisplayDialog(
                    "生成当前战斗 HUD",
                    "调试面板或敌人血条 Prefab 已存在，是否覆盖？",
                    "覆盖",
                    "取消"))
            {
                return;
            }

            CreateDebugPanel(promptOverwrite: false);
            CreateEnemyHealthBar(promptOverwrite: false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("已生成当前仍在使用的战斗 HUD Prefab。");
        }

        [MenuItem("EndLink/UI/Combat HUD/Create Debug Panel")]
        public static void CreateDebugPanel()
        {
            CreateDebugPanel(promptOverwrite: true);
        }

        [MenuItem("EndLink/UI/Combat HUD/Create Enemy Health Bar")]
        public static void CreateEnemyHealthBar()
        {
            CreateEnemyHealthBar(promptOverwrite: true);
        }

        private static void CreateDebugPanel(bool promptOverwrite)
        {
            EnsureFolder(PrefabFolder);
            Sprite squareSprite = EnsureSquareSprite();
            GameObject panel = CreateDebugPanelObject(squareSprite);
            SavePanelPrefab(panel, DebugPanelPath, promptOverwrite);
        }

        private static void CreateEnemyHealthBar(bool promptOverwrite)
        {
            EnsureFolder(PrefabFolder);
            Sprite squareSprite = EnsureSquareSprite();
            GameObject healthBar = CreateEnemyHealthBarObject(squareSprite);
            SaveWorldUIPrefab(healthBar, EnemyHealthBarPath, promptOverwrite);
        }

        private static GameObject CreateDebugPanelObject(Sprite squareSprite)
        {
            GameObject panel = CreateUIObject("PF_DebugPanel", null);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.one;
            panelRect.anchorMax = Vector2.one;
            panelRect.pivot = Vector2.one;
            panelRect.sizeDelta = new Vector2(258f, 312f);
            panelRect.anchoredPosition = new Vector2(-36f, -32f);

            HUDDebugLogPanel debugLogPanel = panel.AddComponent<HUDDebugLogPanel>();

            Image backgroundImage = panel.AddComponent<Image>();
            backgroundImage.sprite = squareSprite;
            backgroundImage.color = new Color(0.83f, 0.83f, 0.83f, 0.9f);
            backgroundImage.raycastTarget = false;

            GameObject textObject = CreateUIObject("DebugText", panel.transform);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 1f);
            textRect.sizeDelta = new Vector2(234f, 288f);
            textRect.anchoredPosition = new Vector2(12f, -12f);

            TextMeshProUGUI debugText = textObject.AddComponent<TextMeshProUGUI>();
            debugText.text = string.Empty;
            debugText.font = TMP_Settings.defaultFontAsset;
            debugText.fontSize = 14f;
            debugText.color = Color.black;
            debugText.alignment = TextAlignmentOptions.TopLeft;
            debugText.textWrappingMode = TextWrappingModes.Normal;
            debugText.overflowMode = TextOverflowModes.Ellipsis;
            debugText.raycastTarget = false;

            SerializedObject serializedObject = new(debugLogPanel);
            serializedObject.FindProperty("logText").objectReferenceValue = debugText;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return panel;
        }

        private static GameObject CreateEnemyHealthBarObject(Sprite squareSprite)
        {
            GameObject root = CreateUIObject("PF_EnemyHealthBar", null);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(1.2f, 0.16f);
            rootRect.localScale = Vector3.one;

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 20;

            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            UIEnemyHealthBar enemyHealthBar = root.AddComponent<UIEnemyHealthBar>();
            UIHealthBar healthBar = root.AddComponent<UIHealthBar>();

            GameObject backgroundObject = CreateUIObject("Background", root.transform);
            Stretch(backgroundObject.GetComponent<RectTransform>());

            Image backgroundImage = backgroundObject.AddComponent<Image>();
            backgroundImage.sprite = squareSprite;
            backgroundImage.color = new Color(0.08f, 0.01f, 0.01f, 0.42f);
            backgroundImage.raycastTarget = false;

            GameObject fillObject = CreateUIObject("Fill", backgroundObject.transform);
            Stretch(fillObject.GetComponent<RectTransform>());

            Image fillImage = fillObject.AddComponent<Image>();
            fillImage.sprite = squareSprite;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 1f;
            fillImage.color = new Color(0.36f, 0.02f, 0.02f, 0.68f);
            fillImage.raycastTarget = false;

            SerializedObject enemyHealthBarObject = new(enemyHealthBar);
            enemyHealthBarObject.FindProperty("worldCanvas").objectReferenceValue = canvas;
            enemyHealthBarObject.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            enemyHealthBarObject.FindProperty("healthBar").objectReferenceValue = healthBar;
            enemyHealthBarObject.FindProperty("backgroundImage").objectReferenceValue = backgroundImage;
            enemyHealthBarObject.FindProperty("fillImage").objectReferenceValue = fillImage;
            enemyHealthBarObject.FindProperty("worldOffset").vector3Value = new Vector3(0f, 2f, 0f);
            enemyHealthBarObject.FindProperty("backgroundColor").colorValue = new Color(0.08f, 0.01f, 0.01f, 0.42f);
            enemyHealthBarObject.FindProperty("fillColor").colorValue = new Color(0.36f, 0.02f, 0.02f, 0.68f);
            enemyHealthBarObject.FindProperty("hideWhenFull").boolValue = true;
            enemyHealthBarObject.FindProperty("hideWhenDead").boolValue = true;
            enemyHealthBarObject.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject healthBarObject = new(healthBar);
            healthBarObject.FindProperty("fillImage").objectReferenceValue = fillImage;
            healthBarObject.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            healthBarObject.FindProperty("hideWhenFull").boolValue = false;
            healthBarObject.FindProperty("hideWhenDead").boolValue = true;
            healthBarObject.FindProperty("showValueText").boolValue = false;
            healthBarObject.FindProperty("autoRefresh").boolValue = false;
            healthBarObject.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject gameObject = new(name, typeof(RectTransform));
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            return gameObject;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void SavePanelPrefab(GameObject panel, string prefabPath, bool promptOverwrite)
        {
            try
            {
                if (!CanOverwrite(prefabPath, promptOverwrite))
                {
                    return;
                }

                SetLayerRecursively(panel, LayerMask.NameToLayer("UI"));
                NormalizePanelRootTransform(panel);
                SavePrefab(panel, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(panel);
            }
        }

        private static void SaveWorldUIPrefab(GameObject root, string prefabPath, bool promptOverwrite)
        {
            try
            {
                if (!CanOverwrite(prefabPath, promptOverwrite))
                {
                    return;
                }

                SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
                SavePrefab(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static bool CanOverwrite(string prefabPath, bool promptOverwrite)
        {
            return !promptOverwrite
                || AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null
                || EditorUtility.DisplayDialog(
                    "覆盖 HUD Prefab",
                    $"Prefab 已存在：\n{prefabPath}\n\n是否覆盖？",
                    "覆盖",
                    "取消");
        }

        private static void SavePrefab(GameObject root, string prefabPath)
        {
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool success);
            if (!success || savedPrefab == null)
            {
                Debug.LogError($"无法生成 HUD Prefab：{prefabPath}");
                return;
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = savedPrefab;
            EditorGUIUtility.PingObject(savedPrefab);
            Debug.Log($"已生成 HUD Prefab：{prefabPath}", savedPrefab);
        }

        private static void NormalizePanelRootTransform(GameObject panel)
        {
            if (!panel.TryGetComponent(out RectTransform rectTransform))
            {
                return;
            }

            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localPosition = new Vector3(
                rectTransform.localPosition.x,
                rectTransform.localPosition.y,
                0f);
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
        }

        private static Sprite EnsureSquareSprite()
        {
            EnsureFolder(GeneratedFolder);
            if (!File.Exists(ToAbsolutePath(SquareSpritePath)))
            {
                CreateSquareTexture(SquareSpritePath);
            }

            TextureImporter importer = AssetImporter.GetAtPath(SquareSpritePath) as TextureImporter;
            if (importer != null
                && (importer.textureType != TextureImporterType.Sprite
                    || importer.mipmapEnabled
                    || !importer.alphaIsTransparency))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            else
            {
                AssetDatabase.ImportAsset(SquareSpritePath);
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
        }

        private static void CreateSquareTexture(string assetPath)
        {
            const int size = 64;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            for (int index = 0; index < pixels.Length; index++)
            {
                pixels[index] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(ToAbsolutePath(assetPath), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] segments = folderPath.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.Combine(
                Directory.GetCurrentDirectory(),
                assetPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (layer < 0)
            {
                return;
            }

            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
