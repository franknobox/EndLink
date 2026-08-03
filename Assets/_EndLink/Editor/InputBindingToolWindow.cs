using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;

namespace EndLink.Editor
{
    /// <summary>
    /// 直接读取并编辑项目默认 Input Actions 资产中的默认绑定。
    /// 运行时玩家自定义键位属于另一套持久化流程，不由本窗口负责。
    /// </summary>
    internal sealed class InputBindingToolWindow : EditorWindow
    {
        private const string InputActionsPath = "Assets/_EndLink/Control/InputSystem_Actions.inputactions";
        private const float ActionLabelWidth = 250f;
        private const float BindingSlotWidth = 155f;
        private const float PathFieldWidth = 360f;

        private static readonly string[] DevicePageNames = { "键盘与鼠标", "手柄" };

        private readonly Dictionary<Guid, bool> _mapFoldouts = new();
        private readonly Dictionary<BindingKey, List<string>> _conflicts = new();
        private readonly List<ConflictPair> _conflictPairs = new();

        private InputActionAsset _sourceAsset;
        private InputActionAsset _workingAsset;
        private string _baselineJson = string.Empty;
        private string _loadError = string.Empty;
        private string _searchText = string.Empty;
        private Vector2 _scrollPosition;
        private DevicePage _selectedPage;
        private bool _showAdvancedPaths;
        private bool _dirty;

        private IDisposable _listenSubscription;
        private Guid _listeningActionId;
        private Guid _listeningBindingId;
        private DeviceKind _listeningDeviceKind;
        private string _listeningLabel = string.Empty;

        [MenuItem("EndLink/Input/Binding Tool")]
        private static void OpenWindow()
        {
            InputBindingToolWindow window = GetWindow<InputBindingToolWindow>();
            window.titleContent = new GUIContent("Input Binding Tool");
            window.minSize = new Vector2(980f, 540f);
            window.Show();
        }

        private void OnEnable()
        {
            saveChangesMessage = "Input Binding Tool 中存在尚未应用到 Input Actions 的修改。是否保存？";
            Undo.undoRedoPerformed += HandleUndoRedo;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            LoadFromDisk();
        }

        private void OnDisable()
        {
            CancelInputListening();
            Undo.undoRedoPerformed -= HandleUndoRedo;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            DestroyWorkingAsset();
        }

        private void OnInspectorUpdate()
        {
            if (_listenSubscription != null)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawToolbar();

            if (!string.IsNullOrEmpty(_loadError))
            {
                EditorGUILayout.HelpBox(_loadError, MessageType.Error);
                if (GUILayout.Button("重新加载", GUILayout.Width(100f)))
                {
                    LoadFromDisk();
                }

                return;
            }

            if (_workingAsset == null)
            {
                EditorGUILayout.HelpBox("未加载 Input Actions 资产。", MessageType.Warning);
                return;
            }

            DrawDeviceTabs();
            DrawSummary();
            DrawListeningStatus();
            DrawPageControls();
            DrawBindingPage();
        }

        public override void SaveChanges()
        {
            if (ApplyChanges())
            {
                base.SaveChanges();
            }
        }

        public override void DiscardChanges()
        {
            CancelInputListening();
            _dirty = false;
            hasUnsavedChanges = false;
            base.DiscardChanges();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("EndLink 输入绑定工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "用于查看和调整项目默认键位。修改先保存在窗口内存副本中，点击“应用到 Input Actions”后才会写回源资产；不会直接修改生成的 InputSystem_Actions.cs。",
                MessageType.Info);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Play Mode 中只允许查看，不能修改或应用默认键位。", MessageType.Warning);
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("重新加载", EditorStyles.toolbarButton, GUILayout.Width(75f)))
                {
                    ReloadWithConfirmation();
                }

                using (new EditorGUI.DisabledScope(_sourceAsset == null))
                {
                    if (GUILayout.Button("定位资产", EditorStyles.toolbarButton, GUILayout.Width(75f)))
                    {
                        Selection.activeObject = _sourceAsset;
                        EditorGUIUtility.PingObject(_sourceAsset);
                    }

                    if (GUILayout.Button("打开原编辑器", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                    {
                        AssetDatabase.OpenAsset(_sourceAsset);
                    }
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!_dirty || EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button("放弃修改", EditorStyles.toolbarButton, GUILayout.Width(75f)))
                    {
                        ReloadWithConfirmation();
                    }

                    if (GUILayout.Button("应用到 Input Actions", EditorStyles.toolbarButton, GUILayout.Width(135f)))
                    {
                        ApplyChanges();
                    }
                }
            }
        }

