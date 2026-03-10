using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Real‑time scene profiler that shows per-object stats.
/// Open from Tools > Realtime Scene Profiler.
/// Has a live overlay in the Scene View showing tri counts over each object.
/// </summary>
public class RealtimeSceneProfiler : EditorWindow
{
    private Vector2 _scroll;
    private List<ObjectStats> _stats = new List<ObjectStats>();
    private bool _scanned;
    private string _sortBy = "Tris";
    private bool _sortDesc = true;

    // real-time overlay
    private static bool _overlayEnabled;
    private static bool _autoRefresh;
    private static float _refreshInterval = 1f;
    private double _lastRefreshTime;

    // overlay display options
    private static bool _showLabels = true;
    private static bool _showWireframe = true;
    private static int _triWarning = 10000;
    private static int _triError = 50000;
    private static float _labelDistance = 30f;

    // scene totals
    private int _totalTris;
    private int _totalVerts;
    private int _totalObjects;
    private long _totalMemory;

    struct ObjectStats
    {
        public string Name;
        public int Tris;
        public int Verts;
        public int Materials;
        public long MemoryBytes;
        public string ShaderName;
        public bool IsStatic;
        public GameObject Go;
        public Bounds Bounds;
    }

    [MenuItem("Tools/Realtime Scene Profiler")]
    static void Open()
    {
        GetWindow<RealtimeSceneProfiler>("Scene Profiler");
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    void Update()
    {
        // auto-refresh on a timer
        if (_autoRefresh && _overlayEnabled && EditorApplication.timeSinceStartup - _lastRefreshTime > _refreshInterval)
        {
            _lastRefreshTime = EditorApplication.timeSinceStartup;
            ScanScene();
            SceneView.RepaintAll();
            Repaint();
        }
    }

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Scan Scene", EditorStyles.toolbarButton, GUILayout.Width(100)))
            ScanScene();
        if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            _stats.Clear();
            _scanned = false;
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // ── overlay controls ──
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Scene Overlay", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        _overlayEnabled = EditorGUILayout.Toggle("Show Overlay in Scene", _overlayEnabled);
        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();

        if (_overlayEnabled)
        {
            EditorGUI.indentLevel++;
            _autoRefresh = EditorGUILayout.Toggle("Auto Refresh", _autoRefresh);
            if (_autoRefresh)
                _refreshInterval = EditorGUILayout.Slider("Refresh Interval (s)", _refreshInterval, 0.2f, 5f);
            _showLabels = EditorGUILayout.Toggle("Show Labels", _showLabels);
            _showWireframe = EditorGUILayout.Toggle("Show Wireframe Tint", _showWireframe);
            _labelDistance = EditorGUILayout.Slider("Label Distance", _labelDistance, 5f, 200f);
            _triWarning = EditorGUILayout.IntField("Warning Threshold", _triWarning);
            _triError = EditorGUILayout.IntField("Error Threshold", _triError);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);

        if (!_scanned)
        {
            EditorGUILayout.HelpBox("Click 'Scan Scene' to collect stats from the current scene.", MessageType.Info);
            return;
        }

        // show totals
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Scene Totals", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Objects: {_totalObjects}   Tris: {_totalTris:N0}   Verts: {_totalVerts:N0}   Memory: {FormatBytes(_totalMemory)}");
        EditorGUILayout.Space(4);

        // sort buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Name", EditorStyles.miniButton)) SetSort("Name");
        if (GUILayout.Button("Tris", EditorStyles.miniButton)) SetSort("Tris");
        if (GUILayout.Button("Verts", EditorStyles.miniButton)) SetSort("Verts");
        if (GUILayout.Button("Mats", EditorStyles.miniButton)) SetSort("Mats");
        if (GUILayout.Button("Memory", EditorStyles.miniButton)) SetSort("Memory");
        EditorGUILayout.EndHorizontal();

