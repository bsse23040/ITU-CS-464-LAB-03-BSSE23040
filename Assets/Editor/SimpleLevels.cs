using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Lab 03: a simple grey-box level built from the provided Synty POLYGON Prototype pieces.
// No scenery and no colour: every piece gets one of four greys. The level has one marked default route
// (a light strip on the floor) from the spawn to the goal. Menu: Blockout/Lab/Build Level.
public static class SimpleLevels
{
    static Transform root, cur;
    static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();
    static Material ground, wallM, coverM, light;

    // ------------------------------------------------------------ the default routes (feet positions)
    public static Vector3[] Route()
    {
        return new[] {
            new Vector3(-7.5f, 0, -33), new Vector3(-7.5f, 0, -20), new Vector3(-1, 0, -20), new Vector3(-1, 0, -11),
            new Vector3(-7.5f, 0, -11), new Vector3(-7.5f, 0, -7.4f),               // to the doorway and stair foot
            new Vector3(-7.5f, 3.1f, -1.4f), new Vector3(-7.5f, 3.1f, 3), new Vector3(7.5f, 3.1f, 3) };
    }

    // ------------------------------------------------------------ helpers
    static GameObject Load(string n)
    {
        GameObject g;
        if (!cache.TryGetValue(n, out g))
        {
            foreach (var guid in AssetDatabase.FindAssets(n + " t:Prefab", new[] { "Assets/Synty/PolygonPrototype/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == n) { g = AssetDatabase.LoadAssetAtPath<GameObject>(path); break; }
            }
            if (g == null) throw new System.Exception("Missing Synty prefab " + n);
            cache[n] = g;
        }
        return g;
    }
    static Material Grey(string name, float v)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        string p = "Assets/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
        m.SetColor("_BaseColor", new Color(v, v, v)); m.SetFloat("_Smoothness", 0f); m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m); return m;
    }
    static Bounds WorldBounds(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
    }
    static void SetMat(GameObject g, Material m)
    {
        foreach (var r in g.GetComponentsInChildren<Renderer>())
        {
            var a = new Material[r.sharedMaterials.Length]; for (int i = 0; i < a.Length; i++) a[i] = m; r.sharedMaterials = a;
        }
    }
    static GameObject Inst(string n, float yaw, Vector3 scale)
    {
        var g = (GameObject)PrefabUtility.InstantiatePrefab(Load(n));
        g.transform.SetParent(cur, false);
        g.transform.localScale = scale; g.transform.rotation = Quaternion.Euler(0, yaw, 0); g.transform.position = Vector3.zero;
        return g;
    }
    // align the piece's bounding-box minimum corner (x,z) and bottom (y)
    static GameObject PutMin(string n, float minX, float baseY, float minZ, Material m, float yaw = 0, Vector3? scale = null)
    {
        var g = Inst(n, yaw, scale ?? Vector3.one); var b = WorldBounds(g);
        g.transform.position = new Vector3(minX - b.min.x, baseY - b.min.y, minZ - b.min.z);
        SetMat(g, m); return g;
    }
    static GameObject PutC(string n, float cx, float baseY, float cz, Material m, float yaw = 0, Vector3? scale = null)
    {
        var g = Inst(n, yaw, scale ?? Vector3.one); var b = WorldBounds(g);
        g.transform.position = new Vector3(cx - b.center.x, baseY - b.min.y, cz - b.center.z);
        SetMat(g, m); return g;
    }
    static GameObject Blk(float x0, float y0, float z0, float sx, float sy, float sz, Material m)
    {
        return PutMin("SM_Buildings_Block_1x1_01", x0, y0, z0, m, 0, new Vector3(sx, sy, sz));
    }
    static void Grp(string n) { var g = new GameObject(n); g.transform.SetParent(root, false); cur = g.transform; }

    static string WallName(char c) { return c == 'D' ? "SM_Buildings_WallDoor_5x3_01" : c == 'N' ? "SM_Buildings_WallWindow_5x3_01" : "SM_Buildings_Wall_5x3_01"; }
    static void WallRowX(float x0, float z, float y, string pat) { for (int i = 0; i < pat.Length; i++) PutMin(WallName(pat[i]), x0 + 5 * i, y, z, wallM, 0); }
    static void WallRowZ(float x, float z0, float y, string pat) { for (int i = 0; i < pat.Length; i++) PutMin(WallName(pat[i]), x, y, z0 + 5 * i, wallM, 90); }

    // Stairs_1x3 is 1 m wide, 3 m tall and 6 m long. Rotate it so it rises toward 'dir'; 'rise' scales the height.
    static void Flight(float minX, float baseY, float minZ, Vector2 dir, int width, float rise)
    {
        var probe = Inst("SM_Buildings_Stairs_1x3_01", 0, Vector3.one);
        var b = WorldBounds(probe); var top = Vector2.zero; int n = 0;
        foreach (var mf in probe.GetComponentsInChildren<MeshFilter>())
            foreach (var v in mf.sharedMesh.vertices) { var w = mf.transform.TransformPoint(v); if (w.y > b.min.y + b.size.y * .9f) { top += new Vector2(w.x, w.z); n++; } }
        top = top / Mathf.Max(1, n) - new Vector2(b.center.x, b.center.z);
        Object.DestroyImmediate(probe);
        float a1 = Mathf.Atan2(top.y, top.x) * Mathf.Rad2Deg, a2 = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float yaw = Mathf.Round((a1 - a2) / 90f) * 90f;
        bool alongX = Mathf.Abs(dir.x) > .5f;
        for (int i = 0; i < width; i++)
            PutMin("SM_Buildings_Stairs_1x3_01", minX + (alongX ? 0 : i), baseY, minZ + (alongX ? i : 0), wallM, yaw, new Vector3(1, rise / 3f, 1));
    }

