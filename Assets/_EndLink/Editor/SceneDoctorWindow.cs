using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EndLink.Editor
{
    /// <summary>
    /// 当前活动场景的只读配置体检窗口。
    /// </summary>
    internal sealed class SceneDoctorWindow : EditorWindow
    {
        private readonly List<SceneValidationIssue> _issues = new();
        private Vector2 _scrollPosition;
        private string _searchText = string.Empty;
        private string[] _categoryOptions = { "全部" };
        private int _categoryIndex;
        private bool _showErrors = true;
        private bool _showWarnings = true;
        private bool _showInfo = true;
        private DateTime? _lastScanTime;

        [MenuItem("EndLink/Validation/Scene Doctor")]
        private static void OpenWindow()
        {
            SceneDoctorWindow window = GetWindow<SceneDoctorWindow>();
            window.titleContent = new GUIContent("Scene Doctor");
            window.minSize = new Vector2(820f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            ScanScene();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawToolbar();
            DrawSummary();
            DrawFilters();
            DrawIssueList();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("EndLink 场景配置体检", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "只扫描当前活动场景，检查会阻断试玩或造成静默失效的常见配置问题。第一版只报告和定位，不会自动修改场景。",
                MessageType.Info);
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("重新扫描", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                {
                    ScanScene();
                }

                using (new EditorGUI.DisabledScope(_issues.Count == 0))
                {
                    if (GUILayout.Button("复制报告", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = BuildReport();
                    }
                }

                GUILayout.FlexibleSpace();
                Scene scene = SceneManager.GetActiveScene();
                string sceneLabel = scene.IsValid() ? scene.name : "无活动场景";
                GUILayout.Label($"当前场景：{sceneLabel}", EditorStyles.miniLabel);
            }
        }

        private void DrawSummary()
        {
            int errorCount = _issues.Count(issue => issue.Severity == SceneValidationSeverity.Error);
            int warningCount = _issues.Count(issue => issue.Severity == SceneValidationSeverity.Warning);
            int infoCount = _issues.Count(issue => issue.Severity == SceneValidationSeverity.Info);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"错误 {errorCount}", EditorStyles.boldLabel, GUILayout.Width(90f));
                GUILayout.Label($"警告 {warningCount}", GUILayout.Width(90f));
                GUILayout.Label($"提示 {infoCount}", GUILayout.Width(90f));
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    _lastScanTime.HasValue
                        ? $"扫描时间：{_lastScanTime.Value:HH:mm:ss}"
                        : "尚未扫描",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawFilters()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _showErrors = GUILayout.Toggle(_showErrors, "错误", EditorStyles.toolbarButton, GUILayout.Width(58f));
                _showWarnings = GUILayout.Toggle(_showWarnings, "警告", EditorStyles.toolbarButton, GUILayout.Width(58f));
                _showInfo = GUILayout.Toggle(_showInfo, "提示", EditorStyles.toolbarButton, GUILayout.Width(58f));

                GUILayout.Space(8f);
                _categoryIndex = EditorGUILayout.Popup(
                    _categoryIndex,
                    _categoryOptions,
                    GUILayout.Width(120f));

                GUILayout.Space(8f);
                _searchText = EditorGUILayout.TextField(
                    _searchText,
                    EditorStyles.toolbarSearchField,
                    GUILayout.MinWidth(180f));
            }

            EditorGUILayout.Space(3f);
        }

        private void DrawIssueList()
        {
            List<SceneValidationIssue> filteredIssues = _issues
                .Where(MatchesFilters)
                .ToList();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            if (filteredIssues.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    _issues.Count == 0
                        ? "未发现已纳入第一版规则的场景配置问题。"
                        : "当前筛选条件下没有结果。",
                    MessageType.None);
            }

            foreach (SceneValidationIssue issue in filteredIssues)
            {
                DrawIssue(issue);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawIssue(SceneValidationIssue issue)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUIContent icon = GetSeverityIcon(issue.Severity);
                    GUILayout.Label(icon, GUILayout.Width(20f), GUILayout.Height(20f));
                    GUILayout.Label(
                        $"[{issue.Category}] {issue.Code}",
                        EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(issue.Context == null))
                    {
                        if (GUILayout.Button("定位", GUILayout.Width(52f)))
                        {
                            LocateIssue(issue);
                        }
                    }
                }

                float messageHeight = Mathf.Max(
                    20f,
                    EditorStyles.wordWrappedLabel.CalcHeight(
                        new GUIContent(issue.Message),
                        Mathf.Max(200f, position.width - 56f)));
                EditorGUILayout.SelectableLabel(
                    issue.Message,
                    EditorStyles.wordWrappedLabel,
                    GUILayout.Height(messageHeight));

                if (!string.IsNullOrWhiteSpace(issue.ContextPath))
                {
                    EditorGUILayout.SelectableLabel(
                        issue.ContextPath,
                        EditorStyles.miniLabel,
                        GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
            }
        }

        private bool MatchesFilters(SceneValidationIssue issue)
        {
            bool severityMatches = issue.Severity switch
            {
                SceneValidationSeverity.Error => _showErrors,
                SceneValidationSeverity.Warning => _showWarnings,
                SceneValidationSeverity.Info => _showInfo,
                _ => true
            };

            if (!severityMatches)
            {
                return false;
            }

            if (_categoryIndex > 0
                && _categoryIndex < _categoryOptions.Length
                && issue.Category != _categoryOptions[_categoryIndex])
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(_searchText))
            {
                return true;
            }

            string search = _searchText.Trim();
            return issue.Code.Contains(search, StringComparison.OrdinalIgnoreCase)
                || issue.Category.Contains(search, StringComparison.OrdinalIgnoreCase)
                || issue.Message.Contains(search, StringComparison.OrdinalIgnoreCase)
                || issue.ContextPath.Contains(search, StringComparison.OrdinalIgnoreCase);
        }

        private void ScanScene()
        {
            _issues.Clear();
            _issues.AddRange(SceneValidator.ValidateActiveScene());
            _lastScanTime = DateTime.Now;
            RebuildCategoryOptions();
            Repaint();
        }

        private void RebuildCategoryOptions()
        {
            string selectedCategory = _categoryIndex >= 0 && _categoryIndex < _categoryOptions.Length
                ? _categoryOptions[_categoryIndex]
                : "全部";

            _categoryOptions = new[] { "全部" }
                .Concat(_issues.Select(issue => issue.Category).Distinct().OrderBy(value => value))
                .ToArray();

            _categoryIndex = Array.IndexOf(_categoryOptions, selectedCategory);
            if (_categoryIndex < 0)
            {
                _categoryIndex = 0;
            }
        }

        private string BuildReport()
        {
            Scene scene = SceneManager.GetActiveScene();
            StringBuilder builder = new();
            builder.AppendLine("EndLink Scene Doctor Report");
            builder.AppendLine($"Scene: {(scene.IsValid() ? scene.path : "<none>")}");
            builder.AppendLine($"Scan Time: {_lastScanTime:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"Issues: {_issues.Count}");
            builder.AppendLine();

            foreach (SceneValidationIssue issue in _issues)
            {
                builder.Append('[')
                    .Append(issue.Severity)
                    .Append("] [")
                    .Append(issue.Category)
                    .Append("] ")
                    .Append(issue.Code)
                    .Append(": ")
                    .AppendLine(issue.Message);

                if (!string.IsNullOrWhiteSpace(issue.ContextPath))
                {
                    builder.Append("  ").AppendLine(issue.ContextPath);
                }
            }

            return builder.ToString();
        }

        private static void LocateIssue(SceneValidationIssue issue)
        {
            Selection.activeObject = issue.Context;
            EditorGUIUtility.PingObject(issue.Context);

            if (issue.Context is GameObject or Component)
            {
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        }

        private static GUIContent GetSeverityIcon(SceneValidationSeverity severity)
        {
            return severity switch
            {
                SceneValidationSeverity.Error => EditorGUIUtility.IconContent("console.erroricon"),
                SceneValidationSeverity.Warning => EditorGUIUtility.IconContent("console.warnicon"),
                _ => EditorGUIUtility.IconContent("console.infoicon")
            };
        }
    }
}
