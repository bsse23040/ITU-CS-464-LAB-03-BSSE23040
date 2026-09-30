using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Lab 03: two PUBG-style Team Deathmatch blockouts built from the provided Synty POLYGON Prototype pack.
// Menu: Blockout/Lab/Build Synty Levels. Pieces are placed by their measured bounds, so pivots do not matter.
public static class SyntyLevels
{
    const string Pre = "Assets/Synty/PolygonPrototype/Prefabs/";
    const string Mats = "Assets/Synty/PolygonPrototype/Materials/";
    static Transform root, cur;
    static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();
    static readonly List<float[]> nogo = new List<float[]>();

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
    static Material Mat(int i) { return AssetDatabase.LoadAssetAtPath<Material>(Mats + "PolygonPrototype_Grid_" + i.ToString("00") + ".mat"); }
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
    static GameObject PutMin(string n, float minX, float baseY, float minZ, float yaw = 0, Vector3? scale = null, Material m = null)
    {
        var g = Inst(n, yaw, scale ?? Vector3.one); var b = WorldBounds(g);
        g.transform.position = new Vector3(minX - b.min.x, baseY - b.min.y, minZ - b.min.z);
        if (m != null) SetMat(g, m); return g;
    }
    // centre the piece's footprint on (cx,cz), bottom at baseY
    static GameObject PutC(string n, float cx, float baseY, float cz, float yaw = 0, Vector3? scale = null, Material m = null)
    {
        var g = Inst(n, yaw, scale ?? Vector3.one); var b = WorldBounds(g);
        g.transform.position = new Vector3(cx - b.center.x, baseY - b.min.y, cz - b.center.z);
        if (m != null) SetMat(g, m); return g;
    }
    // a scaled 1 m block, specified by its min corner and size
    static GameObject Blk(float x0, float y0, float z0, float sx, float sy, float sz, Material m, float yaw = 0)
    {
        return PutMin("SM_Buildings_Block_1x1_01", x0, y0, z0, yaw, new Vector3(sx, sy, sz), m);
    }
    static void Grp(string n) { var g = new GameObject(n); g.transform.SetParent(root, false); cur = g.transform; }
    static void No(float cx, float cz, float hx, float hz) { nogo.Add(new[] { cx, cz, hx, hz }); }

    // ---- modular 5 m walls: pattern letters W wall, D doorway, N window
    static string WallName(char c) { return c == 'D' ? "SM_Buildings_WallDoor_5x3_01" : c == 'N' ? "SM_Buildings_WallWindow_5x3_01" : "SM_Buildings_Wall_5x3_01"; }
    static void WallRowX(float x0, float z, float y, string pat, Material m = null) { for (int i = 0; i < pat.Length; i++) PutMin(WallName(pat[i]), x0 + 5 * i, y, z, 0, null, m); }
    static void WallRowZ(float x, float z0, float y, string pat, Material m = null) { for (int i = 0; i < pat.Length; i++) PutMin(WallName(pat[i]), x, y, z0 + 5 * i, 90, null, m); }