        private void DrawDeviceTabs()
        {
            EditorGUILayout.Space(8f);
            int selectedPage = GUILayout.Toolbar(
                (int)_selectedPage,
                DevicePageNames,
                GUILayout.Height(32f));
            if (selectedPage == (int)_selectedPage)
            {
                return;
            }

            CancelInputListening();
            _selectedPage = (DevicePage)selectedPage;
            _scrollPosition = Vector2.zero;
        }

        private void DrawSummary()
        {
            int actionCount = 0;
            int bindingCount = 0;
            GetPageConflictStats(_selectedPage, out int conflictPairCount, out int affectedBindingCount);
            foreach (InputActionMap map in _workingAsset.actionMaps)
            {
                foreach (InputAction action in map.actions)
                {
                    bool hasPageBinding = false;
                    foreach (InputBinding binding in action.bindings)
                    {
                        if (binding.isComposite || !BindingBelongsToPage(binding, _selectedPage))
                        {
                            continue;
                        }

                        hasPageBinding = true;
                        bindingCount++;
                    }

                    if (hasPageBinding)
                    {
                        actionCount++;
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(DevicePageNames[(int)_selectedPage], EditorStyles.boldLabel, GUILayout.Width(110f));
                GUILayout.Label($"{actionCount} 项操作", GUILayout.Width(90f));
                GUILayout.Label($"{bindingCount} 个绑定", GUILayout.Width(90f));

                GUIStyle conflictStyle = conflictPairCount > 0 ? EditorStyles.boldLabel : EditorStyles.label;
                GUILayout.Label(
                    $"冲突 {conflictPairCount} 组 / {affectedBindingCount} 个槽位",
                    conflictStyle,
                    GUILayout.Width(175f));
                GUILayout.FlexibleSpace();
                GUILayout.Label(_dirty ? "有未应用修改" : "已与磁盘同步", _dirty ? EditorStyles.boldLabel : EditorStyles.miniLabel);
            }

            if (conflictPairCount > 0)
            {
                EditorGUILayout.HelpBox(
                    "当前设备页存在默认绑定冲突。组数表示冲突关系数量，槽位数表示带黄色图标的受影响绑定数量。",
                    MessageType.Warning);
            }
        }

        private void DrawListeningStatus()
        {
            if (_listenSubscription == null)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"正在监听：{_listeningLabel}。请按下新的输入……", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("取消监听", GUILayout.Width(85f)))
                {
                    CancelInputListening();
                }
            }
        }

