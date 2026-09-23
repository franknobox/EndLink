using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EndLink.Editor
{
    /// <summary>
    /// Keeps field-level changes made through Scene Tuning alive across Play Mode reloads.
    /// SessionState is intentionally used so pending changes never become project data.
    /// </summary>
    [InitializeOnLoad]
    internal static class SceneTuningChangeTracker
    {
        private const string SessionKey = "EndLink.SceneTuning.PendingChanges";

        private static ChangeCollection _collection;

        static SceneTuningChangeTracker()
        {
            Load();
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        internal static event Action Changed;

        internal static IReadOnlyList<TrackedChange> Changes => Collection.changes;

        internal static int PendingCount => Collection.changes.Count;

        internal static void Record(
            Component component,
            string category,
            string propertyLabel,
            SerializedValue before,
            SerializedValue after)
        {
            if (!EditorApplication.isPlaying || component == null || before.Equals(after))
            {
                return;
            }

            string targetId = GlobalObjectId.GetGlobalObjectIdSlow(component).ToString();
            if (string.IsNullOrEmpty(targetId))
            {
                return;
            }

            List<TrackedChange> changes = Collection.changes;
            TrackedChange existing = changes.Find(change =>
                change.targetGlobalId == targetId && change.propertyPath == before.propertyPath);

            if (existing == null)
            {
                existing = new TrackedChange
                {
                    selected = true,
                    targetGlobalId = targetId,
                    scenePath = component.gameObject.scene.path,
                    objectName = component.gameObject.name,
                    componentName = component.GetType().Name,
                    category = category,
                    propertyPath = before.propertyPath,
                    propertyLabel = propertyLabel,
                    before = before,
                    after = after
                };
                changes.Add(existing);
            }
            else
            {
                existing.after = after;
                existing.category = category;
                existing.propertyLabel = propertyLabel;
            }

            if (existing.before.Equals(existing.after))
            {
                changes.Remove(existing);
            }

            SaveAndNotify();
        }

        internal static void SetSelected(TrackedChange change, bool selected)
        {
            if (change == null || change.selected == selected)
            {
                return;
            }

            change.selected = selected;
            SaveAndNotify();
        }

        internal static ApplyResult Apply(TrackedChange change)
        {
            if (change == null)
            {
                return ApplyResult.MissingTarget;
            }

            Component component = ResolveComponent(change);
            if (component == null)
            {
                return ApplyResult.MissingTarget;
            }

            SerializedObject serializedObject = new(component);
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(change.propertyPath);
            if (property == null)
            {
                return ApplyResult.MissingProperty;
            }

            Undo.RecordObject(component, $"Apply Scene Tuning: {change.propertyLabel}");
            if (!change.after.ApplyTo(property))
            {
                return ApplyResult.UnsupportedValue;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
            if (component.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            }

            Collection.changes.Remove(change);
            SaveAndNotify();
            return ApplyResult.Applied;
        }

        internal static int ApplySelected(out int failedCount)
        {
            int appliedCount = 0;
            failedCount = 0;
            TrackedChange[] selected = Collection.changes.FindAll(change => change.selected).ToArray();
            foreach (TrackedChange change in selected)
            {
                ApplyResult result = Apply(change);
                if (result == ApplyResult.Applied)
                {
                    appliedCount++;
                }
                else
                {
                    failedCount++;
                }
            }

            return appliedCount;
        }

        internal static void Discard(TrackedChange change)
        {
            if (change != null && Collection.changes.Remove(change))
            {
                SaveAndNotify();
            }
        }

        internal static void DiscardAll()
        {
            if (Collection.changes.Count == 0)
            {
                return;
            }

            Collection.changes.Clear();
            SaveAndNotify();
        }

        internal static Component ResolveComponent(TrackedChange change)
        {
            if (change == null || !GlobalObjectId.TryParse(change.targetGlobalId, out GlobalObjectId objectId))
            {
                return null;
            }

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(objectId) as Component;
        }

        private static ChangeCollection Collection => _collection ??= new ChangeCollection();

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    WarnAboutExistingChanges();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    EditorApplication.delayCall += ShowExitPrompt;
                    break;
            }
        }

        private static void WarnAboutExistingChanges()
        {
            if (PendingCount == 0)
            {
                return;
            }

            int choice = EditorUtility.DisplayDialogComplex(
                "存在待应用的联调参数",
                $"上一次 Play Mode 仍有 {PendingCount} 项参数差异未处理。",
                "查看差异",
                "全部放弃",
                "继续播放并保留");

            if (choice == 0)
            {
                EditorApplication.isPlaying = false;
                SceneTuningWindow.OpenPendingChanges();
            }
            else if (choice == 1)
            {
                DiscardAll();
            }
        }

        private static void ShowExitPrompt()
        {
            if (PendingCount == 0)
            {
                return;
            }

            int choice = EditorUtility.DisplayDialogComplex(
                "检测到运行时调参",
                $"Scene Tuning 记录了 {PendingCount} 项运行时参数差异。",
                "查看并应用",
                "全部放弃",
                "稍后处理");

            if (choice == 0)
            {
                SceneTuningWindow.OpenPendingChanges();
            }
            else if (choice == 1)
            {
                DiscardAll();
            }
        }

        private static void Load()
        {
            string json = SessionState.GetString(SessionKey, string.Empty);
            _collection = string.IsNullOrEmpty(json)
                ? new ChangeCollection()
                : JsonUtility.FromJson<ChangeCollection>(json) ?? new ChangeCollection();
            _collection.changes ??= new List<TrackedChange>();
        }

        private static void SaveAndNotify()
        {
            SessionState.SetString(SessionKey, JsonUtility.ToJson(Collection));
            Changed?.Invoke();
        }

        [Serializable]
        private sealed class ChangeCollection
        {
            public List<TrackedChange> changes = new();
        }

        internal enum ApplyResult
        {
            Applied,
            MissingTarget,
            MissingProperty,
            UnsupportedValue
        }
    }

    [Serializable]
    internal sealed class TrackedChange
    {
        public bool selected;
        public string targetGlobalId;
        public string scenePath;
        public string objectName;
        public string componentName;
        public string category;
        public string propertyPath;
        public string propertyLabel;
        public SerializedValue before;
        public SerializedValue after;
    }

    [Serializable]
    internal struct SerializedValue : IEquatable<SerializedValue>
    {
        public string propertyPath;
        public SerializedPropertyType propertyType;
        public long integerValue;
        public bool booleanValue;
        public double numberValue;
        public string stringValue;
        public string enumDisplayName;
        public string objectGlobalId;
        public Color colorValue;
        public Vector2 vector2Value;
        public Vector3 vector3Value;
        public Vector4 vector4Value;
        public Vector2Int vector2IntValue;
        public Vector3Int vector3IntValue;
        public Rect rectValue;
        public RectInt rectIntValue;
        public Bounds boundsValue;
        public BoundsInt boundsIntValue;
        public Quaternion quaternionValue;

        public static bool TryCapture(SerializedProperty property, out SerializedValue value)
        {
            value = new SerializedValue
            {
                propertyPath = property.propertyPath,
                propertyType = property.propertyType
            };

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character:
                    value.integerValue = property.longValue;
                    return true;
                case SerializedPropertyType.Enum:
                    value.integerValue = property.longValue;
                    value.enumDisplayName = property.enumValueIndex >= 0
                        && property.enumValueIndex < property.enumDisplayNames.Length
                            ? property.enumDisplayNames[property.enumValueIndex]
                            : property.longValue.ToString(CultureInfo.InvariantCulture);
                    return true;
                case SerializedPropertyType.Boolean:
                    value.booleanValue = property.boolValue;
                    return true;
                case SerializedPropertyType.Float:
                    value.numberValue = property.doubleValue;
                    return true;
                case SerializedPropertyType.String:
                    value.stringValue = property.stringValue;
                    return true;
                case SerializedPropertyType.Color:
                    value.colorValue = property.colorValue;
                    return true;
                case SerializedPropertyType.ObjectReference:
                    value.objectGlobalId = property.objectReferenceValue != null
                        ? GlobalObjectId.GetGlobalObjectIdSlow(property.objectReferenceValue).ToString()
                        : string.Empty;
                    return true;
                case SerializedPropertyType.Vector2:
                    value.vector2Value = property.vector2Value;
                    return true;
                case SerializedPropertyType.Vector3:
                    value.vector3Value = property.vector3Value;
                    return true;
                case SerializedPropertyType.Vector4:
                    value.vector4Value = property.vector4Value;
                    return true;
                case SerializedPropertyType.Vector2Int:
                    value.vector2IntValue = property.vector2IntValue;
                    return true;
                case SerializedPropertyType.Vector3Int:
                    value.vector3IntValue = property.vector3IntValue;
                    return true;
                case SerializedPropertyType.Rect:
                    value.rectValue = property.rectValue;
                    return true;
                case SerializedPropertyType.RectInt:
                    value.rectIntValue = property.rectIntValue;
                    return true;
                case SerializedPropertyType.Bounds:
                    value.boundsValue = property.boundsValue;
                    return true;
                case SerializedPropertyType.BoundsInt:
                    value.boundsIntValue = property.boundsIntValue;
                    return true;
                case SerializedPropertyType.Quaternion:
                    value.quaternionValue = property.quaternionValue;
                    return true;
                default:
                    return false;
            }
        }

        public bool ApplyTo(SerializedProperty property)
        {
            if (property == null || property.propertyType != propertyType)
            {
                return false;
            }

            switch (propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character:
                    property.longValue = integerValue;
                    return true;
                case SerializedPropertyType.Boolean:
                    property.boolValue = booleanValue;
                    return true;
                case SerializedPropertyType.Float:
                    property.doubleValue = numberValue;
                    return true;
                case SerializedPropertyType.String:
                    property.stringValue = stringValue;
                    return true;
                case SerializedPropertyType.Color:
                    property.colorValue = colorValue;
                    return true;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = ResolveObjectReference();
                    return string.IsNullOrEmpty(objectGlobalId) || property.objectReferenceValue != null;
                case SerializedPropertyType.Vector2:
                    property.vector2Value = vector2Value;
                    return true;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = vector3Value;
                    return true;
                case SerializedPropertyType.Vector4:
                    property.vector4Value = vector4Value;
                    return true;
                case SerializedPropertyType.Vector2Int:
                    property.vector2IntValue = vector2IntValue;
                    return true;
                case SerializedPropertyType.Vector3Int:
                    property.vector3IntValue = vector3IntValue;
                    return true;
                case SerializedPropertyType.Rect:
                    property.rectValue = rectValue;
                    return true;
                case SerializedPropertyType.RectInt:
                    property.rectIntValue = rectIntValue;
                    return true;
                case SerializedPropertyType.Bounds:
                    property.boundsValue = boundsValue;
                    return true;
                case SerializedPropertyType.BoundsInt:
                    property.boundsIntValue = boundsIntValue;
                    return true;
                case SerializedPropertyType.Quaternion:
                    property.quaternionValue = quaternionValue;
                    return true;
                default:
                    return false;
            }
        }

        public string ToDisplayString()
        {
            switch (propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character:
                    return integerValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Enum:
                    return string.IsNullOrEmpty(enumDisplayName)
                        ? integerValue.ToString(CultureInfo.InvariantCulture)
                        : enumDisplayName;
                case SerializedPropertyType.Boolean:
                    return booleanValue ? "开启" : "关闭";
                case SerializedPropertyType.Float:
                    return numberValue.ToString("0.###", CultureInfo.InvariantCulture);
                case SerializedPropertyType.String:
                    return stringValue ?? string.Empty;
                case SerializedPropertyType.Color:
                    return colorValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    UnityEngine.Object target = ResolveObjectReference();
                    return target != null ? target.name : string.IsNullOrEmpty(objectGlobalId) ? "无" : "引用已失效";
                case SerializedPropertyType.Vector2:
                    return vector2Value.ToString("0.###");
                case SerializedPropertyType.Vector3:
                    return vector3Value.ToString("0.###");
                case SerializedPropertyType.Vector4:
                    return vector4Value.ToString("0.###");
                case SerializedPropertyType.Vector2Int:
                    return vector2IntValue.ToString();
                case SerializedPropertyType.Vector3Int:
                    return vector3IntValue.ToString();
                case SerializedPropertyType.Rect:
                    return rectValue.ToString("0.###");
                case SerializedPropertyType.RectInt:
                    return rectIntValue.ToString();
                case SerializedPropertyType.Bounds:
                    return boundsValue.ToString("0.###");
                case SerializedPropertyType.BoundsInt:
                    return boundsIntValue.ToString();
                case SerializedPropertyType.Quaternion:
                    return quaternionValue.eulerAngles.ToString("0.###");
                default:
                    return "不支持的值";
            }
        }

        public bool Equals(SerializedValue other)
        {
            if (propertyType != other.propertyType || propertyPath != other.propertyPath)
            {
                return false;
            }

            switch (propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character:
                    return integerValue == other.integerValue;
                case SerializedPropertyType.Boolean:
                    return booleanValue == other.booleanValue;
                case SerializedPropertyType.Float:
                    return Math.Abs(numberValue - other.numberValue) <= 0.000001d;
                case SerializedPropertyType.String:
                    return stringValue == other.stringValue;
                case SerializedPropertyType.Color:
                    return colorValue == other.colorValue;
                case SerializedPropertyType.ObjectReference:
                    return objectGlobalId == other.objectGlobalId;
                case SerializedPropertyType.Vector2:
                    return vector2Value == other.vector2Value;
                case SerializedPropertyType.Vector3:
                    return vector3Value == other.vector3Value;
                case SerializedPropertyType.Vector4:
                    return vector4Value == other.vector4Value;
                case SerializedPropertyType.Vector2Int:
                    return vector2IntValue == other.vector2IntValue;
                case SerializedPropertyType.Vector3Int:
                    return vector3IntValue == other.vector3IntValue;
                case SerializedPropertyType.Rect:
                    return rectValue == other.rectValue;
                case SerializedPropertyType.RectInt:
                    return rectIntValue == other.rectIntValue;
                case SerializedPropertyType.Bounds:
                    return boundsValue == other.boundsValue;
                case SerializedPropertyType.BoundsInt:
                    return boundsIntValue == other.boundsIntValue;
                case SerializedPropertyType.Quaternion:
                    return quaternionValue == other.quaternionValue;
                default:
                    return false;
            }
        }

        private UnityEngine.Object ResolveObjectReference()
        {
            if (string.IsNullOrEmpty(objectGlobalId)
                || !GlobalObjectId.TryParse(objectGlobalId, out GlobalObjectId globalId))
            {
                return null;
            }

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
        }
    }
}