    // stairs: Stairs_1x3 is 1 m wide, 3 m tall, 6 m long. Rotate so it rises toward 'dir'.
    static void Flight(float minX, float baseY, float minZ, Vector2 dir, int width, float rise = 3f)
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
        {
            float ox = alongX ? 0 : i, oz = alongX ? i : 0;
            PutMin("SM_Buildings_Stairs_1x3_01", minX + ox, baseY, minZ + oz, yaw, new Vector3(1, rise / 3f, 1));
        }
    }

    // ---- scene scaffolding
    static void Begin(string name)
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        root = new GameObject(name).transform; nogo.Clear();
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude)) if (l.type == LightType.Directional) l.transform.rotation = Quaternion.Euler(50, -30, 0);
    }
    static void Arena()
    {
        Grp("Arena");
        Blk(-300, -1.6f, -300, 600, 1.5f, 600, Mat(2)); // wide ground so the horizon is not empty
        Blk(-48, -1, -48, 96, 1, 96, Mat(10));
        var dark = Mat(6);
        Blk(-49, 0, -49, 98, 5, 1, dark); Blk(-49, 0, 48, 98, 5, 1, dark); Blk(-49, 0, -49, 1, 5, 98, dark); Blk(48, 0, -49, 1, 5, 98, dark);
        Grp("Spawns");
        foreach (int s in new[] { -1, 1 })
        {
            var m = s < 0 ? Mat(4) : Mat(3); string nm = s < 0 ? "Red" : "Blue";
            for (int i = 0; i < 4; i++)
            {
                float x = -9 + i * 6, z = s * 40;
                Blk(x - 1.5f, 0, z - 1.5f, 3, .08f, 3, m).name = "SpawnPad_" + nm + "_" + (i + 1);
                var d = PutC("Character_Dummy_Male_01_FixedScale", x, 0, z, s < 0 ? 0 : 180); d.name = "Dummy_" + nm + "_" + (i + 1);
            }
        }
        Grp("Scenery");
        for (int i = 0; i < 10; i++)
        {
            float a = i * 36f * Mathf.Deg2Rad;
            var g = PutC("SM_Generic_Mountains_Soft_01", Mathf.Sin(a) * 125f, 0, Mathf.Cos(a) * 125f, i * 36f, new Vector3(3f, 3.5f, 3f), Mat(i % 2 == 0 ? 2 : 10));
            g.name = "Mountain_" + i;
        }
        var rnd = new System.Random(7); string[] trees = { "SM_Prop_Tree_Pine_01", "SM_Prop_Tree_Pine_02", "SM_Prop_Tree_Round_01", "SM_Prop_Tree_Palm_01" };
        for (int i = 0; i < 46; i++)
        {
            float x = (float)(rnd.NextDouble() * 2 - 1) * 66, z = (float)(rnd.NextDouble() * 2 - 1) * 66;
            if (Mathf.Abs(x) < 53 && Mathf.Abs(z) < 53) continue;
            PutC(trees[rnd.Next(trees.Length)], x, 0, z, rnd.Next(360), Vector3.one * (2.6f + (float)rnd.NextDouble()));
        }
        for (int i = 0; i < 6; i++) PutC("SM_Generic_Cloud_01", (i % 2 == 0 ? -1 : 1) * (78 + i * 4), 45 + (i % 3) * 6, -40 + i * 16, 20 * i, Vector3.one * 5f);
    }

    // cover pieces
    static Material[] contCols;
    static void Container(float x, float y0, float z, bool alongX, Material m)
    {
        if (alongX) Blk(x - 3, y0, z - 1.2f, 6, 2.6f, 2.4f, m); else Blk(x - 1.2f, y0, z - 3, 2.4f, 2.6f, 6, m);
    }
    static void ContP(float x, float z, bool alongX, int colA, bool stack)
    {
        Container(x, 0, z, alongX, Mat(colA)); Container(-x, 0, -z, alongX, Mat(colA));
        if (stack) Container(x, 2.6f, z, alongX, Mat(colA));
        float hx = alongX ? 3.2f : 1.6f, hz = alongX ? 1.6f : 3.2f; No(x, z, hx, hz); No(-x, -z, hx, hz);
    }
    static bool Blocked(float x, float z, float exX, float exZ)
    {
        if (Mathf.Abs(x) < exX && Mathf.Abs(z) < exZ) return true;
        if (Mathf.Abs(z) > 33 || Mathf.Abs(x) > 41) return true;
        foreach (var q in nogo) if (Mathf.Abs(x - q[0]) < q[2] + 2f && Mathf.Abs(z - q[1]) < q[3] + 2f) return true;
        return false;
    }
    static void Scatter(int seed, int n, float exX, float exZ)
    {
        Grp("Cover");
        var r = new System.Random(seed); int placed = 0, tries = 0;
        while (placed < n && tries < 900)
        {
            tries++;
            float x = (float)(r.NextDouble() * 2 - 1) * 40, z = (float)(r.NextDouble() * 2 - 1) * 30;
            if (Blocked(x, z, exX, exZ) || Blocked(-x, -z, exX, exZ)) continue;
            int k = r.Next(6); float yaw = r.Next(4) * 90;
            for (int s = 0; s < 2; s++)
            {
                float px = s == 0 ? x : -x, pz = s == 0 ? z : -z;
                switch (k)
                {
                    case 0: PutC("SM_Prop_Crate_01", px, 0, pz, yaw, Vector3.one * 1.2f); break;
                    case 1: PutC("SM_Prop_Crate_03", px, 0, pz, yaw, Vector3.one * 1.2f); break;
                    case 2: PutC("SM_Prop_Barrier_01", px, 0, pz, yaw, Vector3.one * 1.1f); break;
                    case 3: PutC("SM_Prop_Barrier_01", px, 0, pz, yaw + 90, Vector3.one * 1.1f); break;
                    case 4: PutC("SM_Prop_Pipe_03", px, 0, pz, yaw, Vector3.one); break;
                    default: PutC("SM_Prop_Barrel_01", px, 0, pz, 0, Vector3.one * 1.3f); break;
                }
            }
            No(x, z, 2.5f, 2.5f); No(-x, -z, 2.5f, 2.5f); placed++;
        }
    }

    // ---------------------------------------------------------------- Level 1
    static void Warehouse()
    {
        Begin("Level1_Warehouse"); Arena();
        float x0 = -12.5f, z0 = -10f, floorY = 3.1f;
        Grp("Warehouse_Ground");
        WallRowX(x0, z0, 0, "WWDWW"); WallRowX(x0, z0 + 20, 0, "WDWDW");
        WallRowZ(x0, z0, 0, "WDWW"); WallRowZ(x0 + 25, z0, 0, "WWDW");
        PutMin("SM_Buildings_Floor_5x5_01", x0, 0, z0, 0, new Vector3(5, 1, 4), Mat(8)); // interior floor
        foreach (var p in new[] { new Vector2(-5, 2), new Vector2(5, 2), new Vector2(-5, -4), new Vector2(5, -4) })
            PutC("SM_Buildings_Column_2x3_01", p.x, 0, p.y, 0, new Vector3(1, 1, 1));
        PutC("SM_Prop_Crate_02", 6, 0, 6, 20, Vector3.one * 1.4f); PutC("SM_Prop_Crate_01", 8, 0, 4.6f, 0, Vector3.one * 1.2f); PutC("SM_Prop_Barrel_01", -3, 0, 7, 0, Vector3.one * 1.3f);
        Grp("Warehouse_Stairs");
        Flight(x0, .1f, z0, new Vector2(1, 0), 3, 3f);
        Grp("Warehouse_Upper");
        // slab with a stair opening in the south-west corner
        PutMin("SM_Buildings_Floor_5x5_01", x0, floorY, z0 + 3, 0, new Vector3(5, 1, 17f / 5f), Mat(7));
        PutMin("SM_Buildings_Floor_5x5_01", x0 + 6, floorY, z0, 0, new Vector3(19f / 5f, 1, 3f / 5f), Mat(7));
        WallRowX(x0, z0, floorY, "NNNNN"); WallRowX(x0, z0 + 20, floorY, "NWNWN");
        WallRowZ(x0, z0, floorY, "NWNW"); WallRowZ(x0 + 25, z0, floorY, "WNNW");
        PutMin("SM_Buildings_Rail_5x1_01", x0, floorY, z0 + 3, 90); PutMin("SM_Buildings_Rail_1x1_01", x0 + 5, floorY, z0 + 3, 90);
        PutC("SM_Prop_Barrier_01", 4, floorY, 0, 90, Vector3.one * 1.1f); PutC("SM_Prop_Barrier_01", -4, floorY, 4, 0, Vector3.one * 1.1f);
        PutC("SM_Prop_Crate_03", 8, floorY, -5, 0, Vector3.one * 1.2f); PutC("SM_Prop_Crate_01", -8, floorY, 8, 30, Vector3.one * 1.2f);
        No(0, 0, 15, 12);
        Grp("Containers");
        ContP(-26, -12, true, 4, true); ContP(-26, 10, false, 2, false); ContP(-16, -28, true, 3, false); ContP(14, -24, false, 1, true);
        Scatter(11, 9, 16, 13);
        Save("Level1_Warehouse");
    }

    // ---------------------------------------------------------------- Level 2
    static void Docks()
    {
        Begin("Level2_Docks"); Arena();
        var dark = Mat(6);
        Grp("Gantry");
        foreach (var lx in new[] { -8f, 8f }) foreach (var lz in new[] { -4.5f, 4.5f }) Blk(lx - .75f, 0, lz - .75f, 1.5f, 6, 1.5f, dark);
        PutMin("SM_Buildings_Floor_5x5_01", -10, 6f, -6, 0, new Vector3(4, 1, 12f / 5f), Mat(7)); // deck, top at y=6
        Flight(-2, 0, -18, new Vector2(0, 1), 4, 3f); Flight(-2, 3, -12, new Vector2(0, 1), 4, 3f);
        Flight(-2, 0, 12, new Vector2(0, -1), 4, 3f); Flight(-2, 3, 6, new Vector2(0, -1), 4, 3f);
        // deck railings, open where the stairs arrive
        PutMin("SM_Buildings_Rail_5x1_01", -10, 6, -6, 90); PutMin("SM_Buildings_Rail_5x1_01", -10, 6, 5.5f, 90);
        PutMin("SM_Buildings_Rail_5x1_01", 5, 6, -6, 90); PutMin("SM_Buildings_Rail_5x1_01", 5, 6, 5.5f, 90);
        PutMin("SM_Buildings_Rail_5x1_01", -10, 6, -6, 0); PutMin("SM_Buildings_Rail_5x1_01", -10, 6, 0, 0);
        PutMin("SM_Buildings_Rail_5x1_01", 9.5f, 6, -6, 0); PutMin("SM_Buildings_Rail_5x1_01", 9.5f, 6, 0, 0);
        PutC("SM_Prop_Barrier_01", -5, 6, 0, 90, Vector3.one * 1.1f); PutC("SM_Prop_Barrier_01", 5, 6, 0, 90, Vector3.one * 1.1f);
        PutC("SM_Prop_Crate_01", 0, 6, 2.5f, 0, Vector3.one * 1.2f);
        Grp("ControlHut");
        WallRowX(-5, -2.5f, 0, "WW"); WallRowX(-5, 2.5f, 0, "WD"); WallRowZ(-5, -2.5f, 0, "W"); WallRowZ(5, -2.5f, 0, "N");
        PutMin("SM_Buildings_Floor_5x5_01", -5, 3.1f, -2.5f, 0, new Vector3(2, 1, 1), Mat(7));
        PutMin("SM_Buildings_Floor_5x5_01", -5, 0, -2.5f, 0, new Vector3(2, 1, 1), Mat(8));
        No(0, 0, 14, 22);
        Grp("Containers");
        foreach (var x in new[] { -27f, -15f, 3f, 15f, 27f }) ContP(x, -31, true, x < 0 ? 4 : 3, x == 3f || x == -27f);
        foreach (var x in new[] { -21f, -9f, 9f, 21f }) ContP(x, -23, true, x < 0 ? 2 : 1, x == 9f);
        Scatter(55, 8, 24, 10);
        Grp("Cranes_and_Pipes");
        PutC("SM_Prop_Pipe_04", -34, 0, 0, 90, Vector3.one); PutC("SM_Prop_Pipe_04", 34, 0, 0, 90, Vector3.one);
        PutC("SM_Prop_Pipe_04", -34, 1.2f, 0, 90, Vector3.one); PutC("SM_Prop_Pipe_04", 34, 1.2f, 0, 90, Vector3.one);
        Save("Level2_Docks");
    }

    static void Save(string name)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes/Lab03")) AssetDatabase.CreateFolder("Assets/Scenes", "Lab03");
        var cam = Camera.main; if (cam != null) { cam.transform.position = new Vector3(0, 6, -55); cam.transform.rotation = Quaternion.Euler(15, 0, 0); }
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/Lab03/" + name + ".unity");
        Debug.Log("[SyntyLevels] saved " + name + " (" + root.GetComponentsInChildren<Transform>().Length + " objects)");
    }

    [MenuItem("Blockout/Lab/Build Synty Levels")]
    public static void BuildAll()
    {
        Warehouse(); Docks();
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene("Assets/Scenes/Lab03/Level1_Warehouse.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Lab03/Level2_Docks.unity", true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[SyntyLevels] done");
    }
}
