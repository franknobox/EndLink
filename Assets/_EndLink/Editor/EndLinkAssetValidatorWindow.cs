using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EndLink.Editor
{
    /// <summary>
    /// 美术资源校验结果窗口。
    /// </summary>
    internal sealed class EndLinkAssetValidatorWindow : EditorWindow
    {
        private const string MenuPath = "EndLink/Validation/Asset Validator";

        private readonly string[] _scopeNames = { "_Incoming", "全部 Art" };

        private Vector2 _scrollPosition;
        private int _selectedScope;
        private bool _showErrors = true;
        private bool _showWarnings = true;
        private bool _showInfo = true;
        private string _searchText = string.Empty;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            EndLinkAssetValidatorWindow window =
                GetWindow<EndLinkAssetValidatorWindow>("Asset Validator");
            window.minSize = new Vector2(720f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            EndLinkAssetValidation.IssuesChanged += HandleIssuesChanged;
            _selectedScope =
                EndLinkAssetValidation.LastScannedRoot == EndLinkAssetValidation.ArtRoot
                    ? 1
                    : 0;

            if (!EndLinkAssetValidation.LastScanTime.HasValue)
            {
                ScanSelectedScope();
            }
        }

        private void OnDisable()
        {
            EndLinkAssetValidation.IssuesChanged -= HandleIssuesChanged;
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
            EditorGUILayout.LabelField("EndLink Asset Validator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "_Incoming 资源导入后会自动校验；工具只报告问题，不会自动修改资源。",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            int nextScope = GUILayout.Toolbar(
                _selectedScope,
                _scopeNames,
                EditorStyles.toolbarButton,
                GUILayout.Width(190f));
            if (nextScope != _selectedScope)
            {
                _selectedScope = nextScope;
                ScanSelectedScope();
            }

            GUILayout.Space(8f);
            if (GUILayout.Button("重新扫描", EditorStyles.toolbarButton, GUILayout.Width(76f)))
            {
                ScanSelectedScope();
            }

            if (GUILayout.Button("复制报告", EditorStyles.toolbarButton, GUILayout.Width(76f)))
            {
                CopyReport();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                "自动检查 _Incoming：开启",
                EditorStyles.miniLabel,
                GUILayout.Width(145f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummary()
        {
            IReadOnlyList<EndLinkValidationIssue> issues = EndLinkAssetValidation.Issues;
            int errors = issues.Count(issue =>
                issue.Severity == EndLinkValidationSeverity.Error);
            int warnings = issues.Count(issue =>
                issue.Severity == EndLinkValidationSeverity.Warning);
            int information = issues.Count(issue =>
                issue.Severity == EndLinkValidationSeverity.Info);

            string scanTime = EndLinkAssetValidation.LastScanTime.HasValue
                ? EndLinkAssetValidation.LastScanTime.Value.ToString("HH:mm:ss")
                : "--:--:--";

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"错误：{errors}", GUILayout.Width(82f));
            EditorGUILayout.LabelField($"警告：{warnings}", GUILayout.Width(82f));
            EditorGUILayout.LabelField($"提示：{information}", GUILayout.Width(82f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                $"上次扫描：{scanTime}",
                EditorStyles.miniLabel,
                GUILayout.Width(125f));
            EditorGUILayout.EndHorizontal();

            if (errors == 0 && warnings == 0)
            {
                EditorGUILayout.HelpBox(
                    "当前扫描范围没有发现需要处理的错误或警告。",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "错误应在资源转入正式目录前解决；警告需要人工确认后再决定是否接受。",
                    errors > 0 ? MessageType.Error : MessageType.Warning);
            }
        }

        private void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();
            _showErrors = GUILayout.Toggle(_showErrors, "错误", GUILayout.Width(68f));
            _showWarnings = GUILayout.Toggle(_showWarnings, "警告", GUILayout.Width(68f));
            _showInfo = GUILayout.Toggle(_showInfo, "提示", GUILayout.Width(68f));
            GUILayout.Space(10f);
            EditorGUILayout.LabelField("搜索", GUILayout.Width(36f));
            _searchText = EditorGUILayout.TextField(_searchText);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(3f);
        }

        private void DrawIssueList()
        {
            List<EndLinkValidationIssue> visibleIssues = EndLinkAssetValidation.Issues
                .Where(IsVisible)
                .ToList();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            if (visibleIssues.Count == 0)
            {
                EditorGUILayout.LabelField(
                    "没有符合当前筛选条件的问题。",
                    EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                foreach (EndLinkValidationIssue issue in visibleIssues)
                {
                    DrawIssue(issue);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawIssue(EndLinkValidationIssue issue)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            GUIContent icon = GetSeverityIcon(issue.Severity);
            GUILayout.Label(icon, GUILayout.Width(20f), GUILayout.Height(20f));

            EditorGUILayout.BeginVertical();
            EditorGUILayout.SelectableLabel(
                $"[{issue.Code}] {issue.Message}",
                EditorStyles.wordWrappedLabel,
                GUILayout.MinHeight(32f));
            EditorGUILayout.SelectableLabel(
                issue.AssetPath,
                EditorStyles.miniLabel,
                GUILayout.Height(18f));
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("定位", GUILayout.Width(48f), GUILayout.Height(24f)))
            {
                PingAsset(issue.AssetPath);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private bool IsVisible(EndLinkValidationIssue issue)
        {
            bool severityVisible = issue.Severity switch
            {
                EndLinkValidationSeverity.Error => _showErrors,
                EndLinkValidationSeverity.Warning => _showWarnings,
                _ => _showInfo
            };

            if (!severityVisible)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(_searchText))
            {
                return true;
            }

            return issue.AssetPath.Contains(
                       _searchText,
                       StringComparison.OrdinalIgnoreCase)
                || issue.Message.Contains(
                    _searchText,
                    StringComparison.OrdinalIgnoreCase)
                || issue.Code.Contains(
                    _searchText,
                    StringComparison.OrdinalIgnoreCase);
        }

        private void ScanSelectedScope()
        {
            if (_selectedScope == 0)
            {
                EndLinkAssetValidation.ScanIncoming(false);
            }
            else
            {
                EndLinkAssetValidation.ScanAllArt(false);
            }

            Repaint();
        }

        private void CopyReport()
        {
            StringBuilder report = new();
            report.AppendLine($"EndLink Asset Validator - {EndLinkAssetValidation.LastScannedRoot}");
            foreach (EndLinkValidationIssue issue in EndLinkAssetValidation.Issues)
            {
                report.AppendLine(
                    $"[{issue.Severity}] [{issue.Code}] {issue.AssetPath}");
                report.AppendLine(issue.Message);
            }

            EditorGUIUtility.systemCopyBuffer = report.ToString();
            ShowNotification(new GUIContent("校验报告已复制"));
        }

        private void HandleIssuesChanged()
        {
            Repaint();
        }

        private static GUIContent GetSeverityIcon(EndLinkValidationSeverity severity)
        {
            return severity switch
            {
                EndLinkValidationSeverity.Error =>
                    EditorGUIUtility.IconContent("console.erroricon.sml"),
                EndLinkValidationSeverity.Warning =>
                    EditorGUIUtility.IconContent("console.warnicon.sml"),
                _ => EditorGUIUtility.IconContent("console.infoicon.sml")
            };
        }

        private static void PingAsset(string assetPath)
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
            {
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
