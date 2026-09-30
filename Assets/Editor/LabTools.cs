using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Lab-03 helpers: screenshots of the two grey-box level and a Scene-view fly-through that follows each level's default route.
public static class LabTools
{
    static readonly string[] Levels = { "Level_Warehouse" };
    static string ScenePath(string n) { return "Assets/Scenes/Lab03/" + n + ".unity"; }
    static readonly Color Background = new Color(.72f, .72f, .72f);

    // ------------------------------------------------------------ screenshots
    struct Shot { public string name; public Vector3 pos, target; public float fov; public bool top; }
    static Shot S(string n, Vector3 p, Vector3 t, float fov = 60, bool top = false) { return new Shot { name = n, pos = p, target = t, fov = fov, top = top }; }
    static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }

    static List<Shot> ShotsFor(string level)
    {
        if (level == "Level_Warehouse") return new List<Shot> {
            S("1_overview", V(-48, 42, -62), V(0, 0, -8), 50),
            S("2_topdown_route", V(0, 95, -2), V(0, 0, 0), 45, true),
            S("3_spawn_view", V(-7.5f, 1.7f, -35), V(-7.5f, 1.5f, -20), 70),
            S("4_the_yard", V(-1, 1.7f, -19), V(-5, 1.5f, -8), 70),
            S("5_doorway_and_stairs", V(-7.5f, 1.7f, -12), V(-7.5f, 2.6f, -2), 70),
            S("6_upper_floor_goal", V(-7.5f, 4.8f, 0), V(7.5f, 4f, 3), 75),
        };
        return new List<Shot>();
    }

    static void Render(Camera cam, string file)
    {
        const int W = 1600, H = 900;
        var rt = new RenderTexture(W, H, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
    }

    [MenuItem("Blockout/Lab/Capture Screenshots")]
    public static void CaptureShots()
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/screenshots"));
        Directory.CreateDirectory(dir);
        foreach (var level in Levels)
        {
            EditorSceneManager.OpenScene(ScenePath(level), OpenSceneMode.Single);
            var go = new GameObject("LabCam");
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.2f; cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Background;
            bool first = true;
            foreach (var s in ShotsFor(level))
            {
                cam.fieldOfView = s.fov;
                go.transform.position = s.pos;
                if (s.top) go.transform.rotation = Quaternion.Euler(90, 0, 0); else go.transform.LookAt(s.target);
                if (first) { Render(cam, Path.Combine(dir, "_warmup.png")); first = false; }
                Render(cam, Path.Combine(dir, level + "_" + s.name + ".png"));
            }
            File.Delete(Path.Combine(dir, "_warmup.png"));
            Object.DestroyImmediate(go);
            Debug.Log("[LabTools] screenshots done for " + level);
        }
    }

    // ------------------------------------------------------------ fly-through along the default route
    // path = (position, look-at) pairs
    static Vector3[] BuildPath(int idx)
    {
        var r = SimpleLevels.Route();
        var pts = new List<Vector3>();                       // densify so the smoothed camera stays on the route
        for (int i = 0; i < r.Length - 1; i++)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(r[i], r[i + 1]) / 5f));
            for (int k = 0; k < n; k++) pts.Add(Vector3.Lerp(r[i], r[i + 1], (float)k / n));
        }
        pts.Add(r[r.Length - 1]);
        var up = Vector3.up;
        var list = new List<Vector3>();
        Vector3 centre = V(0, 0, -8);
        list.Add(V(-48, 42, -62)); list.Add(centre);         // start with the overview
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 look;
            if (i < pts.Count - 1) look = pts[Mathf.Min(i + 2, pts.Count - 1)] + up * 1.4f;
            else look = pts[i] + (pts[i] - pts[i - 1]).normalized * 4f + up * 1.2f;
            list.Add(pts[i] + up * 1.7f); list.Add(look);
        }
        var goal = pts[pts.Count - 1];
        list.Add(goal + V(-6, 8, -10)); list.Add(goal);      // pull back to look at the goal
        list.Add(V(-48, 42, -62)); list.Add(centre);         // and finish on the overview
        return list.ToArray();
    }

    static int levelIdx, nPoints;
    static double t0, segLen = 1.7;
    static Vector3[] path;
    static bool running;

    static Vector3 CR(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        return .5f * ((2 * b) + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (-a + 3 * b - 3 * c + d) * t * t * t);
    }
    static Vector3 Sample(bool look, float u)
    {
        int n = path.Length / 2;
        int i = Mathf.Clamp(Mathf.FloorToInt(u), 0, n - 2);
        float t = u - i;
        int o = look ? 1 : 0;
        Vector3 P(int k) { k = Mathf.Clamp(k, 0, n - 1); return path[k * 2 + o]; }
        return CR(P(i - 1), P(i), P(i + 1), P(i + 2), t);
    }

    // Lets an outside script start the fly-through by creating Temp/fly_now (used while screen recording).
    [InitializeOnLoadMethod]
    static void WatchTrigger()
    {
        EditorApplication.update += () =>
        {
            if (!running && File.Exists("Temp/fly_now")) { File.Delete("Temp/fly_now"); FlyThrough(); }
        };
    }

    [MenuItem("Blockout/Lab/Fly Through the Level")]
    public static void FlyThrough()
    {
        levelIdx = 0; running = true;
        StartLevel();
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.maximized = true;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Debug.Log("[LabTools] FLY_START");
    }

    static void StartLevel()
    {
        EditorSceneManager.OpenScene(ScenePath(Levels[levelIdx]), OpenSceneMode.Single);
        path = BuildPath(levelIdx); nPoints = path.Length / 2;
        t0 = EditorApplication.timeSinceStartup + 1.5; // hold on the first frame briefly
        var sv = SceneView.lastActiveSceneView;
        if (sv != null) { sv.in2DMode = false; sv.orthographic = false; sv.Focus(); }
    }

    static void Tick()
    {
        if (!running) return;
        var sv = SceneView.lastActiveSceneView;
        if (sv == null) return;
        double el = EditorApplication.timeSinceStartup - t0;
        float u = Mathf.Max(0f, (float)(el / segLen));
        if (u >= nPoints - 1)
        {
            if (levelIdx + 1 < Levels.Length) { levelIdx++; StartLevel(); Debug.Log("[LabTools] FLY_NEXT"); return; }
            running = false; EditorApplication.update -= Tick; sv.maximized = false; Debug.Log("[LabTools] FLY_END"); return;
        }
        Vector3 pos = Sample(false, u), look = Sample(true, u);
        var rot = Quaternion.LookRotation((look - pos).normalized, Vector3.up);
        float d = 1.5f, fov = sv.cameraSettings.fieldOfView;
        sv.LookAt(pos + rot * Vector3.forward * d, rot, d * Mathf.Tan(fov * .5f * Mathf.Deg2Rad), false, true);
        sv.Repaint();
    }
}