    // the light strip that marks the default route on flat ground
    static void MarkRoute()
    {
        Grp("DefaultRoute");
        var r = Route();
        for (int i = 0; i < r.Length - 1; i++)
        {
            var a = r[i]; var b = r[i + 1];
            if (Mathf.Abs(a.y - b.y) > .01f) continue;                      // stairs are not marked
            float x0 = Mathf.Min(a.x, b.x) - .4f, x1 = Mathf.Max(a.x, b.x) + .4f, z0 = Mathf.Min(a.z, b.z) - .4f, z1 = Mathf.Max(a.z, b.z) + .4f;
            Blk(x0, a.y, z0, x1 - x0, .05f, z1 - z0, light).name = "Route_" + (i + 1);
        }
    }
    static void Spawn(Vector3 p) { Grp("Spawn"); Blk(p.x - 1.5f, p.y, p.z - 1.5f, 3, .1f, 3, light).name = "Spawn"; }
    static void Goal(Vector3 p)
    {
        Grp("Goal");
        Blk(p.x - 1.5f, p.y, p.z - 1.5f, 3, .1f, 3, light).name = "GoalPad";
        PutC("SM_Prop_FlagPole_01", p.x, p.y + .1f, p.z, light, 0, Vector3.one * 1.8f).name = "GoalFlag";
    }

    static void Begin(string name)
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        root = new GameObject(name).transform;
        ground = Grey("Grey_Ground", .22f); wallM = Grey("Grey_Wall", .62f); coverM = Grey("Grey_Cover", .42f); light = Grey("Grey_Light", .95f);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.55f, .55f, .55f);
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude)) if (l.type == LightType.Directional) { l.transform.rotation = Quaternion.Euler(50, -30, 0); l.color = Color.white; }
        Grp("Ground"); Blk(-30, -1, -37, 60, 1, 74, ground);
    }
    static void Save(string name, Vector3 spawn)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes/Lab03")) AssetDatabase.CreateFolder("Assets/Scenes", "Lab03");
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.72f, .72f, .72f);
            cam.transform.position = spawn + new Vector3(0, 3, -6); cam.transform.rotation = Quaternion.Euler(12, 0, 0);
        }
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/Lab03/" + name + ".unity");
        Debug.Log("[SimpleLevels] saved " + name + " (" + root.GetComponentsInChildren<Transform>().Length + " objects)");
    }

    // ------------------------------------------------------------ the level
    static void Warehouse()
    {
        Begin("Level_Warehouse");
        var r = Route();
        Spawn(r[0]);
        Grp("Warehouse");
        float x0 = -10, z0 = -7.5f;
        WallRowX(x0, z0, 0, "DWWW"); WallRowX(x0, z0 + 15, 0, "WWWW");        // doorway in the south wall, above the stairs
        WallRowZ(x0, z0, 0, "WWW"); WallRowZ(x0 + 20, z0, 0, "WWW");
        Flight(-9, 0, -7.4f, new Vector2(0, 1), 3, 3.1f);                      // 3 m wide, climbs 3.1 m over 6 m
        PutMin("SM_Buildings_Floor_5x5_01", x0, 3f, -1.4f, wallM, 0, new Vector3(4, 1, 8.9f / 5f)); // upper floor, top at 3.1
        WallRowX(x0, z0, 3.1f, "WWWW"); WallRowX(x0, z0 + 15, 3.1f, "NNNN");
        WallRowZ(x0, z0, 3.1f, "WWW"); WallRowZ(x0 + 20, z0, 3.1f, "WWW");
        PutMin("SM_Buildings_Rail_5x1_01", -6, 3.1f, -1.4f, wallM, 90); PutMin("SM_Buildings_Rail_5x1_01", -1, 3.1f, -1.4f, wallM, 90);
        PutMin("SM_Buildings_Rail_5x1_01", 4, 3.1f, -1.4f, wallM, 90); PutMin("SM_Buildings_Rail_1x1_01", 9, 3.1f, -1.4f, wallM, 90);
        PutC("SM_Buildings_Column_2x3_01", 3, 0, -3, wallM); PutC("SM_Buildings_Column_2x3_01", 3, 0, 3, wallM);
        Grp("Yard");
        Blk(-10.5f, 0, -16.2f, 6, 2.6f, 2.4f, coverM).name = "Container_Block";   // makes the route turn east
        Blk(5, 0, -27, 2.4f, 2.6f, 6, coverM).name = "Container_Side";
        PutC("SM_Prop_Barrier_01", 5, 0, -14, coverM, 90, Vector3.one * 1.1f);
        PutC("SM_Prop_Barrier_01", -14, 0, -24, coverM, 0, Vector3.one * 1.1f);
        PutC("SM_Prop_Crate_01", -13, 0, -12, coverM, 0, Vector3.one * 1.2f);
        PutC("SM_Prop_Crate_03", 3, 0, -8, coverM, 0, Vector3.one * 1.2f);
        MarkRoute();
        Goal(r[r.Length - 1]);
        Save("Level_Warehouse", r[0]);
    }

    [MenuItem("Blockout/Lab/Build Level")]
    public static void BuildAll()
    {
        Warehouse();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Lab03/Level_Warehouse.unity", true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[SimpleLevels] done");
    }
}
