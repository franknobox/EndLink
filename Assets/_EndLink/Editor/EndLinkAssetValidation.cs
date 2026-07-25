using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace EndLink.Editor
{
    internal enum EndLinkValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    internal sealed class EndLinkValidationIssue
    {
        public EndLinkValidationIssue(
            EndLinkValidationSeverity severity,
            string code,
            string assetPath,
            string message)
        {
            Severity = severity;
            Code = code;
            AssetPath = assetPath;
            Message = message;
        }

        public EndLinkValidationSeverity Severity { get; }

        public string Code { get; }

        public string AssetPath { get; }

        public string Message { get; }

        public string Key => $"{Severity}|{Code}|{AssetPath}|{Message}";
    }

    /// <summary>
    /// EndLink 美术资源静态校验入口。
    /// 第一版只报告问题，不自动修改 Shader、模型、贴图或 Prefab。
    /// </summary>
    [InitializeOnLoad]
    internal static class EndLinkAssetValidation
    {
        public const string IncomingRoot = "Assets/Art/_Incoming";
        public const string ArtRoot = "Assets/Art";

        private const int MaxAutomaticConsoleIssues = 20;

        private static readonly List<EndLinkValidationIssue> IssuesInternal = new();
        private static readonly HashSet<string> IssueKeys = new(StringComparer.Ordinal);
        private static bool _incomingScanQueued;

        static EndLinkAssetValidation()
        {
            EditorApplication.projectChanged += HandleProjectChanged;
        }

        public static event Action IssuesChanged;

        public static IReadOnlyList<EndLinkValidationIssue> Issues => IssuesInternal;

        public static string LastScannedRoot { get; private set; } = IncomingRoot;

        public static DateTime? LastScanTime { get; private set; }

        public static void ScanIncoming(bool logToConsole)
        {
            ScanRoot(IncomingRoot, logToConsole);
        }

        public static void ScanAllArt(bool logToConsole)
        {
            ScanRoot(ArtRoot, logToConsole);
        }

        public static bool IsIncomingPath(string assetPath)
        {
            return IsPathUnderRoot(assetPath, IncomingRoot);
        }

        public static void ScheduleIncomingScan()
        {
            if (_incomingScanQueued)
            {
                return;
            }

            _incomingScanQueued = true;
            EditorApplication.delayCall += RunQueuedIncomingScan;
        }

        private static void HandleProjectChanged()
        {
            if (LastScannedRoot == IncomingRoot)
            {
                IssuesChanged?.Invoke();
            }
        }

        private static void RunQueuedIncomingScan()
        {
            _incomingScanQueued = false;
            ScanIncoming(true);
        }

        private static void ScanRoot(string rootPath, bool logToConsole)
        {
            IssuesInternal.Clear();
            IssueKeys.Clear();
            LastScannedRoot = rootPath;
            LastScanTime = DateTime.Now;

            if (!AssetDatabase.IsValidFolder(rootPath))
            {
                AddIssue(
                    EndLinkValidationSeverity.Error,
                    "VAL_ROOT_MISSING",
                    rootPath,
                    $"校验目录不存在：{rootPath}");
                FinishScan(logToConsole);
                return;
            }

            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { rootPath });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(assetPath)
                    || AssetDatabase.IsValidFolder(assetPath))
                {
                    continue;
                }

                ValidateAsset(assetPath);
            }

            IssuesInternal.Sort(CompareIssues);
            FinishScan(logToConsole);
        }

        private static void ValidateAsset(string assetPath)
        {
            ValidateIncomingName(assetPath);

            string extension = Path.GetExtension(assetPath).ToLowerInvariant();
            switch (extension)
            {
                case ".mat":
                    ValidateMaterialAsset(assetPath);
                    break;
                case ".prefab":
                    ValidatePrefab(assetPath);
                    break;
                case ".fbx":
                    ValidateModel(assetPath);
                    break;
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".tga":
                case ".tif":
                case ".tiff":
                case ".psd":
                case ".exr":
                    ValidateTexture(assetPath);
                    break;
                case ".unity":
                    if (IsIncomingPath(assetPath))
                    {
                        AddIssue(
                            EndLinkValidationSeverity.Warning,
                            "ART_SCENE_IN_INCOMING",
                            assetPath,
                            "美术临时导入区中包含 Unity 场景。正式场景不应随美术资源包直接进入工程。");
                    }
                    break;
            }
        }

        private static void ValidateIncomingName(string assetPath)
        {
            if (!IsIncomingPath(assetPath))
            {
                return;
            }

            string relativePath = assetPath[(IncomingRoot.Length + 1)..];
            if (relativePath.Any(character => character > 127))
            {
                AddIssue(
                    EndLinkValidationSeverity.Info,
                    "ART_TEMP_NON_ASCII_NAME",
                    assetPath,
                    "资源仍使用中文或其他非 ASCII 临时命名，移入正式目录前需要统一工程命名。");
            }

            string fileName = Path.GetFileName(assetPath);
            if (fileName.IndexOfAny(new[] { '(', ')', '[', ']' }) >= 0)
            {
                AddIssue(
                    EndLinkValidationSeverity.Warning,
                    "ART_DUPLICATE_STYLE_NAME",
                    assetPath,
                    "文件名包含括号或编号副本格式，可能是重复导出资源，转正前需要确认并重命名。");
            }

            if (relativePath.Contains(' '))
            {
                AddIssue(
                    EndLinkValidationSeverity.Info,
                    "ART_NAME_HAS_SPACE",
                    assetPath,
                    "资源路径包含空格，转入正式目录时建议改为下划线或稳定的语义命名。");
            }
        }

        private static void ValidateMaterialAsset(string assetPath)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                AddIssue(
                    EndLinkValidationSeverity.Error,
                    "MAT_LOAD_FAILED",
                    assetPath,
                    "材质资源无法被 Unity 正常加载。");
                return;
            }

            ValidateMaterial(material, assetPath, material.name);
        }

        private static void ValidateMaterial(
            Material material,
            string ownerAssetPath,
            string materialLabel)
        {
            if (material == null)
            {
                AddIssue(
                    EndLinkValidationSeverity.Error,
                    "MAT_REFERENCE_MISSING",
                    ownerAssetPath,
                    $"存在丢失的材质引用：{materialLabel}");
                return;
            }

            Shader shader = material.shader;
            if (shader == null)
            {
                AddIssue(
                    EndLinkValidationSeverity.Error,
                    "MAT_SHADER_MISSING",
                    ownerAssetPath,
                    $"材质 {material.name} 没有有效 Shader。");
                return;
            }

            string shaderName = shader.name ?? string.Empty;
            if (shaderName == "Hidden/InternalErrorShader")
            {
                AddIssue(
                    EndLinkValidationSeverity.Error,
                    "MAT_ERROR_SHADER",
                    ownerAssetPath,
                    $"材质 {material.name} 正在使用 Unity 错误 Shader，通常表示原 Shader 丢失或编译失败。");
                return;
            }

            if (!shader.isSupported)
            {
                AddIssue(
                    EndLinkValidationSeverity.Error,
                    "MAT_SHADER_UNSUPPORTED",
                    ownerAssetPath,
                    $"材质 {material.name} 使用的 Shader 不受当前平台或渲染管线支持：{shaderName}");
            }

            if (IsUniversalRenderPipelineActive() && IsLegacyOrBuiltInShader(shaderName))
            {
                AddIssue(
                    EndLinkValidationSeverity.Warning,
                    "MAT_LEGACY_SHADER",
                    ownerAssetPath,
                    $"材质 {material.name} 在 URP 工程中使用旧版或 Built-in Shader：{shaderName}。需要人工确认透明、混合和粒子表现后再替换。");
            }
        }

        private static void ValidatePrefab(string assetPath)
        {
            GameObject prefabRoot = null;
            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
                if (prefabRoot == null)
                {
                    AddIssue(
                        EndLinkValidationSeverity.Error,
                        "PREFAB_LOAD_FAILED",
                        assetPath,
                        "Prefab 无法被 Unity 正常加载。");
                    return;
                }

                foreach (Transform child in prefabRoot.GetComponentsInChildren<Transform>(true))
                {
                    int missingScriptCount =
                        GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
                    if (missingScriptCount > 0)
                    {
                        AddIssue(
                            EndLinkValidationSeverity.Error,
                            "PREFAB_MISSING_SCRIPT",
                            assetPath,
                            $"Prefab 物体 {GetHierarchyPath(child, prefabRoot.transform)} 存在 {missingScriptCount} 个 Missing Script。");
                    }
                }

                foreach (Renderer renderer in prefabRoot.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int index = 0; index < materials.Length; index++)
                    {
                        Material material = materials[index];
                        string label = $"{renderer.name} / Material {index}";
                        ValidateMaterial(material, assetPath, label);
                    }
                }

                if (IsIncomingPath(assetPath))
                {
                    HashSet<string> attachedScriptTypes = new(StringComparer.Ordinal);
                    foreach (MonoBehaviour behaviour in
                             prefabRoot.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (behaviour == null)
                        {
                            continue;
                        }

                        Type behaviourType = behaviour.GetType();
                        attachedScriptTypes.Add(behaviourType.FullName ?? behaviourType.Name);
                    }

                    if (attachedScriptTypes.Count > 0)
                    {
                        AddIssue(
                            EndLinkValidationSeverity.Warning,
                            "ART_PREFAB_HAS_SCRIPT",
                            assetPath,
                            "美术临时 Prefab 挂载了脚本，需要确认转正后是否仍然需要："
                            + string.Join(", ", attachedScriptTypes.OrderBy(name => name)));
                    }
                }
            }
            catch (Exception exception)
            {
                AddIssue(
                    EndLinkValidationSeverity.Error,
                    "PREFAB_VALIDATION_EXCEPTION",
                    assetPath,
                    $"校验 Prefab 时发生异常：{exception.Message}");
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        private static void ValidateModel(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not ModelImporter importer)
            {
                AddIssue(
                    EndLinkValidationSeverity.Error,
                    "MODEL_IMPORTER_MISSING",
                    assetPath,
                    "FBX 没有有效的 ModelImporter。");
                return;
            }

            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            bool isAnimationAsset =
                fileName.StartsWith("ANI_", StringComparison.OrdinalIgnoreCase);

            if (isAnimationAsset && !importer.importAnimation)
            {
                AddIssue(
                    EndLinkValidationSeverity.Error,
                    "ANIM_IMPORT_DISABLED",
                    assetPath,
                    "动画 FBX 已按 ANI_ 命名，但 Import Animation 处于关闭状态。");
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (IsDefaultAnimationName(clip.name))
                {
                    AddIssue(
                        EndLinkValidationSeverity.Warning,
                        "ANIM_DEFAULT_CLIP_NAME",
                        assetPath,
                        $"动画 Clip 仍使用导出器默认名称：{clip.name}。应改为稳定的动作语义名称。");
                }

            }

            if (importer.animationType == ModelImporterAnimationType.Human)
            {
                Avatar avatar = AssetDatabase
                    .LoadAllAssetsAtPath(assetPath)
                    .OfType<Avatar>()
                    .FirstOrDefault();

                if (avatar == null || !avatar.isValid || !avatar.isHuman)
                {
                    AddIssue(
                        EndLinkValidationSeverity.Error,
                        "MODEL_HUMANOID_AVATAR_INVALID",
                        assetPath,
                        "FBX 被配置为 Humanoid，但没有生成有效的人形 Avatar。需要检查骨骼映射或 Avatar Definition。");
                }
            }
        }

        private static void ValidateTexture(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            {
                return;
            }

            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            bool looksLikeNormalMap =
                fileName.EndsWith("_N", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith("_Normal", StringComparison.OrdinalIgnoreCase);

            if (looksLikeNormalMap && importer.textureType != TextureImporterType.NormalMap)
            {
                AddIssue(
                    EndLinkValidationSeverity.Warning,
                    "TEX_NORMAL_TYPE_MISMATCH",
                    assetPath,
                    "贴图命名表示 Normal Map，但 Texture Type 不是 Normal Map。");
            }

            if (importer.maxTextureSize > 4096)
            {
                AddIssue(
                    EndLinkValidationSeverity.Info,
                    "TEX_LARGE_MAX_SIZE",
                    assetPath,
                    $"贴图最大导入尺寸为 {importer.maxTextureSize}，转正前需要确认是否确实需要超过 4096。");
            }
        }

        private static bool IsUniversalRenderPipelineActive()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            return pipeline != null
                && pipeline.GetType().Name.Contains(
                    "UniversalRenderPipeline",
                    StringComparison.Ordinal);
        }

        private static bool IsLegacyOrBuiltInShader(string shaderName)
        {
            return shaderName.StartsWith("Legacy Shaders/", StringComparison.Ordinal)
                || shaderName.Equals("Standard", StringComparison.Ordinal)
                || shaderName.Equals("Standard (Specular setup)", StringComparison.Ordinal)
                || shaderName.StartsWith("Particles/Standard", StringComparison.Ordinal)
                || shaderName.StartsWith("Mobile/Particles/", StringComparison.Ordinal);
        }

        private static bool IsDefaultAnimationName(string clipName)
        {
            if (string.IsNullOrWhiteSpace(clipName))
            {
                return true;
            }

            return clipName.Equals("mixamo.com", StringComparison.OrdinalIgnoreCase)
                || clipName.StartsWith("Take 0", StringComparison.OrdinalIgnoreCase)
                || clipName.StartsWith("Armature|", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetHierarchyPath(Transform target, Transform root)
        {
            if (target == root)
            {
                return root.name;
            }

            Stack<string> names = new();
            Transform current = target;
            while (current != null && current != root)
            {
                names.Push(current.name);
                current = current.parent;
            }

            names.Push(root.name);
            return string.Join("/", names);
        }

        private static bool IsPathUnderRoot(string assetPath, string rootPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            return assetPath.Equals(rootPath, StringComparison.Ordinal)
                || assetPath.StartsWith($"{rootPath}/", StringComparison.Ordinal);
        }

        private static int CompareIssues(
            EndLinkValidationIssue left,
            EndLinkValidationIssue right)
        {
            int severityComparison = right.Severity.CompareTo(left.Severity);
            if (severityComparison != 0)
            {
                return severityComparison;
            }

            int pathComparison = string.Compare(
                left.AssetPath,
                right.AssetPath,
                StringComparison.Ordinal);
            return pathComparison != 0
                ? pathComparison
                : string.Compare(left.Code, right.Code, StringComparison.Ordinal);
        }

        private static void AddIssue(
            EndLinkValidationSeverity severity,
            string code,
            string assetPath,
            string message)
        {
            EndLinkValidationIssue issue =
                new(severity, code, assetPath, message);
            if (!IssueKeys.Add(issue.Key))
            {
                return;
            }

            IssuesInternal.Add(issue);
        }

        private static void FinishScan(bool logToConsole)
        {
            IssuesChanged?.Invoke();

            if (!logToConsole)
            {
                return;
            }

            LogIssuesToConsole();
        }

        private static void LogIssuesToConsole()
        {
            int errorCount = IssuesInternal.Count(issue =>
                issue.Severity == EndLinkValidationSeverity.Error);
            int warningCount = IssuesInternal.Count(issue =>
                issue.Severity == EndLinkValidationSeverity.Warning);

            if (errorCount == 0 && warningCount == 0)
            {
                Debug.Log($"EndLink Asset Validator：{LastScannedRoot} 未发现资源错误或警告。");
                return;
            }

            IEnumerable<EndLinkValidationIssue> consoleIssues = IssuesInternal
                .Where(issue => issue.Severity != EndLinkValidationSeverity.Info)
                .Take(MaxAutomaticConsoleIssues);

            foreach (EndLinkValidationIssue issue in consoleIssues)
            {
                Object context = AssetDatabase.LoadMainAssetAtPath(issue.AssetPath);
                string logMessage =
                    $"EndLink Asset Validator [{issue.Code}]\n{issue.Message}\n{issue.AssetPath}";
                if (issue.Severity == EndLinkValidationSeverity.Error)
                {
                    Debug.LogError(logMessage, context);
                }
                else
                {
                    Debug.LogWarning(logMessage, context);
                }
            }

            int reportableCount = errorCount + warningCount;
            if (reportableCount > MaxAutomaticConsoleIssues)
            {
                Debug.LogWarning(
                    $"EndLink Asset Validator：共发现 {errorCount} 个错误、{warningCount} 个警告，"
                    + $"Console 只显示前 {MaxAutomaticConsoleIssues} 条。"
                    + "完整结果请打开 EndLink > Validation > Asset Validator。");
            }
        }
    }
}