        // object list
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (var s in _stats)
        {
            EditorGUILayout.BeginHorizontal(GUI.skin.box);

            // color code by tri count
            var oldColor = GUI.color;
            if (s.Tris > 50000) GUI.color = new Color(1f, 0.5f, 0.5f);
            else if (s.Tris > 10000) GUI.color = new Color(1f, 0.85f, 0.5f);

            if (GUILayout.Button(s.Name, EditorStyles.label, GUILayout.Width(200)))
            {
                // click to select
                Selection.activeGameObject = s.Go;
                EditorGUIUtility.PingObject(s.Go);
            }

            GUI.color = oldColor;

            EditorGUILayout.LabelField(s.Tris.ToString("N0"), GUILayout.Width(80));
            EditorGUILayout.LabelField(s.Verts.ToString("N0"), GUILayout.Width(80));
            EditorGUILayout.LabelField(s.Materials.ToString(), GUILayout.Width(40));
            EditorGUILayout.LabelField(FormatBytes(s.MemoryBytes), GUILayout.Width(80));
            EditorGUILayout.LabelField(s.ShaderName, GUILayout.Width(150));
            if (s.IsStatic) EditorGUILayout.LabelField("Static", GUILayout.Width(50));

            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    void ScanScene()
    {
        _stats.Clear();
        _totalTris = 0;
        _totalVerts = 0;
        _totalMemory = 0;

        var renderers = FindObjectsOfType<MeshRenderer>();
        foreach (var r in renderers)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            var mesh = mf.sharedMesh;
            var s = new ObjectStats
            {
                Name = r.gameObject.name,
                Tris = mesh.triangles.Length / 3,
                Verts = mesh.vertexCount,
                Materials = r.sharedMaterials.Length,
                MemoryBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh),
                ShaderName = r.sharedMaterial != null ? r.sharedMaterial.shader.name : "None",
                IsStatic = r.gameObject.isStatic,
                Go = r.gameObject,
                Bounds = r.bounds
            };

            _stats.Add(s);
            _totalTris += s.Tris;
            _totalVerts += s.Verts;
            _totalMemory += s.MemoryBytes;
        }

        // also check skinned mesh renderers
        var skinned = FindObjectsOfType<SkinnedMeshRenderer>();
        foreach (var smr in skinned)
        {
            if (smr.sharedMesh == null) continue;
            var mesh = smr.sharedMesh;
            var s = new ObjectStats
            {
                Name = smr.gameObject.name + " (Skinned)",
                Tris = mesh.triangles.Length / 3,
                Verts = mesh.vertexCount,
                Materials = smr.sharedMaterials.Length,
                MemoryBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh),
                ShaderName = smr.sharedMaterial != null ? smr.sharedMaterial.shader.name : "None",
                IsStatic = false,
                Go = smr.gameObject,
                Bounds = smr.bounds
            };

