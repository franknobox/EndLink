using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EndLink.Combat;
using EndLink.Core;
using EndLink.Enemies;
using EndLink.World;
using UnityEditor;
using UnityEngine;

namespace EndLink.Editor
{
    /// <summary>
    /// Scene Doctor 的白盒资源、空间对齐和场景归类规则。
    /// 这里只读取场景与 Prefab Override，不执行任何自动修复。
    /// </summary>
    internal static class SceneBlockoutValidation
    {
        private const float NearZeroScale = 0.001f;
        private const float HugeLocalPosition = 1000f;
        private const float MinCenterOffsetWarning = 0.25f;
        private const float RelativeCenterOffsetWarning = 0.25f;
        private const float RelativeSizeDifferenceWarning = 0.5f;

        private static readonly string[] TransformPropertyPrefixes =
        {
            "m_LocalPosition",
            "m_LocalRotation",
            "m_LocalScale"
        };

        internal static void Validate(
            IReadOnlyList<GameObject> sceneObjects,
            ICollection<SceneValidationIssue> issues)
        {
            ValidateBoxPrefabTransforms(sceneObjects, issues);
            ValidateMeshColliderAlignment(sceneObjects, issues);
            ValidateHierarchyPlacement(sceneObjects, issues);
            ValidateBlockoutLayersAndTags(sceneObjects, issues);
        }

        private static void ValidateBoxPrefabTransforms(
            IEnumerable<GameObject> sceneObjects,
            ICollection<SceneValidationIssue> issues)
        {
            foreach (GameObject instanceRoot in sceneObjects.Where(IsBoxPrefabInstanceRoot))
            {
                PropertyModification[] modifications =
                    PrefabUtility.GetPropertyModifications(instanceRoot)
                    ?? Array.Empty<PropertyModification>();

                foreach (Transform child in instanceRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (child == instanceRoot.transform)
                    {
                        continue;
                    }

                    ValidateChildTransformValues(child, issues);
                    ValidateChildTransformOverrides(child, modifications, issues);
                }
            }
        }

