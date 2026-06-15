using System.IO;
using EndLink.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EndLink.Editor
{
    /// <summary>
    /// 战斗 HUD UGUI 预制体生成入口。
    /// 这个工具只负责搭建可继续编辑的 UGUI 层级，并把现有 HUD 脚本引用接好。
    /// </summary>
    public static class CombatHUDPrefabGenerator
    {
        private const string PrefabFolder = "Assets/_EndLink/UI/Prefabs";
        private const string GeneratedFolder = "Assets/_EndLink/UI/Generated";
        private const string PartyStatusPanelPath = PrefabFolder + "/PF_PartyStatusPanel.prefab";
        private const string SkillPanelPath = PrefabFolder + "/PF_SkillPanel.prefab";
        private const string UltimatePanelPath = PrefabFolder + "/PF_UltimatePanel.prefab";
        private const string DebugPanelPath = PrefabFolder + "/PF_DebugPanel.prefab";
        private const string CircleSpritePath = GeneratedFolder + "/UI_Circle64.png";
        private const string SquareSpritePath = GeneratedFolder + "/UI_Square64.png";

        [MenuItem("EndLink/UI/Combat HUD/Create All Panels")]
        public static void CreateAllPanels()
        {
            EnsureFolder(PrefabFolder);
            bool hasExistingPanel = AssetDatabase.LoadAssetAtPath<GameObject>(PartyStatusPanelPath) != null
                || AssetDatabase.LoadAssetAtPath<GameObject>(SkillPanelPath) != null
                || AssetDatabase.LoadAssetAtPath<GameObject>(UltimatePanelPath) != null
                || AssetDatabase.LoadAssetAtPath<GameObject>(DebugPanelPath) != null;

            if (hasExistingPanel
                && !EditorUtility.DisplayDialog(
                    "Create Combat HUD Panels",
                    "One or more Combat HUD panel prefabs already exist.\n\nOverwrite them?",
                    "Overwrite",
                    "Cancel"))
            {
                return;
            }

            CreatePartyStatusPanel(false);
            CreateSkillPanel(false);
            CreateUltimatePanel(false);
            CreateDebugPanel(false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Created Combat HUD panel prefabs.");
        }

        [MenuItem("EndLink/UI/Combat HUD/Create Party Status Panel")]
        public static void CreatePartyStatusPanel()
        {
            CreatePartyStatusPanel(true);
        }

        [MenuItem("EndLink/UI/Combat HUD/Create Skill Panel")]
        public static void CreateSkillPanel()
        {
            CreateSkillPanel(true);
        }

        [MenuItem("EndLink/UI/Combat HUD/Create Ultimate Panel")]
        public static void CreateUltimatePanel()
        {
            CreateUltimatePanel(true);
        }

        [MenuItem("EndLink/UI/Combat HUD/Create Debug Panel")]
        public static void CreateDebugPanel()
        {
            CreateDebugPanel(true);
        }

        private static void CreatePartyStatusPanel(bool promptOverwrite)
        {
            EnsureFolder(PrefabFolder);
            Sprite circleSprite = EnsureCircleSprite();
            Sprite squareSprite = EnsureSquareSprite();
            GameObject panel = CreatePartyStatusPanelObject("PF_PartyStatusPanel", null, circleSprite, squareSprite, true);
            SavePanelPrefab(panel, PartyStatusPanelPath, promptOverwrite);
        }

        private static void CreateSkillPanel(bool promptOverwrite)
        {
            EnsureFolder(PrefabFolder);
            Sprite circleSprite = EnsureCircleSprite();
            GameObject panel = CreateSkillPanelObject("PF_SkillPanel", null, circleSprite, true);
            SavePanelPrefab(panel, SkillPanelPath, promptOverwrite);
        }

        private static void CreateUltimatePanel(bool promptOverwrite)
        {
            EnsureFolder(PrefabFolder);
            Sprite squareSprite = EnsureSquareSprite();
            GameObject panel = CreateUltimatePanelObject("PF_UltimatePanel", null, squareSprite, true);
            SavePanelPrefab(panel, UltimatePanelPath, promptOverwrite);
        }

        private static void CreateDebugPanel(bool promptOverwrite)
        {
            EnsureFolder(PrefabFolder);
            Sprite squareSprite = EnsureSquareSprite();
            GameObject panel = CreateDebugPanelObject("PF_DebugPanel", null, squareSprite);
            SavePanelPrefab(panel, DebugPanelPath, promptOverwrite);
        }

        private static GameObject CreatePartyStatusPanelObject(
            string name,
            Transform parent,
            Sprite circleSprite,
            Sprite squareSprite,
            bool autoRefresh)
        {
            GameObject panel = CreateUIObject(name, parent);
            SetTopLeft(panel.GetComponent<RectTransform>(), new Vector2(36f, -32f), new Vector2(520f, 300f));

            CreateText(
                "HealthLabel",
                panel.transform,
                "HP",
                new Vector2(74f, -26f),
                new Vector2(160f, 42f),
                34f,
                TextAlignmentOptions.Center,
                Color.black);

            CreateHealthPlaceholder("Health_Main", panel.transform, squareSprite, new Vector2(0f, -1f), new Vector2(290f, 24f));
            CreateHealthPlaceholder("Health_AllyA", panel.transform, squareSprite, new Vector2(0f, -75f), new Vector2(140f, 24f));
            CreateHealthPlaceholder("Health_AllyB", panel.transform, squareSprite, new Vector2(0f, -148f), new Vector2(140f, 24f));

            CreatePortrait(
                "Portrait_Main",
                panel.transform,
                UIPartyMemberSlot.MainCharacter,
                "1",
                circleSprite,
                new Vector2(312f, -1f),
                78f,
                autoRefresh);

            CreatePortrait(
                "Portrait_AllyA",
                panel.transform,
                UIPartyMemberSlot.AllySlotA,
                "2",
                circleSprite,
                new Vector2(200f, -70f),
                68f,
                autoRefresh);

            CreatePortrait(
                "Portrait_AllyB",
                panel.transform,
                UIPartyMemberSlot.AllySlotB,
                "3",
                circleSprite,
                new Vector2(180f, -182f),
                68f,
                autoRefresh);

            CreateText(
                "PortraitNote",
                panel.transform,
                "Portraits\nGlow = Link",
                new Vector2(270f, -118f),
                new Vector2(260f, 92f),
                30f,
                TextAlignmentOptions.Left,
                Color.black);

            return panel;
        }

        private static GameObject CreateSkillPanelObject(
            string name,
            Transform parent,
            Sprite circleSprite,
            bool autoRefresh)
        {
            GameObject panel = CreateUIObject(name, parent);
            SetBottomLeft(panel.GetComponent<RectTransform>(), new Vector2(36f, 48f), new Vector2(360f, 240f));

            UIPartyCombatAction partyCombatAction = panel.AddComponent<UIPartyCombatAction>();

            UICombatActionSlot playerSkill = CreateActionSlot(
                "Skill_Q",
                panel.transform,
                UICombatActionSlotId.PlayerSkill,
                "Q",
                circleSprite,
                new Color(0.62f, 0.54f, 0.82f, 0.95f));
            SetBottomLeft(playerSkill.GetComponent<RectTransform>(), new Vector2(0f, 150f), new Vector2(78f, 78f));

            UICombatActionSlot allyASkill = CreateActionSlot(
                "Skill_E",
                panel.transform,
                UICombatActionSlotId.AllySlotASkill,
                "E",
                circleSprite,
                new Color(0.58f, 0.69f, 0.86f, 0.95f));
            SetBottomLeft(allyASkill.GetComponent<RectTransform>(), new Vector2(82f, 78f), new Vector2(78f, 78f));

            UICombatActionSlot allyBSkill = CreateActionSlot(
                "Skill_F",
                panel.transform,
                UICombatActionSlotId.AllySlotBSkill,
                "F",
                circleSprite,
                new Color(0.55f, 0.75f, 0.66f, 0.95f));
            SetBottomLeft(allyBSkill.GetComponent<RectTransform>(), new Vector2(162f, 4f), new Vector2(78f, 78f));

            CreateText(
                "SkillLabel",
                panel.transform,
                "Skills",
                new Vector2(118f, 164f),
                new Vector2(180f, 44f),
                30f,
                TextAlignmentOptions.Left,
                Color.black,
                AnchorPreset.BottomLeft);

            SerializedObject partySerializedObject = new(partyCombatAction);
            partySerializedObject.FindProperty("autoCollectChildSlots").boolValue = false;
            partySerializedObject.FindProperty("autoRefresh").boolValue = autoRefresh;
            partySerializedObject.FindProperty("playerSkillSlot").objectReferenceValue = playerSkill;
            partySerializedObject.FindProperty("allySlotASkillSlot").objectReferenceValue = allyASkill;
            partySerializedObject.FindProperty("allySlotBSkillSlot").objectReferenceValue = allyBSkill;
            partySerializedObject.FindProperty("driveChildSlotsManually").boolValue = true;
            partySerializedObject.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static GameObject CreateUltimatePanelObject(
            string name,
            Transform parent,
            Sprite squareSprite,
            bool autoRefresh)
        {
            GameObject panel = CreateUIObject(name, parent);
            SetBottomRight(panel.GetComponent<RectTransform>(), new Vector2(-36f, 42f), new Vector2(390f, 250f));

            UIPartyUltimateBar ultimateBar = panel.AddComponent<UIPartyUltimateBar>();

            GameObject barObject = CreateUIObject("UltimateBar", panel.transform);
            RectTransform barRect = barObject.GetComponent<RectTransform>();
            SetBottomRight(barRect, new Vector2(-20f, 36f), new Vector2(330f, 42f));
            barRect.localEulerAngles = new Vector3(0f, 0f, 42f);

            Image backgroundImage = barObject.AddComponent<Image>();
            backgroundImage.sprite = squareSprite;
            backgroundImage.color = new Color(0.82f, 0.82f, 0.82f, 0.9f);
            backgroundImage.raycastTarget = false;

            GameObject fillObject = CreateUIObject("Fill", barObject.transform);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            Stretch(fillRect);

            Image fillImage = fillObject.AddComponent<Image>();
            fillImage.sprite = squareSprite;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 0f;
            fillImage.color = new Color(0.7f, 0.72f, 0.76f, 0.95f);
            fillImage.raycastTarget = false;

            CreateText(
                "UltimateLabel",
                panel.transform,
                "Ultimate",
                new Vector2(-244f, 112f),
                new Vector2(180f, 42f),
                30f,
                TextAlignmentOptions.Left,
                Color.black,
                AnchorPreset.BottomRight);

            TextMeshProUGUI keyText = CreateText(
                "UltimateKey",
                panel.transform,
                "V",
                new Vector2(-66f, 98f),
                new Vector2(54f, 42f),
                28f,
                TextAlignmentOptions.Center,
                Color.black,
                AnchorPreset.BottomRight);

            TextMeshProUGUI valueText = CreateText(
                "UltimateValue",
                panel.transform,
                "0%",
                new Vector2(-116f, 68f),
                new Vector2(90f, 32f),
                22f,
                TextAlignmentOptions.Center,
                Color.black,
                AnchorPreset.BottomRight);

            SerializedObject serializedObject = new(ultimateBar);
            serializedObject.FindProperty("fillImage").objectReferenceValue = fillImage;
            serializedObject.FindProperty("keyLabelText").objectReferenceValue = keyText;
            serializedObject.FindProperty("valueText").objectReferenceValue = valueText;
            serializedObject.FindProperty("autoRefresh").boolValue = autoRefresh;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static GameObject CreateDebugPanelObject(string name, Transform parent, Sprite squareSprite)
        {
            GameObject panel = CreateUIObject(name, parent);
            SetTopRight(panel.GetComponent<RectTransform>(), new Vector2(-36f, -32f), new Vector2(258f, 312f));

            HUDDebugLogPanel debugLogPanel = panel.AddComponent<HUDDebugLogPanel>();

            Image backgroundImage = panel.AddComponent<Image>();
            backgroundImage.sprite = squareSprite;
            backgroundImage.color = new Color(0.83f, 0.83f, 0.83f, 0.9f);
            backgroundImage.raycastTarget = false;

            TextMeshProUGUI debugText = CreateText(
                "DebugText",
                panel.transform,
                "Debug\nInfo",
                new Vector2(12f, -12f),
                new Vector2(234f, 288f),
                14f,
                TextAlignmentOptions.TopLeft,
                Color.black,
                AnchorPreset.TopLeft);

            debugText.textWrappingMode = TextWrappingModes.Normal;
            debugText.overflowMode = TextOverflowModes.Ellipsis;

            SerializedObject serializedObject = new(debugLogPanel);
            serializedObject.FindProperty("logText").objectReferenceValue = debugText;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static UIPartyMemberPortrait CreatePortrait(
            string name,
            Transform parent,
            UIPartyMemberSlot memberSlot,
            string keyLabel,
            Sprite circleSprite,
            Vector2 anchoredPosition,
            float portraitSize,
            bool autoRefresh)
        {
            GameObject portraitObject = CreateUIObject(name, parent);
            SetTopLeft(portraitObject.GetComponent<RectTransform>(), anchoredPosition, new Vector2(portraitSize + 18f, portraitSize + 18f));

            UIPartyMemberPortrait portrait = portraitObject.AddComponent<UIPartyMemberPortrait>();

            GameObject highlightObject = CreateUIObject("Highlight", portraitObject.transform);
            RectTransform highlightRect = highlightObject.GetComponent<RectTransform>();
            Stretch(highlightRect);

            Image highlightImage = highlightObject.AddComponent<Image>();
            highlightImage.sprite = circleSprite;
            highlightImage.color = new Color(1f, 0.88f, 0.35f, 0f);
            highlightImage.raycastTarget = false;

            GameObject iconObject = CreateUIObject("Icon", portraitObject.transform);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(portraitSize, portraitSize);
            iconRect.anchoredPosition = Vector2.zero;

            Image portraitImage = iconObject.AddComponent<Image>();
            portraitImage.sprite = circleSprite;
            portraitImage.color = new Color(0.82f, 0.82f, 0.82f, 1f);
            portraitImage.raycastTarget = false;

            TextMeshProUGUI keyText = CreateText(
                "LinkKey",
                portraitObject.transform,
                keyLabel,
                new Vector2(0f, -portraitSize * 0.5f - 9f),
                new Vector2(50f, 24f),
                18f,
                TextAlignmentOptions.Center,
                Color.black,
                AnchorPreset.Center);

            SerializedObject serializedObject = new(portrait);
            serializedObject.FindProperty("memberSlot").enumValueIndex = (int)memberSlot;
            serializedObject.FindProperty("portraitImage").objectReferenceValue = portraitImage;
            serializedObject.FindProperty("highlightImage").objectReferenceValue = highlightImage;
            serializedObject.FindProperty("keyLabelText").objectReferenceValue = keyText;
            serializedObject.FindProperty("autoRefresh").boolValue = autoRefresh;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            return portrait;
        }

        private static void CreateHealthPlaceholder(
            string name,
            Transform parent,
            Sprite squareSprite,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject barObject = CreateUIObject(name, parent);
            SetTopLeft(barObject.GetComponent<RectTransform>(), anchoredPosition, size);

            Image image = barObject.AddComponent<Image>();
            image.sprite = squareSprite;
            image.color = new Color(0.2f, 0.75f, 0.22f, 1f);
            image.raycastTarget = false;
        }

        private static UICombatActionSlot CreateActionSlot(
            string name,
            Transform parent,
            UICombatActionSlotId slotId,
            string keyLabel,
            Sprite circleSprite,
            Color color,
            float size = 68f,
            float keyFontSize = 24f)
        {
            GameObject slotObject = CreateUIObject(name, parent);
            RectTransform slotRect = slotObject.GetComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(size, size);

            LayoutElement layoutElement = slotObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = size;
            layoutElement.preferredHeight = size;

            Image iconImage = slotObject.AddComponent<Image>();
            iconImage.sprite = circleSprite;
            iconImage.color = color;
            iconImage.raycastTarget = false;

            UICombatActionSlot actionSlot = slotObject.AddComponent<UICombatActionSlot>();

            GameObject keyObject = CreateUIObject("KeyLabel", slotObject.transform);
            RectTransform keyRect = keyObject.GetComponent<RectTransform>();
            Stretch(keyRect);

            TextMeshProUGUI keyText = keyObject.AddComponent<TextMeshProUGUI>();
            keyText.text = keyLabel;
            keyText.font = TMP_Settings.defaultFontAsset;
            keyText.fontStyle = FontStyles.Bold;
            keyText.fontSize = keyFontSize;
            keyText.color = new Color(0.1f, 0.1f, 0.12f, 1f);
            keyText.alignment = TextAlignmentOptions.Center;
            keyText.raycastTarget = false;

            SerializedObject slotSerializedObject = new(actionSlot);
            slotSerializedObject.FindProperty("slot").enumValueIndex = (int)slotId;
            slotSerializedObject.FindProperty("iconImage").objectReferenceValue = iconImage;
            slotSerializedObject.FindProperty("keyLabelText").objectReferenceValue = keyText;
            slotSerializedObject.FindProperty("cooldownTintColor").colorValue = new Color(0.45f, 0.45f, 0.45f, 0.65f);
            slotSerializedObject.FindProperty("autoRefreshCooldown").boolValue = false;
            slotSerializedObject.FindProperty("autoRefreshKeyLabel").boolValue = false;
            slotSerializedObject.ApplyModifiedPropertiesWithoutUndo();

            return actionSlot;
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

        private static void SetTopLeft(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = anchoredPosition;
        }

        private static void SetTopRight(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = anchoredPosition;
        }

        private static void SetBottomLeft(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.zero;
            rectTransform.pivot = Vector2.zero;
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = anchoredPosition;
        }

        private static void SetBottomRight(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(1f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(1f, 0f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = anchoredPosition;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string text,
            Vector2 anchoredPosition,
            Vector2 size,
            float fontSize,
            TextAlignmentOptions alignment,
            Color color,
            AnchorPreset anchorPreset = AnchorPreset.TopLeft)
        {
            GameObject textObject = CreateUIObject(name, parent);
            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            ApplyAnchorPreset(rectTransform, anchorPreset, anchoredPosition, size);

            TextMeshProUGUI textComponent = textObject.AddComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.font = TMP_Settings.defaultFontAsset;
            textComponent.fontSize = fontSize;
            textComponent.color = color;
            textComponent.alignment = alignment;
            textComponent.raycastTarget = false;
            return textComponent;
        }

        private static void ApplyAnchorPreset(
            RectTransform rectTransform,
            AnchorPreset anchorPreset,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            switch (anchorPreset)
            {
                case AnchorPreset.TopRight:
                    SetTopRight(rectTransform, anchoredPosition, size);
                    break;
                case AnchorPreset.BottomLeft:
                    SetBottomLeft(rectTransform, anchoredPosition, size);
                    break;
                case AnchorPreset.BottomRight:
                    SetBottomRight(rectTransform, anchoredPosition, size);
                    break;
                case AnchorPreset.Center:
                    rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    rectTransform.sizeDelta = size;
                    rectTransform.anchoredPosition = anchoredPosition;
                    break;
                default:
                    SetTopLeft(rectTransform, anchoredPosition, size);
                    break;
            }
        }

        private static void SavePanelPrefab(GameObject panel, string prefabPath, bool promptOverwrite)
        {
            try
            {
                if (promptOverwrite
                    && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null
                    && !EditorUtility.DisplayDialog(
                        "Create Combat HUD Panel",
                        $"Prefab already exists:\n{prefabPath}\n\nOverwrite it?",
                        "Overwrite",
                        "Cancel"))
                {
                    return;
                }

                SetLayerRecursively(panel, LayerMask.NameToLayer("UI"));
                NormalizePanelRootTransform(panel);

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(panel, prefabPath, out bool success);
                if (!success || savedPrefab == null)
                {
                    Debug.LogError($"Failed to create Combat HUD panel prefab at {prefabPath}.");
                    return;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = savedPrefab;
                EditorGUIUtility.PingObject(savedPrefab);
                Debug.Log($"Created Combat HUD panel prefab: {prefabPath}", savedPrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(panel);
            }
        }

        private static void NormalizePanelRootTransform(GameObject panel)
        {
            if (panel == null || !panel.TryGetComponent(out RectTransform rectTransform))
            {
                return;
            }

            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localPosition = new Vector3(rectTransform.localPosition.x, rectTransform.localPosition.y, 0f);
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
        }

        private static Sprite EnsureCircleSprite()
        {
            EnsureFolder(GeneratedFolder);

            if (!File.Exists(ToAbsolutePath(CircleSpritePath)))
            {
                CreateCircleTexture(CircleSpritePath);
            }

            TextureImporter importer = AssetImporter.GetAtPath(CircleSpritePath) as TextureImporter;
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
                AssetDatabase.ImportAsset(CircleSpritePath);
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath);
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

        private static void CreateCircleTexture(string assetPath)
        {
            const int size = 64;
            const float radius = size * 0.5f - 1f;
            Vector2 center = new(size * 0.5f, size * 0.5f);

            Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pixelCenter = new(x + 0.5f, y + 0.5f);
                    float distance = Vector2.Distance(pixelCenter, center);
                    float alpha = Mathf.Clamp01(radius + 1f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            File.WriteAllBytes(ToAbsolutePath(assetPath), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath);
        }

        private static void CreateSquareTexture(string assetPath)
        {
            const int size = 64;

            Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }

            texture.Apply();
            File.WriteAllBytes(ToAbsolutePath(assetPath), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
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
            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
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

        private enum AnchorPreset
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
            Center
        }
    }
}