            _stats.Add(s);
            _totalTris += s.Tris;
            _totalVerts += s.Verts;
            _totalMemory += s.MemoryBytes;
        }

        _totalObjects = _stats.Count;
        SortStats();
        _scanned = true;
    }

    void SetSort(string column)
    {
        if (_sortBy == column) _sortDesc = !_sortDesc;
        else { _sortBy = column; _sortDesc = true; }
        SortStats();
    }

    void SortStats()
    {
        switch (_sortBy)
        {
            case "Name":
                _stats = _sortDesc ? _stats.OrderByDescending(s => s.Name).ToList() : _stats.OrderBy(s => s.Name).ToList();
                break;
            case "Tris":
                _stats = _sortDesc ? _stats.OrderByDescending(s => s.Tris).ToList() : _stats.OrderBy(s => s.Tris).ToList();
                break;
            case "Verts":
                _stats = _sortDesc ? _stats.OrderByDescending(s => s.Verts).ToList() : _stats.OrderBy(s => s.Verts).ToList();
                break;
            case "Mats":
                _stats = _sortDesc ? _stats.OrderByDescending(s => s.Materials).ToList() : _stats.OrderBy(s => s.Materials).ToList();
                break;
            case "Memory":
                _stats = _sortDesc ? _stats.OrderByDescending(s => s.MemoryBytes).ToList() : _stats.OrderBy(s => s.MemoryBytes).ToList();
                break;
        }
    }

    string FormatBytes(long bytes)
    {
        if (bytes < 1024) return bytes + " B";
        if (bytes < 1024 * 1024) return (bytes / 1024f).ToString("F1") + " KB";
        return (bytes / 1024f / 1024f).ToString("F2") + " MB";
    }

    // ── Scene View Overlay ──────────────────────────────────────────

    // in-view stats (computed each frame)
    private int _viewObjects;
    private int _viewTris;
    private int _viewVerts;
    private long _viewMemory;
    private int _viewMaterials;

    void OnSceneGUI(SceneView sceneView)
    {
        if (!_overlayEnabled || !_scanned || _stats.Count == 0) return;

        Camera cam = sceneView.camera;
        Vector3 camPos = cam.transform.position;
        GameObject selected = Selection.activeGameObject;

        // frustum planes for visibility check
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cam);

        // reset in-view counters
        _viewObjects = 0;
        _viewTris = 0;
        _viewVerts = 0;
        _viewMemory = 0;
        _viewMaterials = 0;

        // draw per-object overlays
        foreach (var s in _stats)
        {
            if (s.Go == null) continue;

            Color col = GetHeatColor(s.Tris);
            bool isSelected = (s.Go == selected);

            // frustum visibility — count stats for objects in view
            bool inFrustum = GeometryUtility.TestPlanesAABB(frustumPlanes, s.Bounds);
            if (inFrustum)
            {
                _viewObjects++;
                _viewTris += s.Tris;
                _viewVerts += s.Verts;
                _viewMemory += s.MemoryBytes;
                _viewMaterials += s.Materials;
            }

            // distance check — skip far-away objects to declutter (always show selected)
            float distToObj = Vector3.Distance(camPos, s.Bounds.center);
            if (!isSelected && distToObj > _labelDistance) continue;

            // wireframe tint: draw a colored bounding box
            if (_showWireframe)
            {
                Handles.color = isSelected
                    ? new Color(col.r, col.g, col.b, 0.9f)
                    : new Color(col.r, col.g, col.b, 0.4f);
                Handles.DrawWireCube(s.Bounds.center, s.Bounds.size);
            }

            // floating label above the object
            if (_showLabels)
            {
                Vector3 labelPos = s.Bounds.center + Vector3.up * s.Bounds.extents.y;
                float dist = distToObj;

                int fontSize = dist < 20f ? 11 : 9;

                var style = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = fontSize,
                    normal = { textColor = col },
                    alignment = TextAnchor.MiddleCenter
                };

                string label = $"{s.Name}\n{s.Tris:N0} tris | {s.Materials} mat";
                Handles.Label(labelPos, label, style);
            }
        }

        // draw HUD + in-view panel + selected detail panel
        Handles.BeginGUI();
        DrawSceneHUD(sceneView);
        DrawInViewHUD();
        if (selected != null)
            DrawSelectedDetail(sceneView, selected);
        Handles.EndGUI();
    }

    void DrawSceneHUD(SceneView sceneView)
    {
        float width = 220;
        float height = 90;
        float margin = 10;

        var rect = new Rect(margin, margin, width, height);
        GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

        GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, rect.height - 12));

        var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
        GUILayout.Label("Scene Profiler", headerStyle);

        var statStyle = new GUIStyle(EditorStyles.label) { fontSize = 10 };
        GUILayout.Label($"Objects: {_totalObjects}    Tris: {_totalTris:N0}", statStyle);
        GUILayout.Label($"Verts: {_totalVerts:N0}    Memory: {FormatBytes(_totalMemory)}", statStyle);

        if (_autoRefresh)
        {
            var liveStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.green } };
            GUILayout.Label($"● LIVE  (every {_refreshInterval:F1}s)", liveStyle);
        }

        GUILayout.EndArea();
    }

    void DrawInViewHUD()
    {
        float width = 220;
        float height = 80;
        float margin = 10;
        float topOffset = 100; // below the main HUD

        var rect = new Rect(margin, margin + topOffset, width, height);
        GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

        GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, rect.height - 12));

        var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11, normal = { textColor = new Color(0.5f, 0.85f, 1f) } };
        GUILayout.Label("\u25b6 In View", headerStyle);

        var statStyle = new GUIStyle(EditorStyles.label) { fontSize = 10 };
        GUILayout.Label($"Objects: {_viewObjects}    Tris: {_viewTris:N0}", statStyle);
        GUILayout.Label($"Verts: {_viewVerts:N0}    Memory: {FormatBytes(_viewMemory)}", statStyle);

        GUILayout.EndArea();
    }

    void DrawSelectedDetail(SceneView sceneView, GameObject selected)
    {
        // find this object in our stats
        ObjectStats? found = null;
        foreach (var s in _stats)
        {
            if (s.Go == selected) { found = s; break; }
        }

        if (!found.HasValue) return;
        var stat = found.Value;

        // gather extra info from the actual object
        var renderer = selected.GetComponent<Renderer>();
        var mf = selected.GetComponent<MeshFilter>();
        var smr = selected.GetComponent<SkinnedMeshRenderer>();
        Mesh mesh = mf != null ? mf.sharedMesh : (smr != null ? smr.sharedMesh : null);

        float panelW = 260;
        float panelH = 220;
        float margin = 10;
        var sceneRect = sceneView.position;
        float x = sceneRect.width - panelW - margin;
        float y = margin;

        var rect = new Rect(x, y, panelW, panelH);
        GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

        GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, rect.height - 12));

        // header
        Color col = GetHeatColor(stat.Tris);
        var nameStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, normal = { textColor = col } };
        GUILayout.Label(stat.Name, nameStyle);

        var infoStyle = new GUIStyle(EditorStyles.label) { fontSize = 10 };

        GUILayout.Label($"Triangles:  {stat.Tris:N0}", infoStyle);
        GUILayout.Label($"Vertices:   {stat.Verts:N0}", infoStyle);
        GUILayout.Label($"Materials:  {stat.Materials}", infoStyle);
        GUILayout.Label($"Memory:     {FormatBytes(stat.MemoryBytes)}", infoStyle);
        GUILayout.Label($"Shader:     {stat.ShaderName}", infoStyle);
        GUILayout.Label($"Static:     {(stat.IsStatic ? "Yes" : "No")}", infoStyle);

        if (mesh != null)
        {
            GUILayout.Label($"Submeshes:  {mesh.subMeshCount}", infoStyle);
            GUILayout.Label($"UV Sets:    {(mesh.uv != null && mesh.uv.Length > 0 ? "UV0" : "")}{(mesh.uv2 != null && mesh.uv2.Length > 0 ? " UV1" : "")}{(mesh.uv3 != null && mesh.uv3.Length > 0 ? " UV2" : "")}", infoStyle);
            GUILayout.Label($"Bounds:     {mesh.bounds.size.x:F1} x {mesh.bounds.size.y:F1} x {mesh.bounds.size.z:F1}", infoStyle);
        }

        if (renderer != null)
        {
            GUILayout.Label($"Cast Shadows: {renderer.shadowCastingMode}", infoStyle);
        }

        GUILayout.EndArea();
    }

    Color GetHeatColor(int tris)
    {
        if (tris >= _triError) return new Color(1f, 0.2f, 0.2f);     // red
        if (tris >= _triWarning) return new Color(1f, 0.75f, 0.2f);  // orange
        if (tris >= _triWarning / 2) return new Color(1f, 1f, 0.3f); // yellow
        return new Color(0.3f, 1f, 0.3f);                             // green
    }
}
