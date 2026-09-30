using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Lab-03 helpers: screenshots of the two blockout levels and a Scene-view fly-through (for screen recording).
public static class LabTools
{
    static readonly string[] Levels = { "Level1_Warehouse", "Level2_Docks" };
    static string ScenePath(string n) { return "Assets/Scenes/Lab03/" + n + ".unity"; }

    // ------------------------------------------------------------ screenshots
    struct Shot { public string name; public Vector3 pos, target; public float fov; public bool top; }
    static Shot S(string n, Vector3 p, Vector3 t, float fov = 60, bool top = false) { return new Shot { name = n, pos = p, target = t, fov = fov, top = top }; }

    static List<Shot> ShotsFor(string level)
    {
        if (level == "Level1_Warehouse") return new List<Shot> {
            S("1_overview", new Vector3(-55, 45, -60), Vector3.zero, 50),
            S("2_topdown", new Vector3(0, 100, 0), Vector3.zero, 45, true),
            S("3_spawn_view", new Vector3(0, 1.7f, -38), new Vector3(0, 2.5f, 0), 70),
            S("4_building_outside", new Vector3(-30, 5, -28), new Vector3(0, 3, 0), 60),
            S("5_ground_floor", new Vector3(-9, 1.7f, 2), new Vector3(9, 1.7f, -4), 75),
            S("6_upper_floor", new Vector3(-9, 4.8f, 4), new Vector3(9, 4.3f, -3), 75),
        };
        return new List<Shot> {
            S("1_overview", new Vector3(-55, 45, -60), Vector3.zero, 50),
            S("2_topdown", new Vector3(0, 100, 0), Vector3.zero, 45, true),
            S("3_spawn_view", new Vector3(0, 1.7f, -38), new Vector3(0, 3, 0), 70),
            S("4_gantry_front", new Vector3(-8, 12, -36), new Vector3(0, 4, 0), 55),
            S("5_gantry_side", new Vector3(-34, 4, -10), new Vector3(0, 4, 0), 60),
            S("6_deck_view", new Vector3(0, 7.7f, -4), new Vector3(0, 1, -30), 70),
        };
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

    // ------------------------------------------------------------ fly-through
    static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }
    static readonly Vector3[][] Paths = {
        // Warehouse: (position, look-at) pairs
        new[] { V(0,1.7f,-42), V(0,3,0),       V(0,1.7f,-31), V(0,3,0),       V(-14,1.7f,-24), V(-8,3,-2),    V(-28,2.5f,-12), V(-10,3,-2),
                V(-24,1.7f,-3), V(-8,1.7f,-2.5f), V(-10,1.7f,-2.5f), V(6,1.7f,-2.5f), V(-9,1.7f,-5), V(-8,3,-8.5f),
                V(-12,2f,-8.5f), V(-6,4.5f,-8.5f), V(-9,3.5f,-8.5f), V(-3,4.9f,-8.5f), V(-5,4.8f,-8.5f), V(6,4.6f,0),
                V(0,4.8f,-4), V(12,4.6f,6),    V(2,4.8f,3), V(-10,4.8f,8),    V(2,4.8f,3), V(14,4.5f,-6),
                V(-40,30,-45), V(0,3,0),       V(-52,40,-58), V(0,2,0) },
        // Docks
        new[] { V(0,1.7f,-42), V(0,3,0),       V(0,1.7f,-33), V(0,4,0),       V(-4,1.7f,-27), V(0,6,0),       V(0,1.7f,-21), V(0,7,0),
                V(0,2.5f,-17), V(0,7.5f,0),    V(0,4.5f,-13), V(0,8,0),       V(0,6.8f,-8), V(0,8,0),         V(0,7.7f,-4), V(0,6,10),
                V(0,7.7f,0), V(0,2,30),        V(0,7.7f,0), V(30,3,0),        V(0,7.7f,0), V(0,2,-30),        V(-30,16,-30), V(0,5,0),
                V(-52,40,-58), V(0,2,0) },
    };

    static int levelIdx, nPoints;
    static double t0, segLen = 3.0;
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
        t = t * t * (3 - 2 * t) * .4f + t * .6f; // gentle ease
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

    [MenuItem("Blockout/Lab/Fly Through Both Levels")]
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
        path = Paths[levelIdx]; nPoints = path.Length / 2;
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
