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
    /// 扫描 ProBuilder 网格的拓扑边界，并在用户确认后封口或桥接两圈边界。
    /// </summary>
    internal sealed class ProBuilderMeshRepairWindow : EditorWindow
    {
        private const string MenuPath = "EndLink/Level/ProBuilder Mesh Repair";
        private const float DefaultPlanarityTolerance = 0.01f;

        private static readonly Color ClosedColor = new(0.15f, 0.85f, 1f, 1f);
        private static readonly Color OpenColor = new(1f, 0.58f, 0.12f, 1f);
        private static readonly Color AmbiguousColor = new(1f, 0.2f, 0.2f, 1f);
        private static readonly Color SelectedColor = new(1f, 0.9f, 0.15f, 1f);
        private static readonly Color NonManifoldColor = new(0.95f, 0.2f, 1f, 1f);

        private readonly List<BoundaryRegion> _regions = new();
        private readonly List<CanonicalEdge> _nonManifoldEdges = new();
        private readonly List<SharedPoint> _sharedPoints = new();

        private ProBuilderMesh _target;
        private Vector2 _scrollPosition;
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
            DrawSummary();
            DrawRegionList();
            DrawRepairControls();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("ProBuilder 网格修补", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "扫描选中 ProBuilder 网格的开放边界。请只勾选确实需要修补的区域；门洞、窗口等有意开口不会被自动区分。",
                MessageType.Info);
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

            float nextTolerance = EditorGUILayout.FloatField("平面误差容许值", _planarityTolerance);
            nextTolerance = Mathf.Clamp(nextTolerance, 0.0001f, 0.5f);
            if (!Mathf.Approximately(nextTolerance, _planarityTolerance))
            {
                _planarityTolerance = nextTolerance;
                EvaluateRegionPlanarity();
                SceneView.RepaintAll();
            }
        }

        private void DrawSummary()
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

                if (GUILayout.Button("选择可封口项", GUILayout.Width(100f)))
                {
                    foreach (BoundaryRegion region in _regions)
                    {
                        region.Selected = region.Kind == BoundaryKind.Closed && region.IsPlanar;
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

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
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
                        ? $"近平面，误差 {region.PlanarityError:F4}"
                        : $"非平面，误差 {region.PlanarityError:F4}";
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

        private void DrawRepairControls()
        {
            EditorGUILayout.Space(4f);
            _flipNewFaceWinding = EditorGUILayout.Toggle(
                new GUIContent(
                    "反转新面方向",
                    "新面的正反面不符合预期时，撤销后勾选此项重新修补。"),
                _flipNewFaceWinding);

            List<BoundaryRegion> selected = _regions.Where(region => region.Selected).ToList();
            bool canFill = selected.Count > 0
                && selected.All(region => region.Kind == BoundaryKind.Closed && region.IsPlanar);
            bool canBridge = selected.Count == 2
                && selected.All(region => region.Kind == BoundaryKind.Closed)
                && selected[0].OrderedSharedIndices.Count == selected[1].OrderedSharedIndices.Count;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!canFill))
                {
                    if (GUILayout.Button("封闭所选平面开口", GUILayout.Height(28f)))
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
                    "当前选择不满足安全修补条件。开放边链和歧义边界需要先手工整理拓扑。",
                    EditorStyles.wordWrappedMiniLabel);
            }
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

            if (_target == null)
            {
                SetStatus("请选择一个带 ProBuilderMesh 的物体。", MessageType.Info);
                SceneView.RepaintAll();
                return;
            }

            IList<Vector3> positions = _target.positions;
            IList<SharedVertex> sharedVertices = _target.sharedVertices;
            if (positions == null || positions.Count == 0 || sharedVertices == null)
            {
                SetStatus("目标没有可扫描的 ProBuilder 顶点数据。", MessageType.Warning);
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
                    $"扫描完成：检测到 {_regions.Count} 个边界区域、{_nonManifoldEdges.Count} 条非流形边。",
                    _nonManifoldEdges.Count > 0 ? MessageType.Warning : MessageType.Info);
            }

            SceneView.RepaintAll();
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
            });
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

        private void DrawScenePreview(SceneView sceneView)
        {
            if (!_previewInScene || _target == null || _regions.Count == 0)
            {
                return;
            }

            Matrix4x4 previousMatrix = Handles.matrix;
            Color previousColor = Handles.color;
            CompareFunction previousZTest = Handles.zTest;
            Handles.matrix = _target.transform.localToWorldMatrix;
            Handles.zTest = CompareFunction.LessEqual;

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

                if (region.Selected && region.Kind == BoundaryKind.Closed && region.IsPlanar)
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

            Handles.matrix = previousMatrix;
            Handles.color = previousColor;
            Handles.zTest = previousZTest;
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

        private enum BoundaryKind
        {
            Closed,
            OpenChain,
            Ambiguous
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
