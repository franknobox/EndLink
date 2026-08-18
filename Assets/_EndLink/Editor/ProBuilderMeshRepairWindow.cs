using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace EndLink.Editor
{
    /// <summary>
    /// 检查 ProBuilder 面朝向，并提供独立的拓扑边界封口工具。
    /// </summary>
    internal sealed class ProBuilderMeshRepairWindow : EditorWindow
    {
        private const string MenuPath = "EndLink/Level/ProBuilder Mesh Repair";
        private const float DefaultPlanarityTolerance = 0.01f;

        private static readonly Color InwardColor = new(1f, 0.32f, 0.18f, 1f);
        private static readonly Color ClosedColor = new(0.15f, 0.85f, 1f, 1f);
        private static readonly Color OpenColor = new(1f, 0.58f, 0.12f, 1f);
        private static readonly Color AmbiguousColor = new(1f, 0.2f, 0.2f, 1f);
        private static readonly Color SelectedColor = new(1f, 0.9f, 0.15f, 1f);
        private static readonly Color NonManifoldColor = new(0.95f, 0.2f, 1f, 1f);

        private readonly List<BoundaryRegion> _regions = new();
        private readonly List<CanonicalEdge> _nonManifoldEdges = new();
        private readonly List<SharedPoint> _sharedPoints = new();
        private readonly List<FaceOrientationIssue> _faceIssues = new();

        private ProBuilderMesh _target;
        private RepairMode _repairMode = RepairMode.FaceOrientation;
        private Vector2 _faceScrollPosition;
        private Vector2 _boundaryScrollPosition;
        private float _planarityTolerance = DefaultPlanarityTolerance;
        private bool _previewInScene = true;
        private bool _flipNewFaceWinding;
        private string _statusMessage = "请选择一个 ProBuilder 物体。";
        private MessageType _statusType = MessageType.Info;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            ProBuilderMeshRepairWindow window =
                GetWindow<ProBuilderMeshRepairWindow>("ProBuilder Mesh Repair");
            window.minSize = new Vector2(620f, 460f);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawScenePreview;
            UseCurrentSelection();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawScenePreview;
        }

        private void OnSelectionChange()
        {
            UseCurrentSelection();
            Repaint();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawTargetControls();

            if (_repairMode == RepairMode.FaceOrientation)
            {
                DrawFaceSummary();
                DrawFaceList();
                DrawFaceRepairControls();
            }
            else
            {
                DrawBoundarySummary();
                DrawRegionList();
                DrawBoundaryRepairControls();
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("ProBuilder 网格修补", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            int nextMode = GUILayout.Toolbar(
                (int)_repairMode,
                new[] { "面朝向", "边界封口" },
                GUILayout.Height(24f));
            if (EditorGUI.EndChangeCheck())
            {
                _repairMode = (RepairMode)nextMode;
                ScanTarget();
            }

            string description = _repairMode == RepairMode.FaceOrientation
                ? "默认只查询疑似朝向物体内部的面。勾选确认后可翻转面方向，适合修复楼梯底面从外侧不可见的问题。"
                : "手动拓扑工具：显示开放边界并支持封口或桥接。开放边界可能是正常建模结构，不代表网格存在问题。";
            EditorGUILayout.HelpBox(description, MessageType.Info);
        }

        private void DrawTargetControls()
        {
            EditorGUI.BeginChangeCheck();
            ProBuilderMesh nextTarget = (ProBuilderMesh)EditorGUILayout.ObjectField(
                "目标网格",
                _target,
                typeof(ProBuilderMesh),
                true);
            if (EditorGUI.EndChangeCheck())
            {
                _target = nextTarget;
                ScanTarget();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("使用当前选择", GUILayout.Width(108f)))
                {
                    UseCurrentSelection();
                }

                using (new EditorGUI.DisabledScope(_target == null))
                {
                    if (GUILayout.Button("重新扫描", GUILayout.Width(86f)))
                    {
                        ScanTarget();
                    }
                }

                GUILayout.FlexibleSpace();
                _previewInScene = GUILayout.Toggle(
                    _previewInScene,
                    "Scene View 预览",
                    GUILayout.Width(132f));
            }

            if (_repairMode == RepairMode.BoundaryFill)
            {
                float nextTolerance = EditorGUILayout.FloatField(
                    new GUIContent(
                        "单面封口平面误差",
                        "误差以内的边界生成一个多边形面；超过误差的空间边界使用三角面组封闭。"),
                    _planarityTolerance);
                nextTolerance = Mathf.Clamp(nextTolerance, 0.0001f, 0.5f);
                if (!Mathf.Approximately(nextTolerance, _planarityTolerance))
                {
                    _planarityTolerance = nextTolerance;
                    EvaluateRegionPlanarity();
                    SceneView.RepaintAll();
                }
            }
        }

        private void DrawFaceSummary()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(_statusMessage, _statusType);

            if (_target == null)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"疑似反向面 {_faceIssues.Count}", GUILayout.Width(120f));
                GUILayout.FlexibleSpace();
                GUILayout.Label($"总面数 {_target.faceCount}", EditorStyles.miniLabel);
            }
        }

        private void DrawBoundarySummary()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(_statusMessage, _statusType);

            if (_target == null)
            {
                return;
            }

            int closedCount = _regions.Count(region => region.Kind == BoundaryKind.Closed);
            int openCount = _regions.Count(region => region.Kind == BoundaryKind.OpenChain);
            int ambiguousCount = _regions.Count(region => region.Kind == BoundaryKind.Ambiguous);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"闭合边界 {closedCount}", GUILayout.Width(100f));
                GUILayout.Label($"开放边链 {openCount}", GUILayout.Width(100f));
                GUILayout.Label($"歧义区域 {ambiguousCount}", GUILayout.Width(100f));
                GUILayout.Label($"非流形边 {_nonManifoldEdges.Count}", GUILayout.Width(110f));
                GUILayout.FlexibleSpace();
                GUILayout.Label($"顶点 {_target.vertexCount}", EditorStyles.miniLabel);
            }
        }

        private void DrawFaceList()
        {
            if (_target == null || _faceIssues.Count == 0)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("疑似反向的面", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("全部选择", GUILayout.Width(78f)))
                {
                    foreach (FaceOrientationIssue issue in _faceIssues)
                    {
                        issue.Selected = true;
                    }

                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("清除选择", GUILayout.Width(78f)))
                {
                    foreach (FaceOrientationIssue issue in _faceIssues)
                    {
                        issue.Selected = false;
                    }

                    SceneView.RepaintAll();
                }
            }

            _faceScrollPosition = EditorGUILayout.BeginScrollView(_faceScrollPosition);
            foreach (FaceOrientationIssue issue in _faceIssues)
            {
                DrawFaceIssue(issue);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawFaceIssue(FaceOrientationIssue issue)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                bool selected = EditorGUILayout.Toggle(issue.Selected, GUILayout.Width(18f));
                if (EditorGUI.EndChangeCheck())
                {
                    issue.Selected = selected;
                    SceneView.RepaintAll();
                }

                GUILayout.Label($"面 {issue.FaceIndex}", EditorStyles.boldLabel, GUILayout.Width(68f));
                GUILayout.Label("与相邻面的方向不一致", GUILayout.Width(160f));
                GUILayout.Label("请在 Scene View 确认", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("定位", GUILayout.Width(52f)))
                {
                    FrameFace(issue);
                }
            }
        }

        private void DrawFaceRepairControls()
        {
            EditorGUILayout.Space(4f);
            List<Face> selectedIssues = _faceIssues
                .Where(issue => issue.Selected)
                .Select(issue => issue.Face)
                .ToList();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(selectedIssues.Count == 0))
                {
                    if (GUILayout.Button("翻转勾选面", GUILayout.Height(28f)))
                    {
                        FlipFaces(selectedIssues, "翻转疑似反向面");
                    }
                }

                using (new EditorGUI.DisabledScope(_target == null))
                {
                    if (GUILayout.Button("翻转 ProBuilder 当前选面", GUILayout.Height(28f)))
                    {
                        FlipCurrentProBuilderSelection();
                    }
                }
            }

            EditorGUILayout.LabelField(
                "自动结果只报告与同一连通区域多数面方向不一致的面；整体朝内或缺少相邻面的结构，需要在 ProBuilder 面模式中手动选面后翻转。",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawRegionList()
        {
            if (_target == null || _regions.Count == 0)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("检测到的边界", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("选择闭合开口", GUILayout.Width(100f)))
                {
                    foreach (BoundaryRegion region in _regions)
                    {
                        region.Selected = region.Kind == BoundaryKind.Closed;
                    }

                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("清除选择", GUILayout.Width(78f)))
                {
                    foreach (BoundaryRegion region in _regions)
                    {
                        region.Selected = false;
                    }

                    SceneView.RepaintAll();
                }
            }

            _boundaryScrollPosition = EditorGUILayout.BeginScrollView(_boundaryScrollPosition);
            foreach (BoundaryRegion region in _regions)
            {
                DrawRegion(region);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawRegion(BoundaryRegion region)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                bool selected = EditorGUILayout.Toggle(region.Selected, GUILayout.Width(18f));
                if (EditorGUI.EndChangeCheck())
                {
                    region.Selected = selected;
                    SceneView.RepaintAll();
                }

                string kindLabel = region.Kind switch
                {
                    BoundaryKind.Closed => "闭合",
                    BoundaryKind.OpenChain => "开放",
                    _ => "歧义"
                };
                GUILayout.Label($"#{region.Id}", EditorStyles.boldLabel, GUILayout.Width(34f));
                GUILayout.Label(kindLabel, GUILayout.Width(42f));
                GUILayout.Label($"边 {region.Edges.Count}", GUILayout.Width(54f));
                GUILayout.Label($"点 {region.OrderedSharedIndices.Count}", GUILayout.Width(54f));

                if (region.Kind == BoundaryKind.Closed)
                {
                    string planarLabel = region.IsPlanar
                        ? $"单面封口，误差 {region.PlanarityError:F4}"
                        : $"三角化封口，误差 {region.PlanarityError:F4}";
                    GUILayout.Label(planarLabel, GUILayout.MinWidth(150f));
                }
                else
                {
                    GUILayout.Label(region.Description, GUILayout.MinWidth(150f));
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("定位", GUILayout.Width(52f)))
                {
                    FrameRegion(region);
                }
            }
        }

        private void DrawBoundaryRepairControls()
        {
            EditorGUILayout.Space(4f);
            _flipNewFaceWinding = EditorGUILayout.Toggle(
                new GUIContent(
                    "反转新面方向",
                    "新面的正反面不符合预期时，撤销后勾选此项重新修补。"),
                _flipNewFaceWinding);

            List<BoundaryRegion> selected = _regions.Where(region => region.Selected).ToList();
            bool canFill = selected.Count > 0
                && selected.All(region => region.Kind == BoundaryKind.Closed);
            bool canBridge = selected.Count == 2
                && selected.All(region => region.Kind == BoundaryKind.Closed)
                && selected[0].OrderedSharedIndices.Count == selected[1].OrderedSharedIndices.Count;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!canFill))
                {
                    if (GUILayout.Button("封闭所选开口", GUILayout.Height(28f)))
                    {
                        FillSelectedRegions(selected);
                    }
                }

                using (new EditorGUI.DisabledScope(!canBridge))
                {
                    if (GUILayout.Button("桥接所选两圈边界", GUILayout.Height(28f)))
                    {
                        BridgeSelectedRegions(selected[0], selected[1]);
                    }
                }
            }

            if (selected.Count > 0 && !canFill && !canBridge)
            {
                EditorGUILayout.LabelField(
                    "当前选择不满足安全修补条件。只有闭合边界能直接封口；开放边链和歧义边界需要先手工整理拓扑。",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void FlipCurrentProBuilderSelection()
        {
            Face[] selectedFaces = _target.GetSelectedFaces();
            if (selectedFaces == null || selectedFaces.Length == 0)
            {
                SetStatus(
                    "ProBuilder 当前没有选中的面。请切换到面选择模式后选中目标面。",
                    MessageType.Warning);
                return;
            }

            FlipFaces(selectedFaces, "翻转 ProBuilder 当前选面");
        }

        private void FlipFaces(IEnumerable<Face> faces, string undoName)
        {
            List<Face> faceList = faces
                .Where(face => face != null)
                .Distinct()
                .ToList();
            if (_target == null || faceList.Count == 0)
            {
                return;
            }

            ExecuteMeshRepair(undoName, () =>
            {
                foreach (Face face in faceList)
                {
                    face.Reverse();
                }
            });
        }

        private void UseCurrentSelection()
        {
            GameObject selectedObject = Selection.activeGameObject;
            _target = selectedObject != null
                ? selectedObject.GetComponentInParent<ProBuilderMesh>()
                : null;
            ScanTarget();
        }

        private void ScanTarget()
        {
            _regions.Clear();
            _nonManifoldEdges.Clear();
            _sharedPoints.Clear();
            _faceIssues.Clear();

            if (_target == null)
            {
                SetStatus("请选择一个带 ProBuilderMesh 的物体。", MessageType.Info);
                SceneView.RepaintAll();
                return;
            }

            IList<Vector3> positions = _target.positions;
            if (positions == null || positions.Count == 0)
            {
                SetStatus("目标没有可扫描的 ProBuilder 顶点数据。", MessageType.Warning);
                return;
            }

            if (_repairMode == RepairMode.FaceOrientation)
            {
                ScanFaceOrientation(positions);
            }
            else
            {
                ScanBoundaryTopology(positions);
            }

            SceneView.RepaintAll();
        }

        private void ScanFaceOrientation(IList<Vector3> positions)
        {
            IList<SharedVertex> sharedVertices = _target.sharedVertices;
            if (sharedVertices == null)
            {
                SetStatus("目标没有可扫描的 ProBuilder 共享顶点数据。", MessageType.Warning);
                return;
            }

            int[] rawToShared = BuildSharedPointLookup(positions, sharedVertices);
            IList<Face> faces = _target.faces;
            Dictionary<CanonicalEdge, List<FaceEdgeUse>> edgeUses = new();
            for (int faceIndex = 0; faceIndex < faces.Count; faceIndex++)
            {
                Face face = faces[faceIndex];
                foreach (Edge edge in face.edges)
                {
                    if (!TryGetSharedIndex(rawToShared, edge.a, out int a)
                        || !TryGetSharedIndex(rawToShared, edge.b, out int b)
                        || a == b)
                    {
                        continue;
                    }

                    CanonicalEdge canonicalEdge = new(a, b);
                    if (!edgeUses.TryGetValue(canonicalEdge, out List<FaceEdgeUse> uses))
                    {
                        uses = new List<FaceEdgeUse>(2);
                        edgeUses.Add(canonicalEdge, uses);
                    }

                    uses.Add(new FaceEdgeUse(faceIndex, a, b));
                }
            }

            Dictionary<int, List<FaceAdjacency>> adjacency = new();
            for (int faceIndex = 0; faceIndex < faces.Count; faceIndex++)
            {
                adjacency.Add(faceIndex, new List<FaceAdjacency>());
            }

            foreach (List<FaceEdgeUse> uses in edgeUses.Values)
            {
                if (uses.Count != 2)
                {
                    continue;
                }

                FaceEdgeUse first = uses[0];
                FaceEdgeUse second = uses[1];
                bool sameDirection = first.A == second.A && first.B == second.B;
                adjacency[first.FaceIndex].Add(
                    new FaceAdjacency(second.FaceIndex, sameDirection));
                adjacency[second.FaceIndex].Add(
                    new FaceAdjacency(first.FaceIndex, sameDirection));
            }

            bool?[] flipFlags = new bool?[faces.Count];
            for (int seed = 0; seed < faces.Count; seed++)
            {
                if (flipFlags[seed].HasValue)
                {
                    continue;
                }

                List<int> component = new();
                Queue<int> queue = new();
                flipFlags[seed] = false;
                queue.Enqueue(seed);

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    component.Add(current);
                    bool currentFlip = flipFlags[current].GetValueOrDefault();

                    foreach (FaceAdjacency neighbor in adjacency[current])
                    {
                        bool expectedFlip = currentFlip ^ neighbor.RequiresOppositeFlip;
                        if (!flipFlags[neighbor.FaceIndex].HasValue)
                        {
                            flipFlags[neighbor.FaceIndex] = expectedFlip;
                            queue.Enqueue(neighbor.FaceIndex);
                        }
                    }
                }

                int flippedCount = component.Count(index => flipFlags[index] == true);
                int unflippedCount = component.Count - flippedCount;
                if (flippedCount == unflippedCount)
                {
                    continue;
                }

                bool minorityFlag = flippedCount < unflippedCount;
                foreach (int faceIndex in component)
                {
                    if (flipFlags[faceIndex] != minorityFlag)
                    {
                        continue;
                    }

                    Face face = faces[faceIndex];
                    Vector3 normal = UnityEngine.ProBuilder.Math.Normal(_target, face).normalized;
                    _faceIssues.Add(new FaceOrientationIssue
                    {
                        FaceIndex = faceIndex,
                        Face = face,
                        Center = CalculateFaceCenter(face, positions),
                        Normal = normal,
                        LocalBounds = CalculateFaceBounds(face, positions)
                    });
                }
            }

            if (_faceIssues.Count == 0)
            {
                SetStatus(
                    "未发现与相邻面方向明显不一致的面。拓扑边界不会计入此结果。",
                    MessageType.Info);
            }
            else
            {
                SetStatus(
                    $"发现 {_faceIssues.Count} 个疑似反向面。请结合 Scene View 确认后再翻转。",
                    MessageType.Warning);
            }
        }

        private void ScanBoundaryTopology(IList<Vector3> positions)
        {
            IList<SharedVertex> sharedVertices = _target.sharedVertices;
            if (sharedVertices == null)
            {
                SetStatus("目标没有可扫描的 ProBuilder 共享顶点数据。", MessageType.Warning);
                return;
            }

            int[] rawToShared = BuildSharedPointLookup(positions, sharedVertices);
            Dictionary<CanonicalEdge, int> edgeUseCounts = new();

            foreach (Face face in _target.faces)
            {
                foreach (Edge edge in face.edges)
                {
                    if (!TryGetSharedIndex(rawToShared, edge.a, out int a)
                        || !TryGetSharedIndex(rawToShared, edge.b, out int b)
                        || a == b)
                    {
                        continue;
                    }

                    CanonicalEdge canonicalEdge = new(a, b);
                    edgeUseCounts.TryGetValue(canonicalEdge, out int useCount);
                    edgeUseCounts[canonicalEdge] = useCount + 1;
                }
            }

            List<CanonicalEdge> boundaryEdges = new();
            foreach ((CanonicalEdge edge, int useCount) in edgeUseCounts)
            {
                if (useCount == 1)
                {
                    boundaryEdges.Add(edge);
                }
                else if (useCount > 2)
                {
                    _nonManifoldEdges.Add(edge);
                }
            }

            BuildBoundaryRegions(boundaryEdges);
            EvaluateRegionPlanarity();

            if (_regions.Count == 0 && _nonManifoldEdges.Count == 0)
            {
                SetStatus("未检测到开放边界或非流形边。网格拓扑当前是闭合的。", MessageType.Info);
            }
            else
            {
                SetStatus(
                    $"拓扑扫描完成：显示 {_regions.Count} 个边界区域、{_nonManifoldEdges.Count} 条非流形边。这些结果不默认视为问题。",
                    _nonManifoldEdges.Count > 0 ? MessageType.Warning : MessageType.Info);
            }
        }

        private static Vector3 CalculateFaceCenter(Face face, IList<Vector3> positions)
        {
            Vector3 center = Vector3.zero;
            IReadOnlyList<int> indices = face.distinctIndexes;
            for (int index = 0; index < indices.Count; index++)
            {
                center += positions[indices[index]];
            }

            return indices.Count > 0 ? center / indices.Count : center;
        }

        private static Bounds CalculateFaceBounds(Face face, IList<Vector3> positions)
        {
            IReadOnlyList<int> indices = face.distinctIndexes;
            if (indices.Count == 0)
            {
                return default;
            }

            Bounds bounds = new(positions[indices[0]], Vector3.zero);
            for (int index = 1; index < indices.Count; index++)
            {
                bounds.Encapsulate(positions[indices[index]]);
            }

            return bounds;
        }

        private int[] BuildSharedPointLookup(
            IList<Vector3> positions,
            IList<SharedVertex> sharedVertices)
        {
            int[] rawToShared = Enumerable.Repeat(-1, positions.Count).ToArray();

            for (int sharedIndex = 0; sharedIndex < sharedVertices.Count; sharedIndex++)
            {
                SharedVertex sharedVertex = sharedVertices[sharedIndex];
                Vector3 positionSum = Vector3.zero;
                int count = 0;
                int representative = -1;

                foreach (int rawIndex in sharedVertex)
                {
                    if (rawIndex < 0 || rawIndex >= positions.Count)
                    {
                        continue;
                    }

                    rawToShared[rawIndex] = sharedIndex;
                    representative = representative < 0 ? rawIndex : representative;
                    positionSum += positions[rawIndex];
                    count++;
                }

                Vector3 position = count > 0 ? positionSum / count : Vector3.zero;
                _sharedPoints.Add(new SharedPoint(position, representative));
            }

            return rawToShared;
        }

        private static bool TryGetSharedIndex(int[] lookup, int rawIndex, out int sharedIndex)
        {
            sharedIndex = rawIndex >= 0 && rawIndex < lookup.Length ? lookup[rawIndex] : -1;
            return sharedIndex >= 0;
        }

        private void BuildBoundaryRegions(List<CanonicalEdge> boundaryEdges)
        {
            Dictionary<int, List<int>> adjacency = new();
            foreach (CanonicalEdge edge in boundaryEdges)
            {
                AddNeighbor(adjacency, edge.A, edge.B);
                AddNeighbor(adjacency, edge.B, edge.A);
            }

            HashSet<int> remainingVertices = new(adjacency.Keys);
            int regionId = 1;
            while (remainingVertices.Count > 0)
            {
                int seed = remainingVertices.First();
                HashSet<int> componentVertices = CollectComponent(seed, adjacency);
                remainingVertices.ExceptWith(componentVertices);

                List<CanonicalEdge> componentEdges = boundaryEdges
                    .Where(edge => componentVertices.Contains(edge.A)
                        && componentVertices.Contains(edge.B))
                    .ToList();
                BoundaryKind kind = ClassifyBoundary(componentVertices, componentEdges, adjacency);
                List<int> ordered = OrderBoundaryVertices(componentVertices, adjacency, kind);

                BoundaryRegion region = new()
                {
                    Id = regionId++,
                    Kind = kind,
                    Edges = componentEdges,
                    OrderedSharedIndices = ordered,
                    Description = BuildBoundaryDescription(kind, componentVertices, adjacency)
                };
                region.LocalBounds = CalculateLocalBounds(componentVertices);
                _regions.Add(region);
            }
        }

        private static void AddNeighbor(Dictionary<int, List<int>> adjacency, int vertex, int neighbor)
        {
            if (!adjacency.TryGetValue(vertex, out List<int> neighbors))
            {
                neighbors = new List<int>();
                adjacency.Add(vertex, neighbors);
            }

            if (!neighbors.Contains(neighbor))
            {
                neighbors.Add(neighbor);
            }
        }

        private static HashSet<int> CollectComponent(
            int seed,
            IReadOnlyDictionary<int, List<int>> adjacency)
        {
            HashSet<int> visited = new();
            Queue<int> queue = new();
            queue.Enqueue(seed);
            visited.Add(seed);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int neighbor in adjacency[current])
                {
                    if (visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return visited;
        }

        private static BoundaryKind ClassifyBoundary(
            IReadOnlyCollection<int> vertices,
            IReadOnlyCollection<CanonicalEdge> edges,
            IReadOnlyDictionary<int, List<int>> adjacency)
        {
            int degreeOneCount = vertices.Count(vertex => adjacency[vertex].Count == 1);
            bool onlySimpleDegrees = vertices.All(vertex => adjacency[vertex].Count is 1 or 2);
            bool allDegreeTwo = vertices.All(vertex => adjacency[vertex].Count == 2);

            if (vertices.Count >= 3 && edges.Count == vertices.Count && allDegreeTwo)
            {
                return BoundaryKind.Closed;
            }

            if (vertices.Count >= 2
                && edges.Count == vertices.Count - 1
                && degreeOneCount == 2
                && onlySimpleDegrees)
            {
                return BoundaryKind.OpenChain;
            }

            return BoundaryKind.Ambiguous;
        }

        private static List<int> OrderBoundaryVertices(
            IReadOnlyCollection<int> vertices,
            IReadOnlyDictionary<int, List<int>> adjacency,
            BoundaryKind kind)
        {
            if (kind == BoundaryKind.Ambiguous)
            {
                return vertices.OrderBy(vertex => vertex).ToList();
            }

            int start = kind == BoundaryKind.OpenChain
                ? vertices.First(vertex => adjacency[vertex].Count == 1)
                : vertices.Min();
            List<int> ordered = new(vertices.Count);
            int previous = -1;
            int current = start;

            while (ordered.Count < vertices.Count)
            {
                ordered.Add(current);
                int next = -1;
                foreach (int neighbor in adjacency[current])
                {
                    if (neighbor != previous && !ordered.Contains(neighbor))
                    {
                        next = neighbor;
                        break;
                    }
                }

                if (next < 0)
                {
                    break;
                }

                previous = current;
                current = next;
            }

            return ordered;
        }

        private static string BuildBoundaryDescription(
            BoundaryKind kind,
            IReadOnlyCollection<int> vertices,
            IReadOnlyDictionary<int, List<int>> adjacency)
        {
            if (kind == BoundaryKind.OpenChain)
            {
                return "两端未闭合，不能直接封面";
            }

            int branchCount = vertices.Count(vertex => adjacency[vertex].Count > 2);
            return branchCount > 0
                ? $"包含 {branchCount} 个分叉点"
                : "拓扑关系无法组成单一边界";
        }

        private Bounds CalculateLocalBounds(IEnumerable<int> sharedIndices)
        {
            bool initialized = false;
            Bounds bounds = default;
            foreach (int sharedIndex in sharedIndices)
            {
                if (!TryGetSharedPoint(sharedIndex, out SharedPoint point))
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = new Bounds(point.Position, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(point.Position);
                }
            }

            return bounds;
        }

        private void EvaluateRegionPlanarity()
        {
            foreach (BoundaryRegion region in _regions)
            {
                region.IsPlanar = false;
                region.PlanarityError = float.PositiveInfinity;
                region.PlaneNormal = Vector3.up;

                if (region.Kind != BoundaryKind.Closed
                    || region.OrderedSharedIndices.Count < 3)
                {
                    continue;
                }

                List<Vector3> points = region.OrderedSharedIndices
                    .Select(index => _sharedPoints[index].Position)
                    .ToList();
                Vector3 normal = CalculateNewellNormal(points);
                if (normal.sqrMagnitude < 0.000001f)
                {
                    continue;
                }

                normal.Normalize();
                Vector3 origin = points[0];
                float maximumError = points.Max(point =>
                    Mathf.Abs(Vector3.Dot(point - origin, normal)));

                region.PlaneNormal = normal;
                region.PlanarityError = maximumError;
                region.IsPlanar = maximumError <= _planarityTolerance;
            }
        }

        private static Vector3 CalculateNewellNormal(IReadOnlyList<Vector3> points)
        {
            Vector3 normal = Vector3.zero;
            for (int index = 0; index < points.Count; index++)
            {
                Vector3 current = points[index];
                Vector3 next = points[(index + 1) % points.Count];
                normal.x += (current.y - next.y) * (current.z + next.z);
                normal.y += (current.z - next.z) * (current.x + next.x);
                normal.z += (current.x - next.x) * (current.y + next.y);
            }

            return normal;
        }

        private void FillSelectedRegions(IReadOnlyList<BoundaryRegion> selectedRegions)
        {
            if (_target == null || selectedRegions.Count == 0)
            {
                return;
            }

            ExecuteMeshRepair("封闭 ProBuilder 开口", () =>
            {
                foreach (BoundaryRegion region in selectedRegions)
                {
                    List<int> rawIndices = GetRawVertexIndices(region.OrderedSharedIndices);
                    if (region.IsPlanar)
                    {
                        if (_flipNewFaceWinding)
                        {
                            rawIndices.Reverse();
                        }

                        if (_target.CreatePolygon(rawIndices, false) == null)
                        {
                            throw new InvalidOperationException(
                                $"边界 #{region.Id} 无法生成有效多边形。请检查边界顺序或改用手工修复。");
                        }
                    }
                    else
                    {
                        FillNonPlanarRegion(region, rawIndices);
                    }
                }
            });
        }

        private void FillNonPlanarRegion(BoundaryRegion region, IReadOnlyList<int> rawIndices)
        {
            List<BoundaryTriangle> triangles = TriangulateBoundary(region.OrderedSharedIndices);
            if (triangles.Count != rawIndices.Count - 2)
            {
                throw new InvalidOperationException(
                    $"边界 #{region.Id} 无法得到稳定的三角化结果，请先手工整理边界。");
            }

            foreach (BoundaryTriangle triangle in triangles)
            {
                List<int> triangleIndices = new()
                {
                    rawIndices[triangle.A],
                    rawIndices[triangle.B],
                    rawIndices[triangle.C]
                };
                if (_flipNewFaceWinding)
                {
                    triangleIndices.Reverse();
                }

                if (_target.CreatePolygon(triangleIndices, false) == null)
                {
                    throw new InvalidOperationException(
                        $"边界 #{region.Id} 的三角面创建失败，边界中可能存在共线点或交叉边。");
                }
            }
        }

        private List<BoundaryTriangle> TriangulateBoundary(IReadOnlyList<int> sharedIndices)
        {
            int count = sharedIndices.Count;
            float[,] costs = new float[count, count];
            int[,] splits = new int[count, count];

            for (int start = 0; start < count; start++)
            {
                for (int end = 0; end < count; end++)
                {
                    costs[start, end] = float.PositiveInfinity;
                    splits[start, end] = -1;
                }

                costs[start, start] = 0f;
                if (start + 1 < count)
                {
                    costs[start, start + 1] = 0f;
                }
            }

            for (int gap = 2; gap < count; gap++)
            {
                for (int start = 0; start + gap < count; start++)
                {
                    int end = start + gap;
                    for (int split = start + 1; split < end; split++)
                    {
                        float triangleCost = CalculateTriangleCost(
                            sharedIndices[start],
                            sharedIndices[split],
                            sharedIndices[end]);
                        if (float.IsPositiveInfinity(triangleCost)
                            || float.IsPositiveInfinity(costs[start, split])
                            || float.IsPositiveInfinity(costs[split, end]))
                        {
                            continue;
                        }

                        float totalCost = costs[start, split]
                            + costs[split, end]
                            + triangleCost;
                        if (totalCost < costs[start, end])
                        {
                            costs[start, end] = totalCost;
                            splits[start, end] = split;
                        }
                    }
                }
            }

            List<BoundaryTriangle> triangles = new(count - 2);
            CollectBoundaryTriangles(0, count - 1, splits, triangles);
            return triangles;
        }

        private float CalculateTriangleCost(int firstIndex, int secondIndex, int thirdIndex)
        {
            Vector3 first = _sharedPoints[firstIndex].Position;
            Vector3 second = _sharedPoints[secondIndex].Position;
            Vector3 third = _sharedPoints[thirdIndex].Position;
            float doubledArea = Vector3.Cross(second - first, third - first).magnitude;
            if (doubledArea <= 0.000001f)
            {
                return float.PositiveInfinity;
            }

            return (first - second).sqrMagnitude
                + (second - third).sqrMagnitude
                + (third - first).sqrMagnitude;
        }

        private static void CollectBoundaryTriangles(
            int start,
            int end,
            int[,] splits,
            ICollection<BoundaryTriangle> triangles)
        {
            if (end - start < 2)
            {
                return;
            }

            int split = splits[start, end];
            if (split <= start || split >= end)
            {
                return;
            }

            triangles.Add(new BoundaryTriangle(start, split, end));
            CollectBoundaryTriangles(start, split, splits, triangles);
            CollectBoundaryTriangles(split, end, splits, triangles);
        }

        private void BridgeSelectedRegions(BoundaryRegion first, BoundaryRegion second)
        {
            if (_target == null)
            {
                return;
            }

            List<int> firstRaw = GetRawVertexIndices(first.OrderedSharedIndices);
            List<int> secondRaw = FindBestLoopAlignment(first, second);
            ExecuteMeshRepair("桥接 ProBuilder 边界", () =>
            {
                for (int index = 0; index < firstRaw.Count; index++)
                {
                    int next = (index + 1) % firstRaw.Count;
                    List<int> quad = new()
                    {
                        firstRaw[index],
                        firstRaw[next],
                        secondRaw[next],
                        secondRaw[index]
                    };
                    if (_flipNewFaceWinding)
                    {
                        quad.Reverse();
                    }

                    if (_target.CreatePolygon(quad, false) == null)
                    {
                        throw new InvalidOperationException(
                            $"第 {index + 1} 个桥接面创建失败。两圈边界可能不适合直接桥接。");
                    }
                }
            });
        }

        private List<int> FindBestLoopAlignment(BoundaryRegion first, BoundaryRegion second)
        {
            IReadOnlyList<int> firstLoop = first.OrderedSharedIndices;
            IReadOnlyList<int> secondLoop = second.OrderedSharedIndices;
            int count = firstLoop.Count;
            float bestScore = float.PositiveInfinity;
            int bestShift = 0;
            bool bestReverse = false;

            for (int reverseFlag = 0; reverseFlag < 2; reverseFlag++)
            {
                bool reverse = reverseFlag == 1;
                for (int shift = 0; shift < count; shift++)
                {
                    float score = 0f;
                    for (int index = 0; index < count; index++)
                    {
                        int secondIndex = reverse
                            ? PositiveModulo(shift - index, count)
                            : (shift + index) % count;
                        Vector3 firstPosition = _sharedPoints[firstLoop[index]].Position;
                        Vector3 secondPosition = _sharedPoints[secondLoop[secondIndex]].Position;
                        score += (firstPosition - secondPosition).sqrMagnitude;
                    }

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestShift = shift;
                        bestReverse = reverse;
                    }
                }
            }

            List<int> alignedRawIndices = new(count);
            for (int index = 0; index < count; index++)
            {
                int secondIndex = bestReverse
                    ? PositiveModulo(bestShift - index, count)
                    : (bestShift + index) % count;
                alignedRawIndices.Add(_sharedPoints[secondLoop[secondIndex]].RepresentativeRawIndex);
            }

            return alignedRawIndices;
        }

        private static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private List<int> GetRawVertexIndices(IEnumerable<int> sharedIndices)
        {
            List<int> result = new();
            foreach (int sharedIndex in sharedIndices)
            {
                if (!TryGetSharedPoint(sharedIndex, out SharedPoint point)
                    || point.RepresentativeRawIndex < 0)
                {
                    throw new InvalidOperationException("边界包含无效的 ProBuilder 共享顶点。");
                }

                result.Add(point.RepresentativeRawIndex);
            }

            return result;
        }

        private void ExecuteMeshRepair(string undoName, Action repairAction)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);

            MeshFilter meshFilter = _target.GetComponent<MeshFilter>();
            MeshCollider[] meshColliders = _target.GetComponents<MeshCollider>();
            List<Object> undoObjects = new() { _target };
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                undoObjects.Add(meshFilter.sharedMesh);
            }

            undoObjects.AddRange(meshColliders.Cast<Object>());
            Undo.RegisterCompleteObjectUndo(undoObjects.ToArray(), undoName);

            try
            {
                repairAction();
                _target.ToMesh();
                _target.Refresh();
                RefreshMeshColliders(meshFilter, meshColliders);

                EditorUtility.SetDirty(_target);
                if (_target.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(_target.gameObject.scene);
                }

                Undo.CollapseUndoOperations(undoGroup);
                ScanTarget();
                SetStatus($"{undoName}完成。可使用 Ctrl+Z 撤销。", MessageType.Info);
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                ScanTarget();
                SetStatus($"修补失败：{exception.Message}", MessageType.Error);
                Debug.LogException(exception, _target);
            }
        }

        private static void RefreshMeshColliders(
            MeshFilter meshFilter,
            IEnumerable<MeshCollider> meshColliders)
        {
            if (meshFilter == null)
            {
                return;
            }

            Mesh mesh = meshFilter.sharedMesh;
            foreach (MeshCollider meshCollider in meshColliders)
            {
                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh = mesh;
                EditorUtility.SetDirty(meshCollider);
            }

            Physics.SyncTransforms();
        }

        private void FrameRegion(BoundaryRegion region)
        {
            if (_target == null || SceneView.lastActiveSceneView == null)
            {
                return;
            }

            Selection.activeGameObject = _target.gameObject;
            Bounds worldBounds = TransformBounds(_target.transform, region.LocalBounds);
            worldBounds.Expand(Mathf.Max(0.5f, worldBounds.size.magnitude * 0.25f));
            SceneView.lastActiveSceneView.Frame(worldBounds, false);
        }

        private void FrameFace(FaceOrientationIssue issue)
        {
            if (_target == null || SceneView.lastActiveSceneView == null)
            {
                return;
            }

            Selection.activeGameObject = _target.gameObject;
            Bounds worldBounds = TransformBounds(_target.transform, issue.LocalBounds);
            worldBounds.Expand(Mathf.Max(0.5f, worldBounds.size.magnitude * 0.5f));
            SceneView.lastActiveSceneView.Frame(worldBounds, false);
        }

        private void DrawScenePreview(SceneView sceneView)
        {
            bool hasPreview = _repairMode == RepairMode.FaceOrientation
                ? _faceIssues.Count > 0
                : _regions.Count > 0 || _nonManifoldEdges.Count > 0;
            if (!_previewInScene || _target == null || !hasPreview)
            {
                return;
            }

            Matrix4x4 previousMatrix = Handles.matrix;
            Color previousColor = Handles.color;
            CompareFunction previousZTest = Handles.zTest;
            Handles.matrix = _target.transform.localToWorldMatrix;
            Handles.zTest = CompareFunction.LessEqual;

            if (_repairMode == RepairMode.FaceOrientation)
            {
                DrawFaceOrientationPreview();
            }
            else
            {
                DrawBoundaryPreview();
            }

            Handles.matrix = previousMatrix;
            Handles.color = previousColor;
            Handles.zTest = previousZTest;
        }

        private void DrawFaceOrientationPreview()
        {
            IList<Vector3> positions = _target.positions;
            foreach (FaceOrientationIssue issue in _faceIssues)
            {
                Handles.color = issue.Selected ? SelectedColor : InwardColor;
                foreach (Edge edge in issue.Face.edges)
                {
                    if (edge.a >= 0
                        && edge.a < positions.Count
                        && edge.b >= 0
                        && edge.b < positions.Count)
                    {
                        Handles.DrawAAPolyLine(5f, positions[edge.a], positions[edge.b]);
                    }
                }

                float normalLength = Mathf.Max(0.35f, issue.LocalBounds.size.magnitude * 0.2f);
                Handles.DrawLine(
                    issue.Center,
                    issue.Center + issue.Normal * normalLength,
                    issue.Selected ? 4f : 2f);
                Handles.Label(
                    issue.Center,
                    $"面 {issue.FaceIndex}",
                    issue.Selected ? EditorStyles.whiteBoldLabel : EditorStyles.miniBoldLabel);
            }
        }

        private void DrawBoundaryPreview()
        {
            foreach (BoundaryRegion region in _regions)
            {
                Handles.color = region.Selected ? SelectedColor : GetRegionColor(region.Kind);
                foreach (CanonicalEdge edge in region.Edges)
                {
                    if (TryGetSharedPoint(edge.A, out SharedPoint first)
                        && TryGetSharedPoint(edge.B, out SharedPoint second))
                    {
                        Handles.DrawAAPolyLine(4f, first.Position, second.Position);
                    }
                }

                Vector3 labelPosition = region.LocalBounds.center;
                Handles.Label(
                    labelPosition,
                    $"#{region.Id}",
                    region.Selected ? EditorStyles.whiteBoldLabel : EditorStyles.miniBoldLabel);

                if (region.Selected && region.Kind == BoundaryKind.Closed)
                {
                    float normalLength = Mathf.Max(0.35f, region.LocalBounds.size.magnitude * 0.15f);
                    Handles.DrawLine(
                        region.LocalBounds.center,
                        region.LocalBounds.center + region.PlaneNormal * normalLength,
                        2f);
                }
            }

            Handles.color = NonManifoldColor;
            foreach (CanonicalEdge edge in _nonManifoldEdges)
            {
                if (TryGetSharedPoint(edge.A, out SharedPoint first)
                    && TryGetSharedPoint(edge.B, out SharedPoint second))
                {
                    Handles.DrawAAPolyLine(6f, first.Position, second.Position);
                }
            }
        }

        private bool TryGetSharedPoint(int index, out SharedPoint point)
        {
            if (index >= 0 && index < _sharedPoints.Count)
            {
                point = _sharedPoints[index];
                return true;
            }

            point = default;
            return false;
        }

        private static Color GetRegionColor(BoundaryKind kind)
        {
            return kind switch
            {
                BoundaryKind.Closed => ClosedColor,
                BoundaryKind.OpenChain => OpenColor,
                _ => AmbiguousColor
            };
        }

        private static Bounds TransformBounds(Transform transform, Bounds localBounds)
        {
            Vector3 worldCenter = transform.TransformPoint(localBounds.center);
            Vector3 localExtents = localBounds.extents;
            Vector3 axisX = transform.TransformVector(localExtents.x, 0f, 0f);
            Vector3 axisY = transform.TransformVector(0f, localExtents.y, 0f);
            Vector3 axisZ = transform.TransformVector(0f, 0f, localExtents.z);
            Vector3 worldExtents = new(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
            return new Bounds(worldCenter, worldExtents * 2f);
        }

        private void SetStatus(string message, MessageType type)
        {
            _statusMessage = message;
            _statusType = type;
            Repaint();
        }

        private enum RepairMode
        {
            FaceOrientation,
            BoundaryFill
        }

        private enum BoundaryKind
        {
            Closed,
            OpenChain,
            Ambiguous
        }

        private sealed class FaceOrientationIssue
        {
            public int FaceIndex;
            public Face Face;
            public Vector3 Center;
            public Vector3 Normal;
            public Bounds LocalBounds;
            public bool Selected;
        }

        private sealed class BoundaryRegion
        {
            public int Id;
            public BoundaryKind Kind;
            public List<CanonicalEdge> Edges = new();
            public List<int> OrderedSharedIndices = new();
            public Bounds LocalBounds;
            public bool IsPlanar;
            public float PlanarityError;
            public Vector3 PlaneNormal;
            public bool Selected;
            public string Description = string.Empty;
        }

        private readonly struct SharedPoint
        {
            public SharedPoint(Vector3 position, int representativeRawIndex)
            {
                Position = position;
                RepresentativeRawIndex = representativeRawIndex;
            }

            public Vector3 Position { get; }
            public int RepresentativeRawIndex { get; }
        }

        private readonly struct BoundaryTriangle
        {
            public BoundaryTriangle(int a, int b, int c)
            {
                A = a;
                B = b;
                C = c;
            }

            public int A { get; }
            public int B { get; }
            public int C { get; }
        }

        private readonly struct FaceEdgeUse
        {
            public FaceEdgeUse(int faceIndex, int a, int b)
            {
                FaceIndex = faceIndex;
                A = a;
                B = b;
            }

            public int FaceIndex { get; }
            public int A { get; }
            public int B { get; }
        }

        private readonly struct FaceAdjacency
        {
            public FaceAdjacency(int faceIndex, bool requiresOppositeFlip)
            {
                FaceIndex = faceIndex;
                RequiresOppositeFlip = requiresOppositeFlip;
            }

            public int FaceIndex { get; }
            public bool RequiresOppositeFlip { get; }
        }

        private readonly struct CanonicalEdge : IEquatable<CanonicalEdge>
        {
            public CanonicalEdge(int first, int second)
            {
                A = Mathf.Min(first, second);
                B = Mathf.Max(first, second);
            }

            public int A { get; }
            public int B { get; }

            public bool Equals(CanonicalEdge other)
            {
                return A == other.A && B == other.B;
            }

            public override bool Equals(object obj)
            {
                return obj is CanonicalEdge other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(A, B);
            }
        }
    }
}