        private static bool IsBoxPrefabInstanceRoot(GameObject gameObject)
        {
            if (gameObject == null || !PrefabUtility.IsAnyPrefabInstanceRoot(gameObject))
            {
                return false;
            }

            string prefabPath =
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
            return !string.IsNullOrWhiteSpace(prefabPath)
                && Path.GetFileNameWithoutExtension(prefabPath)
                    .StartsWith("BOX_", StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateChildTransformOverrides(
            Transform child,
            IEnumerable<PropertyModification> modifications,
            ICollection<SceneValidationIssue> issues)
        {
            Transform sourceTransform =
                PrefabUtility.GetCorrespondingObjectFromSource(child);
            if (sourceTransform == null)
            {
                return;
            }

            string[] overriddenProperties = modifications
                .Where(modification => modification != null
                    && modification.target == sourceTransform
                    && IsTransformProperty(modification.propertyPath))
                .Select(modification => GetTransformPropertyLabel(modification.propertyPath))
                .Distinct()
                .ToArray();

            if (overriddenProperties.Length == 0)
            {
                return;
            }

            SceneValidator.Add(
                issues,
                SceneValidationSeverity.Warning,
                "PREFAB_CHILD_TRANSFORM_OVERRIDE",
                "Prefab",
                $"BOX_ Prefab 子物体存在 Transform Override：{string.Join("、", overriddenProperties)}。"
                + "根节点变换属于正常关卡摆放，子节点变换请确认是否为有意调整。",
                child);
        }

        private static bool IsTransformProperty(string propertyPath)
        {
            return !string.IsNullOrWhiteSpace(propertyPath)
                && TransformPropertyPrefixes.Any(prefix =>
                    propertyPath.StartsWith(prefix, StringComparison.Ordinal));
        }

        private static string GetTransformPropertyLabel(string propertyPath)
        {
            if (propertyPath.StartsWith("m_LocalPosition", StringComparison.Ordinal))
            {
                return "Position";
            }

            if (propertyPath.StartsWith("m_LocalRotation", StringComparison.Ordinal))
            {
                return "Rotation";
            }

            return "Scale";
        }

        private static void ValidateChildTransformValues(
            Transform child,
            ICollection<SceneValidationIssue> issues)
        {
            Vector3 scale = child.localScale;
            if (Mathf.Abs(scale.x) <= NearZeroScale
                || Mathf.Abs(scale.y) <= NearZeroScale
                || Mathf.Abs(scale.z) <= NearZeroScale)
            {
                SceneValidator.Add(
                    issues,
                    SceneValidationSeverity.Warning,
                    "PREFAB_CHILD_SCALE_NEAR_ZERO",
                    "Prefab",
                    $"BOX_ Prefab 子物体缩放接近 0：{FormatVector(scale)}。"
                    + "这通常会让网格或碰撞退化。",
                    child);
            }

            if (scale.x < 0f || scale.y < 0f || scale.z < 0f)
            {
                SceneValidator.Add(
                    issues,
                    SceneValidationSeverity.Warning,
                    "PREFAB_CHILD_SCALE_NEGATIVE",
                    "Prefab",
                    $"BOX_ Prefab 子物体使用负缩放：{FormatVector(scale)}。"
                    + "请确认法线、碰撞和子节点朝向是否仍正确。",
                    child);
            }

            Vector3 position = child.localPosition;
            if (Mathf.Abs(position.x) > HugeLocalPosition
                || Mathf.Abs(position.y) > HugeLocalPosition
                || Mathf.Abs(position.z) > HugeLocalPosition)
            {
                SceneValidator.Add(
                    issues,
                    SceneValidationSeverity.Warning,
                    "PREFAB_CHILD_POSITION_HUGE",
                    "Prefab",
                    $"BOX_ Prefab 子物体局部坐标异常巨大：{FormatVector(position)}。"
                    + $"任一轴超过 {HugeLocalPosition:0} 米。",
                    child);
            }
        }

        private static void ValidateMeshColliderAlignment(
            IEnumerable<GameObject> sceneObjects,
            ICollection<SceneValidationIssue> issues)
        {
            foreach (GameObject sceneObject in sceneObjects)
            {
                MeshFilter meshFilter = sceneObject.GetComponent<MeshFilter>();
                MeshRenderer renderer = sceneObject.GetComponent<MeshRenderer>();
                if (meshFilter == null
                    || renderer == null
                    || !renderer.enabled
                    || renderer.forceRenderingOff
                    || sceneObject.GetComponentInParent<ObjInteractable>() != null)
                {
                    continue;
                }

                Collider[] colliders = sceneObject.GetComponents<Collider>()
                    .Where(collider => collider != null
                        && collider.enabled
                        && !collider.isTrigger
                        && collider is not CharacterController)
                    .ToArray();
                if (colliders.Length == 0)
                {
                    continue;
                }

                ValidateMeshReferences(meshFilter, colliders, issues);
                if (sceneObject.activeInHierarchy)
                {
                    ValidateBounds(renderer, colliders, issues);
                }
            }
        }

        private static void ValidateMeshReferences(
            MeshFilter meshFilter,
            IEnumerable<Collider> colliders,
            ICollection<SceneValidationIssue> issues)
        {
            foreach (MeshCollider meshCollider in colliders.OfType<MeshCollider>())
            {
                if (meshFilter.sharedMesh == meshCollider.sharedMesh)
                {
                    continue;
                }

                string visualMeshName = meshFilter.sharedMesh != null
                    ? meshFilter.sharedMesh.name
                    : "<空>";
                string colliderMeshName = meshCollider.sharedMesh != null
                    ? meshCollider.sharedMesh.name
                    : "<空>";
                SceneValidator.Add(
                    issues,
                    SceneValidationSeverity.Error,
                    "GEOMETRY_MESH_COLLIDER_MISMATCH",
                    "几何",
                    $"同物体 MeshFilter 与 MeshCollider 引用了不同网格："
                    + $"视觉={visualMeshName}，碰撞={colliderMeshName}。",
                    meshCollider);
            }
        }

        private static void ValidateBounds(
            MeshRenderer renderer,
            IReadOnlyList<Collider> colliders,
            ICollection<SceneValidationIssue> issues)
        {
            Bounds rendererBounds = renderer.bounds;
            Bounds colliderBounds = colliders[0].bounds;
            for (int index = 1; index < colliders.Count; index++)
            {
                colliderBounds.Encapsulate(colliders[index].bounds);
            }

            if (!rendererBounds.Intersects(colliderBounds))
            {
                SceneValidator.Add(
                    issues,
                    SceneValidationSeverity.Error,
                    "GEOMETRY_BOUNDS_DISJOINT",
                    "几何",
                    $"Renderer 与非 Trigger Collider 的世界 Bounds 完全不相交。"
                    + $"中心距离={Vector3.Distance(rendererBounds.center, colliderBounds.center):0.00}m，"
                    + $"视觉尺寸={FormatVector(rendererBounds.size)}，碰撞尺寸={FormatVector(colliderBounds.size)}。",
                    renderer);
                return;
            }

            float centerDistance =
                Vector3.Distance(rendererBounds.center, colliderBounds.center);
            float centerThreshold = Mathf.Max(
                MinCenterOffsetWarning,
                rendererBounds.extents.magnitude * RelativeCenterOffsetWarning);
            float sizeDifference = GetMaximumRelativeSizeDifference(
                rendererBounds.size,
                colliderBounds.size);
            if (centerDistance <= centerThreshold
                && sizeDifference <= RelativeSizeDifferenceWarning)
            {
                return;
            }

            SceneValidator.Add(
                issues,
                SceneValidationSeverity.Warning,
                "GEOMETRY_BOUNDS_OFFSET",
                "几何",
                $"Renderer 与 Collider 虽有相交，但空间差异较大："
                + $"中心偏移={centerDistance:0.00}m（阈值 {centerThreshold:0.00}m），"
                + $"最大尺寸差={sizeDifference:P0}，"
                + $"视觉尺寸={FormatVector(rendererBounds.size)}，碰撞尺寸={FormatVector(colliderBounds.size)}。",
                renderer);
        }

        private static float GetMaximumRelativeSizeDifference(
            Vector3 rendererSize,
            Vector3 colliderSize)
        {
            return Mathf.Max(
                GetRelativeDifference(rendererSize.x, colliderSize.x),
                GetRelativeDifference(rendererSize.y, colliderSize.y),
                GetRelativeDifference(rendererSize.z, colliderSize.z));
        }

        private static float GetRelativeDifference(float expected, float actual)
        {
            return Mathf.Abs(actual - expected) / Mathf.Max(Mathf.Abs(expected), 0.001f);
        }

        private static void ValidateHierarchyPlacement(
            IEnumerable<GameObject> sceneObjects,
            ICollection<SceneValidationIssue> issues)
        {
            foreach (GameObject sceneObject in sceneObjects)
            {
                if (sceneObject.GetComponent<DoorInteractable>() != null
                    || sceneObject.GetComponent<ElevatorPlatform>() != null)
                {
                    RequireHierarchy(
                        sceneObject,
                        "_Blockout",
                        "Interact",
                        "门、电梯等可交互结构应归入 _Blockout/Interact。",
                        issues);
                }

                if (sceneObject.GetComponent<WorldCheckpoint>() != null)
                {
                    RequireHierarchy(
                        sceneObject,
                        "_Gameplay",
                        "Checkpoints",
                        "检查点应归入 _Gameplay/Checkpoints。",
                        issues);
                }

                WorldSpawnPoint spawnPoint = sceneObject.GetComponent<WorldSpawnPoint>();
                if (spawnPoint != null)
                {
                    bool isCheckpoint = spawnPoint.HasRole(WorldSpawnRole.PlayerStart)
                        || spawnPoint.HasRole(WorldSpawnRole.Checkpoint);
                    RequireHierarchy(
                        sceneObject,
                        "_Gameplay",
                        isCheckpoint ? "Checkpoints" : "SpawnPoints",
                        isCheckpoint
                            ? "玩家开场点与复活点应归入 _Gameplay/Checkpoints。"
                            : "生成点应归入 _Gameplay/SpawnPoints。",
                        issues);
                }

                if (sceneObject.GetComponent<EnemyCombatCoordinator>() != null)
                {
                    RequireHierarchy(
                        sceneObject,
                        "_Gameplay",
                        "CombatZones",
                        "敌人战斗协调器应归入 _Gameplay/CombatZones。",
                        issues);
                }

                if (IsSystemObject(sceneObject))
                {
                    RequireHierarchy(
                        sceneObject,
                        "_Systems",
                        null,
                        "管理器、镜头、导航和全局 UI 应归入 _Systems。",
                        issues);
                }
            }
        }

        private static bool IsSystemObject(GameObject sceneObject)
        {
            if (sceneObject.GetComponent<WorldRespawnManager>() != null
                || sceneObject.GetComponent<CombatFeedbackDispatcher>() != null
                || sceneObject.GetComponent<PlayerViewController>() != null
                || sceneObject.GetComponent<Camera>() != null)
            {
                return true;
            }

            return sceneObject.GetComponents<Component>()
                .Where(component => component != null)
                .Any(component => component.GetType().Name is "NavMeshSurface" or "EventSystem");
        }

        private static void RequireHierarchy(
            GameObject owner,
            string rootName,
            string childName,
            string message,
            ICollection<SceneValidationIssue> issues)
        {
            if (IsUnderHierarchy(owner.transform, rootName, childName))
            {
                return;
            }

            SceneValidator.Add(
                issues,
                SceneValidationSeverity.Warning,
                "SCENE_HIERARCHY_MISPLACED",
                "场景分类",
                message,
                owner);
        }

        private static bool IsUnderHierarchy(
            Transform transform,
            string rootName,
            string childName)
        {
            for (Transform current = transform; current != null; current = current.parent)
            {
                if (!string.Equals(current.name, rootName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(childName))
                {
                    return true;
                }

                for (Transform descendant = transform;
                     descendant != null && descendant != current;
                     descendant = descendant.parent)
                {
                    if (string.Equals(descendant.name, childName, StringComparison.Ordinal)
                        && descendant.parent == current)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void ValidateBlockoutLayersAndTags(
            IEnumerable<GameObject> sceneObjects,
            ICollection<SceneValidationIssue> issues)
        {
            int environmentLayer = LayerMask.NameToLayer("Environment");
            int interactableLayer = LayerMask.NameToLayer("Interactable");

            foreach (GameObject sceneObject in sceneObjects)
            {
                bool isBlockoutObject = IsUnderHierarchy(sceneObject.transform, "_Blockout", null);
                if (isBlockoutObject && !sceneObject.CompareTag("Untagged"))
                {
                    SceneValidator.Add(
                        issues,
                        SceneValidationSeverity.Warning,
                        "BLOCKOUT_TAG_INVALID",
                        "Tag",
                        $"普通白盒物体应保持 Untagged，当前 Tag 为 {sceneObject.tag}。",
                        sceneObject);
                }

                if (environmentLayer >= 0
                    && IsUnderHierarchy(sceneObject.transform, "_Blockout", "Env"))
                {
                    Collider[] invalidColliders = sceneObject.GetComponents<Collider>()
                        .Where(collider => collider != null
                            && !collider.isTrigger
                            && collider.gameObject.layer != environmentLayer)
                        .ToArray();
                    if (invalidColliders.Length > 0)
                    {
                        SceneValidator.Add(
                            issues,
                            SceneValidationSeverity.Warning,
                            "BLOCKOUT_ENV_LAYER_INVALID",
                            "Layer",
                            $"_Blockout/Env 的 {invalidColliders.Length} 个实体 Collider 应使用 "
                            + $"Environment Layer，当前物体为 {GetLayerName(sceneObject.layer)}。",
                            sceneObject);
                    }
                }

                ObjInteractable interactable = sceneObject.GetComponent<ObjInteractable>();
                if (interactable == null || interactableLayer < 0)
                {
                    continue;
                }

                Collider[] interactionColliders = sceneObject.GetComponents<Collider>();
                if (interactionColliders.Length > 0
                    && sceneObject.layer != interactableLayer)
                {
                    SceneValidator.Add(
                        issues,
                        SceneValidationSeverity.Warning,
                        "INTERACTABLE_LAYER_INVALID",
                        "Layer",
                        $"ObjInteractable 所在碰撞子物体应使用 Interactable Layer，"
                        + $"当前为 {GetLayerName(sceneObject.layer)}。",
                        interactable);
                }
            }
        }

        private static string GetLayerName(int layer)
        {
            string layerName = LayerMask.LayerToName(layer);
            return string.IsNullOrWhiteSpace(layerName) ? $"Layer {layer}" : layerName;
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }
    }
}