        private void DrawPageControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("搜索操作", GUILayout.Width(60f));
                _searchText = EditorGUILayout.TextField(_searchText, GUI.skin.FindStyle("ToolbarSearchTextField"));
                _showAdvancedPaths = GUILayout.Toggle(
                    _showAdvancedPaths,
                    new GUIContent("高级路径", "显示并允许直接编辑 Input System Path。"),
                    EditorStyles.miniButton,
                    GUILayout.Width(80f));
            }
        }

        private void DrawBindingPage()
        {
            EditorGUILayout.Space(4f);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            try
            {
                foreach (InputActionMap map in _workingAsset.actionMaps)
                {
                    DrawActionMap(map, _selectedPage);
                }
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawActionMap(InputActionMap map, DevicePage page)
        {
            if (!MapHasVisibleBindings(map, page))
            {
                return;
            }

            bool defaultExpanded = !string.Equals(map.name, "UI", StringComparison.OrdinalIgnoreCase);
            bool expanded = _mapFoldouts.TryGetValue(map.id, out bool savedExpanded) ? savedExpanded : defaultExpanded;
            expanded = EditorGUILayout.Foldout(
                expanded,
                GetMapDisplayName(map.name),
                true,
                EditorStyles.foldoutHeader);
            _mapFoldouts[map.id] = expanded;
            if (!expanded)
            {
                return;
            }

            foreach (InputAction action in map.actions)
            {
                DrawAction(action, page);
            }

            EditorGUILayout.Space(8f);
        }

        private void DrawAction(InputAction action, DevicePage page)
        {
            List<BindingRowGroup> groups = new();
            for (int bindingIndex = 0; bindingIndex < action.bindings.Count; bindingIndex++)
            {
                InputBinding binding = action.bindings[bindingIndex];
                if (binding.isComposite || !BindingBelongsToPage(binding, page))
                {
                    continue;
                }

                string groupName = binding.isPartOfComposite ? binding.name ?? string.Empty : string.Empty;
                BindingRowGroup group = groups.Find(candidate =>
                    string.Equals(candidate.Name, groupName, StringComparison.OrdinalIgnoreCase));
                if (group == null)
                {
                    group = new BindingRowGroup(groupName);
                    groups.Add(group);
                }

                group.BindingIndices.Add(bindingIndex);
            }

            string actionDisplayName = GetActionDisplayName(action.name);
            foreach (BindingRowGroup group in groups)
            {
                string rowLabel = string.IsNullOrEmpty(group.Name)
                    ? actionDisplayName
                    : $"{actionDisplayName} - {GetCompositePartDisplayName(group.Name)}";
                if (!RowMatchesSearch(action, rowLabel, group.BindingIndices))
                {
                    continue;
                }

                DrawBindingRow(action, rowLabel, group.BindingIndices, page);
            }
        }

        private void DrawBindingRow(
            InputAction action,
            string rowLabel,
            List<int> bindingIndices,
            DevicePage page)
        {
            Rect rowRect = EditorGUILayout.GetControlRect(false, 34f);
            Color rowColor = EditorGUIUtility.isProSkin
                ? new Color(0.19f, 0.20f, 0.22f, 1f)
                : new Color(0.90f, 0.91f, 0.92f, 1f);
            EditorGUI.DrawRect(rowRect, rowColor);

            Rect labelRect = new(rowRect.x + 12f, rowRect.y + 7f, ActionLabelWidth - 12f, 20f);
            GUI.Label(labelRect, new GUIContent(rowLabel, action.name), EditorStyles.label);

            float currentX = rowRect.x + ActionLabelWidth;
            foreach (int bindingIndex in bindingIndices)
            {
                InputBinding binding = action.bindings[bindingIndex];
                bool isSharedBinding = IsSharedDeviceBinding(binding);
                bool canEdit = !EditorApplication.isPlayingOrWillChangePlaymode && !isSharedBinding;
                bool isListening = _listenSubscription != null &&
                                   _listeningActionId == action.id &&
                                   _listeningBindingId == binding.id;
                BindingKey key = new(action.id, binding.id);
                _conflicts.TryGetValue(key, out List<string> messages);
                bool hasConflict = messages != null && BindingHasConflictOnPage(key, page);

                Rect slotRect = new(currentX, rowRect.y + 4f, BindingSlotWidth, 26f);
                Color previousBackground = GUI.backgroundColor;
                if (hasConflict)
                {
                    GUI.backgroundColor = new Color(1f, 0.72f, 0.25f);
                }

                using (new EditorGUI.DisabledScope(!canEdit))
                {
                    string slotText = isListening ? "等待输入…" : GetGameStyleBindingName(binding.path, page);
                    string slotTooltip = isSharedBinding
                        ? "该槽位由键鼠和手柄共用，保持 Input System 的系统默认绑定。"
                        : "点击后监听新的输入。";
                    if (GUI.Button(slotRect, new GUIContent(slotText, slotTooltip)))
                    {
                        if (isListening)
                        {
                            CancelInputListening();
                        }
                        else
                        {
                            BeginInputListening(action, binding, GetDeviceKind(page));
                        }
                    }
                }

                GUI.backgroundColor = previousBackground;
                currentX += BindingSlotWidth + 4f;

                Rect clearRect = new(currentX, rowRect.y + 5f, 24f, 24f);
                using (new EditorGUI.DisabledScope(!canEdit || string.IsNullOrEmpty(binding.path)))
                {
                    if (GUI.Button(clearRect, new GUIContent("×", "清除该绑定槽位。"), EditorStyles.miniButton))
                    {
                        SetBindingPath(action.id, binding.id, string.Empty);
                    }
                }

                currentX += 27f;
                if (hasConflict)
                {
                    GUIContent warning = EditorGUIUtility.IconContent("console.warnicon.sml");
                    warning.tooltip = string.Join("\n", messages);
                    GUI.Label(new Rect(currentX, rowRect.y + 7f, 20f, 20f), warning);
                }

                currentX += 24f;
            }

            if (!_showAdvancedPaths)
            {
                return;
            }

            foreach (int bindingIndex in bindingIndices)
            {
                InputBinding binding = action.bindings[bindingIndex];
                bool isSharedBinding = IsSharedDeviceBinding(binding);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(ActionLabelWidth);
                    GUILayout.Label(
                        string.IsNullOrEmpty(binding.name) ? "路径" : GetCompositePartDisplayName(binding.name),
                        EditorStyles.miniLabel,
                        GUILayout.Width(65f));
                    using (new EditorGUI.DisabledScope(
                               EditorApplication.isPlayingOrWillChangePlaymode || isSharedBinding))
                    {
                        EditorGUI.BeginChangeCheck();
                        string newPath = EditorGUILayout.DelayedTextField(
                            binding.path ?? string.Empty,
                            GUILayout.Width(PathFieldWidth));
                        if (EditorGUI.EndChangeCheck())
                        {
                            SetBindingPath(action.id, binding.id, newPath);
                        }
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void BeginInputListening(InputAction action, InputBinding binding, DeviceKind deviceKind)
        {
            CancelInputListening();

            _listeningActionId = action.id;
            _listeningBindingId = binding.id;
            _listeningDeviceKind = deviceKind;
            _listeningLabel = $"{action.actionMap?.name}/{action.name}/{GetBindingLabel(binding)}";

            // 延迟一帧订阅，避免用于点击“监听”的鼠标按键被立即记录成新绑定。
            EditorApplication.delayCall += StartListeningAfterGuiEvent;
        }

        private void StartListeningAfterGuiEvent()
        {
            if (this == null || _workingAsset == null || _listeningBindingId == Guid.Empty)
            {
                return;
            }

            _listenSubscription = InputSystem.onAnyButtonPress.Call(HandleAnyButtonPress);
            Repaint();
        }

        private void HandleAnyButtonPress(InputControl control)
        {
            if (control == null || !DeviceMatchesListeningSlot(control.device, _listeningDeviceKind))
            {
                return;
            }

            Guid actionId = _listeningActionId;
            Guid bindingId = _listeningBindingId;
            string path = BuildGenericControlPath(control);
            CancelInputListening();

            EditorApplication.delayCall += () =>
            {
                if (this == null || string.IsNullOrEmpty(path))
                {
                    return;
                }

                SetBindingPath(actionId, bindingId, path);
                Repaint();
            };
        }

        private void CancelInputListening()
        {
            _listenSubscription?.Dispose();
            _listenSubscription = null;
            _listeningActionId = Guid.Empty;
            _listeningBindingId = Guid.Empty;
            _listeningDeviceKind = DeviceKind.Other;
            _listeningLabel = string.Empty;
            Repaint();
        }

        private void SetBindingPath(Guid actionId, Guid bindingId, string newPath)
        {
            if (_workingAsset == null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!TryFindBinding(actionId, bindingId, out InputAction action, out int bindingIndex))
            {
                Debug.LogWarning("Input Binding Tool: 未找到要修改的绑定，窗口将重新加载源资产。");
                LoadFromDisk();
                return;
            }

            string normalizedPath = newPath?.Trim() ?? string.Empty;
            if (string.Equals(action.bindings[bindingIndex].path, normalizedPath, StringComparison.Ordinal))
            {
                return;
            }

            Undo.RecordObject(_workingAsset, "Change Input Binding");
            action.ChangeBinding(bindingIndex).WithPath(normalizedPath);
            EditorUtility.SetDirty(_workingAsset);
            UpdateDirtyAndConflicts();
        }

        private bool TryFindBinding(Guid actionId, Guid bindingId, out InputAction foundAction, out int bindingIndex)
        {
            foreach (InputActionMap map in _workingAsset.actionMaps)
            {
                foreach (InputAction action in map.actions)
                {
                    if (action.id != actionId)
                    {
                        continue;
                    }

                    for (int index = 0; index < action.bindings.Count; index++)
                    {
                        if (action.bindings[index].id == bindingId)
                        {
                            foundAction = action;
                            bindingIndex = index;
                            return true;
                        }
                    }
                }
            }

            foundAction = null;
            bindingIndex = -1;
            return false;
        }

        private void LoadFromDisk()
        {
            CancelInputListening();
            DestroyWorkingAsset();
            _loadError = string.Empty;

            try
            {
                _sourceAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
                if (_sourceAsset == null)
                {
                    throw new FileNotFoundException("找不到 Input Actions 资产。", InputActionsPath);
                }

                string fullPath = Path.GetFullPath(InputActionsPath);
                string json = File.ReadAllText(fullPath, Encoding.UTF8);
                _workingAsset = InputActionAsset.FromJson(json);
                _workingAsset.hideFlags = HideFlags.HideAndDontSave;
                _baselineJson = _workingAsset.ToJson();
                _dirty = false;
                hasUnsavedChanges = false;
                RebuildConflicts();
            }
            catch (Exception exception)
            {
                _loadError = $"加载失败：{exception.Message}";
                _sourceAsset = null;
                _workingAsset = null;
                _baselineJson = string.Empty;
                _dirty = false;
                hasUnsavedChanges = false;
            }

            Repaint();
        }

        private bool ApplyChanges()
        {
            if (_workingAsset == null || !_dirty)
            {
                return true;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("无法应用", "请先退出 Play Mode，再应用默认键位修改。", "确定");
                return false;
            }

            try
            {
                CancelInputListening();
                string json = _workingAsset.ToJson();
                string fullPath = Path.GetFullPath(InputActionsPath);
                File.WriteAllText(fullPath, json, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(
                    InputActionsPath,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                LoadFromDisk();
                Debug.Log($"Input Binding Tool: 已更新 {InputActionsPath}");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("应用失败", exception.Message, "确定");
                return false;
            }
        }

        private void ReloadWithConfirmation()
        {
            if (_dirty && !EditorUtility.DisplayDialog(
                    "放弃未应用修改？",
                    "重新加载会丢弃当前窗口中尚未应用的键位修改。",
                    "重新加载",
                    "取消"))
            {
                return;
            }

            LoadFromDisk();
        }

        private void HandleUndoRedo()
        {
            if (_workingAsset == null)
            {
                return;
            }

            UpdateDirtyAndConflicts();
        }

        private void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            CancelInputListening();
            Repaint();
        }

        private void UpdateDirtyAndConflicts()
        {
            _dirty = _workingAsset != null && !string.Equals(_workingAsset.ToJson(), _baselineJson, StringComparison.Ordinal);
            hasUnsavedChanges = _dirty;
            RebuildConflicts();
            Repaint();
        }

        private void RebuildConflicts()
        {
            _conflicts.Clear();
            _conflictPairs.Clear();
            if (_workingAsset == null)
            {
                return;
            }

            List<BindingEntry> entries = new();
            foreach (InputActionMap map in _workingAsset.actionMaps)
            {
                foreach (InputAction action in map.actions)
                {
                    foreach (InputBinding binding in action.bindings)
                    {
                        if (binding.isComposite || string.IsNullOrWhiteSpace(binding.path))
                        {
                            continue;
                        }

                        entries.Add(new BindingEntry(map.name, action.name, action.id, binding));
                    }
                }
            }

            for (int firstIndex = 0; firstIndex < entries.Count; firstIndex++)
            {
                BindingEntry first = entries[firstIndex];
                for (int secondIndex = firstIndex + 1; secondIndex < entries.Count; secondIndex++)
                {
                    BindingEntry second = entries[secondIndex];
                    if (!string.Equals(first.MapName, second.MapName, StringComparison.OrdinalIgnoreCase) ||
                        !BindingGroupsOverlap(first.Groups, second.Groups) ||
                        !PathsConflict(first.Path, second.Path))
                    {
                        continue;
                    }

                    if (IsAllowedContextualBindingPair(first, second))
                    {
                        continue;
                    }

                    _conflictPairs.Add(new ConflictPair(
                        first.Key,
                        second.Key,
                        first.KeyboardMouse && second.KeyboardMouse,
                        first.Gamepad && second.Gamepad));
                    AddConflict(first.Key, $"与 {second.DisplayName} 冲突：{second.Path}");
                    AddConflict(second.Key, $"与 {first.DisplayName} 冲突：{first.Path}");
                }
            }
        }

        private void GetPageConflictStats(
            DevicePage page,
            out int pairCount,
            out int affectedBindingCount)
        {
            pairCount = 0;
            HashSet<BindingKey> affectedBindings = new();
            foreach (ConflictPair pair in _conflictPairs)
            {
                if (!pair.AppliesTo(page))
                {
                    continue;
                }

                pairCount++;
                affectedBindings.Add(pair.First);
                affectedBindings.Add(pair.Second);
            }

            affectedBindingCount = affectedBindings.Count;
        }

        private bool BindingHasConflictOnPage(BindingKey key, DevicePage page)
        {
            foreach (ConflictPair pair in _conflictPairs)
            {
                if (pair.AppliesTo(page) && (pair.First.Equals(key) || pair.Second.Equals(key)))
                {
                    return true;
                }
            }

            return false;
        }

        private void AddConflict(BindingKey key, string message)
        {
            if (!_conflicts.TryGetValue(key, out List<string> messages))
            {
                messages = new List<string>();
                _conflicts.Add(key, messages);
            }

            messages.Add(message);
        }

        private bool MapHasVisibleBindings(InputActionMap map, DevicePage page)
        {
            bool mapMatches = MatchesSearch(map.name) || MatchesSearch(GetMapDisplayName(map.name));
            foreach (InputAction action in map.actions)
            {
                foreach (InputBinding binding in action.bindings)
                {
                    if (binding.isComposite || !BindingBelongsToPage(binding, page))
                    {
                        continue;
                    }

                    if (mapMatches ||
                        MatchesSearch(action.name) ||
                        MatchesSearch(GetActionDisplayName(action.name)) ||
                        MatchesSearch(binding.name) ||
                        MatchesSearch(binding.path) ||
                        MatchesSearch(GetReadablePath(binding.path)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool RowMatchesSearch(InputAction action, string rowLabel, List<int> bindingIndices)
        {
            if (MatchesSearch(action.actionMap?.name) ||
                MatchesSearch(GetMapDisplayName(action.actionMap?.name)) ||
                MatchesSearch(action.name) ||
                MatchesSearch(rowLabel))
            {
                return true;
            }

            foreach (int bindingIndex in bindingIndices)
            {
                InputBinding binding = action.bindings[bindingIndex];
                if (MatchesSearch(binding.path) || MatchesSearch(GetReadablePath(binding.path)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool MatchesSearch(string value)
        {
            return string.IsNullOrWhiteSpace(_searchText) ||
                   (!string.IsNullOrEmpty(value) && value.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool BindingBelongsToPage(InputBinding binding, DevicePage page)
        {
            string groups = binding.groups ?? string.Empty;
            string path = binding.path ?? string.Empty;
            bool hasKeyboardMouseGroup = groups.IndexOf("Keyboard&Mouse", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasGamepadGroup = groups.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0;

            if (page == DevicePage.KeyboardMouse)
            {
                return hasKeyboardMouseGroup ||
                       path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase) ||
                       path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase) ||
                       path.StartsWith("<Pointer>", StringComparison.OrdinalIgnoreCase) ||
                       path.StartsWith("<Pen>", StringComparison.OrdinalIgnoreCase);
            }

            return hasGamepadGroup || path.StartsWith("<Gamepad>", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSharedDeviceBinding(InputBinding binding)
        {
            string groups = binding.groups ?? string.Empty;
            return groups.IndexOf("Keyboard&Mouse", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   groups.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static DeviceKind GetDeviceKind(DevicePage page)
        {
            return page == DevicePage.KeyboardMouse ? DeviceKind.KeyboardMouse : DeviceKind.Gamepad;
        }

        private static string GetMapDisplayName(string mapName)
        {
            return mapName switch
            {
                "Player" => "游戏操作",
                "UI" => "界面操作",
                _ => mapName ?? "未命名操作组"
            };
        }

        private static string GetActionDisplayName(string actionName)
        {
            return actionName switch
            {
                "Move" => "移动",
                "Look" => "视角移动",
                "Attack" => "攻击",
                "Jump" => "跳跃",
                "Previous" => "切换目标（左）",
                "Next" => "切换目标（右）",
                "Sprint" => "冲刺",
                "Dodge" => "闪避",
                "Guard" => "格挡",
                "Aim" => "瞄准",
                "PreviousWeaponForm" => "上一武器形态",
                "NextWeaponForm" => "下一武器形态",
                "TargetLock" => "锁定目标",
                "PlayerSkill" => "主动技能",
                "AllySlotASkill" => "队友 A 技能",
                "AllySlotBSkill" => "队友 B 技能",
                "PartyUltimate" => "终链奥义",
                "Navigate" => "界面导航",
                "Submit" => "确认",
                "Cancel" => "返回",
                "Point" => "指针移动",
                "Click" => "点击",
                "RightClick" => "右键点击",
                "MiddleClick" => "中键点击",
                "ScrollWheel" => "界面滚动",
                "TrackedDevicePosition" => "追踪设备位置",
                "TrackedDeviceOrientation" => "追踪设备朝向",
                _ => actionName
            };
        }

        private static string GetCompositePartDisplayName(string partName)
        {
            return partName?.ToLowerInvariant() switch
            {
                "up" => "上",
                "down" => "下",
                "left" => "左",
                "right" => "右",
                _ => string.IsNullOrEmpty(partName) ? "默认" : partName
            };
        }

        private static string GetGameStyleBindingName(string path, DevicePage page)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "未绑定";
            }

            string normalizedPath = path.ToLowerInvariant();
            string commonName = normalizedPath switch
            {
                "<mouse>/leftbutton" => "鼠标左键",
                "<mouse>/rightbutton" => "鼠标右键",
                "<mouse>/middlebutton" => "鼠标中键",
                "<mouse>/scroll/up" => "滚轮向上",
                "<mouse>/scroll/down" => "滚轮向下",
                "<mouse>/scroll" => "鼠标滚轮",
                "<mouse>/position" => "鼠标指针",
                "<pointer>/delta" => "鼠标移动",
                "<gamepad>/buttonsouth" => "A / ×",
                "<gamepad>/buttoneast" => "B / ○",
                "<gamepad>/buttonwest" => "X / □",
                "<gamepad>/buttonnorth" => "Y / △",
                "<gamepad>/leftshoulder" => "LB / L1",
                "<gamepad>/rightshoulder" => "RB / R1",
                "<gamepad>/lefttrigger" => "LT / L2",
                "<gamepad>/righttrigger" => "RT / R2",
                "<gamepad>/leftstickpress" => "按下左摇杆",
                "<gamepad>/rightstickpress" => "按下右摇杆",
                "<gamepad>/leftstick" => "左摇杆",
                "<gamepad>/rightstick" => "右摇杆",
                "<gamepad>/rightstick/left" => "右摇杆向左",
                "<gamepad>/rightstick/right" => "右摇杆向右",
                "<gamepad>/dpad" => "方向键",
                "<gamepad>/dpad/up" => "方向键上",
                "<gamepad>/dpad/down" => "方向键下",
                "<gamepad>/dpad/left" => "方向键左",
                "<gamepad>/dpad/right" => "方向键右",
                "*/{submit}" => page == DevicePage.Gamepad ? "默认确认键" : "Enter / Space",
                "*/{cancel}" => page == DevicePage.Gamepad ? "默认返回键" : "Esc",
                _ => null
            };

            return commonName ?? GetReadablePath(path);
        }

        private static string GetBindingLabel(InputBinding binding)
        {
            if (binding.isComposite)
            {
                return $"[{binding.path}]";
            }

            if (binding.isPartOfComposite)
            {
                return $"  └ {binding.name}";
            }

            return string.IsNullOrEmpty(binding.name) ? "默认" : binding.name;
        }

        private static string GetReadablePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "未绑定";
            }

            try
            {
                return InputControlPath.ToHumanReadableString(
                    path,
                    InputControlPath.HumanReadableStringOptions.OmitDevice);
            }
            catch
            {
                return path;
            }
        }

        private static bool DeviceMatchesListeningSlot(InputDevice device, DeviceKind expectedKind)
        {
            return expectedKind switch
            {
                DeviceKind.KeyboardMouse => device is Keyboard || device is Mouse,
                DeviceKind.Gamepad => device is Gamepad,
                _ => true
            };
        }

        private static string BuildGenericControlPath(InputControl control)
        {
            if (control?.device == null)
            {
                return string.Empty;
            }

            string layout = control.device switch
            {
                Keyboard => "Keyboard",
                Mouse => "Mouse",
                Gamepad => "Gamepad",
                _ => control.device.layout
            };

            string relativePath = control.path;
            string devicePath = control.device.path;
            if (!string.IsNullOrEmpty(devicePath) && relativePath.StartsWith(devicePath, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = relativePath.Substring(devicePath.Length);
            }

            relativePath = relativePath.TrimStart('/');
            return string.IsNullOrEmpty(layout) || string.IsNullOrEmpty(relativePath)
                ? string.Empty
                : $"<{layout}>/{relativePath}";
        }

        private static bool BindingGroupsOverlap(string firstGroups, string secondGroups)
        {
            if (string.IsNullOrWhiteSpace(firstGroups) || string.IsNullOrWhiteSpace(secondGroups))
            {
                return true;
            }

            string[] first = firstGroups.Split(';');
            string[] second = secondGroups.Split(';');
            foreach (string firstGroup in first)
            {
                foreach (string secondGroup in second)
                {
                    if (string.Equals(firstGroup.Trim(), secondGroup.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool PathsConflict(string firstPath, string secondPath)
        {
            string first = firstPath.TrimEnd('/');
            string second = secondPath.TrimEnd('/');
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase) ||
                   first.StartsWith(second + "/", StringComparison.OrdinalIgnoreCase) ||
                   second.StartsWith(first + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAllowedContextualBindingPair(BindingEntry first, BindingEntry second)
        {
            if (!string.Equals(first.MapName, "Player", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(first.Path, "<Mouse>/rightButton", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(second.Path, "<Mouse>/rightButton", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.Equals(first.ActionName, "Guard", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(second.ActionName, "Aim", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(first.ActionName, "Aim", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(second.ActionName, "Guard", StringComparison.OrdinalIgnoreCase);
        }

        private void DestroyWorkingAsset()
        {
            if (_workingAsset == null)
            {
                return;
            }

            DestroyImmediate(_workingAsset);
            _workingAsset = null;
        }

        private enum DevicePage
        {
            KeyboardMouse,
            Gamepad
        }

        private enum DeviceKind
        {
            KeyboardMouse,
            Gamepad,
            Other
        }

        private sealed class BindingRowGroup
        {
            public BindingRowGroup(string name)
            {
                Name = name;
            }

            public string Name { get; }
            public List<int> BindingIndices { get; } = new();
        }

        private readonly struct BindingKey : IEquatable<BindingKey>
        {
            public BindingKey(Guid actionId, Guid bindingId)
            {
                ActionId = actionId;
                BindingId = bindingId;
            }

            private Guid ActionId { get; }
            private Guid BindingId { get; }

            public bool Equals(BindingKey other)
            {
                return ActionId.Equals(other.ActionId) && BindingId.Equals(other.BindingId);
            }

            public override bool Equals(object obj)
            {
                return obj is BindingKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(ActionId, BindingId);
            }
        }

        private readonly struct BindingEntry
        {
            public BindingEntry(string mapName, string actionName, Guid actionId, InputBinding binding)
            {
                Key = new BindingKey(actionId, binding.id);
                MapName = mapName;
                ActionName = actionName;
                Path = binding.path;
                Groups = binding.groups;
                KeyboardMouse = BindingBelongsToPage(binding, DevicePage.KeyboardMouse);
                Gamepad = BindingBelongsToPage(binding, DevicePage.Gamepad);
                string bindingName = string.IsNullOrEmpty(binding.name) ? "默认" : binding.name;
                DisplayName = $"{mapName}/{actionName}/{bindingName}";
            }

            public BindingKey Key { get; }
            public string MapName { get; }
            public string ActionName { get; }
            public string Path { get; }
            public string Groups { get; }
            public bool KeyboardMouse { get; }
            public bool Gamepad { get; }
            public string DisplayName { get; }
        }

        private readonly struct ConflictPair
        {
            public ConflictPair(
                BindingKey first,
                BindingKey second,
                bool keyboardMouse,
                bool gamepad)
            {
                First = first;
                Second = second;
                KeyboardMouse = keyboardMouse;
                Gamepad = gamepad;
            }

            public BindingKey First { get; }
            public BindingKey Second { get; }
            private bool KeyboardMouse { get; }
            private bool Gamepad { get; }

            public bool AppliesTo(DevicePage page)
            {
                return page == DevicePage.KeyboardMouse ? KeyboardMouse : Gamepad;
            }
        }
    }
}
