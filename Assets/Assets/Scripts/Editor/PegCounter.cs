#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PegCounter
{
    const string DefaultCsvPath = "Assets/PegReports/peg_counts.csv";

    [MenuItem("Tools/Ponggle/Count Pegs in OPEN Scene")]
    public static void CountOpenScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded) { EditorUtility.DisplayDialog("PegCounter", "Tidak ada scene terbuka.", "OK"); return; }

        var rows = new List<string> { "Scene,Blue,Orange,Green,Rainbow,Element,Total" };
        var cnt = CountInScene(scene);
        rows.Add(Row(scene.name, cnt));
        ShowSummary(rows, scene.name);
    }

    [MenuItem("Tools/Ponggle/Count Pegs across ALL Build Scenes")]
    public static void CountAllBuildScenes()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DefaultCsvPath)!);

        var rows = new List<string> { "Scene,Blue,Orange,Green,Rainbow,Element,Total" };

        // Simpan scene aktif agar bisa balik lagi
        string currentPath = SceneManager.GetActiveScene().path;
        bool currentDirty = SceneManager.GetActiveScene().isDirty;

        foreach (var s in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            var opened = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
            var cnt = CountInScene(opened);
            rows.Add(Row(opened.name, cnt));
        }

        File.WriteAllLines(DefaultCsvPath, rows);
        AssetDatabase.Refresh();

        if (!string.IsNullOrEmpty(currentPath))
            EditorSceneManager.OpenScene(currentPath, OpenSceneMode.Single);
        if (currentDirty) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("PegCounter", $"CSV tersimpan:\n{DefaultCsvPath}", "Buka");
        EditorUtility.RevealInFinder(DefaultCsvPath);
    }

    // ----- Core -----
    struct Counts { public int blue, orange, green, rainbow, element; public int Total => blue + orange + green + rainbow + element; }

    static Counts CountInScene(Scene scene)
    {
        var counts = new Counts();

        // Ambil SEMUA PegController di scene (aktif/nonaktif)
        var pegs = GameObject.FindObjectsOfType<PegController>(true);
        foreach (var p in pegs)
        {
            switch (p.Type) // langsung dari PegController.Type
            {
                case PegType.Blue: counts.blue++; break;
                case PegType.Orange: counts.orange++; break;
                case PegType.Green: counts.green++; break;
                case PegType.Rainbow: counts.rainbow++; break;
                case PegType.Element: counts.element++; break;
            }
        }

        // OPTIONAL: kalau kamu punya jenis “brick peg” tapi TIDAK pakai PegController,
        // dan ada script lain yang menyimpan warna dalam field bernama "pegType" atau "Type"
        // bertipe enum PegType, kode di bawah akan ikut menghitungnya via reflection aman.
        var allRoots = scene.GetRootGameObjects();
        foreach (var go in allRoots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject))
        {
            if (go.GetComponent<PegController>()) continue; // sudah dihitung di atas

            // cari komponen yang punya field "pegType" / "Type" bertipe PegType
            foreach (var mb in go.GetComponents<MonoBehaviour>())
            {
                if (!mb) continue;
                var t = mb.GetType();

                var f = t.GetField("pegType", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                     ?? t.GetField("Type", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f == null) continue;
                if (f.FieldType != typeof(PegType)) continue;

                var val = (PegType)f.GetValue(mb);
                switch (val)
                {
                    case PegType.Blue: counts.blue++; break;
                    case PegType.Orange: counts.orange++; break;
                    case PegType.Green: counts.green++; break;
                    case PegType.Rainbow: counts.rainbow++; break;
                    case PegType.Element: counts.element++; break;
                }
                break; // satu komponen cukup
            }
        }

        return counts;
    }

    static string Row(string sceneName, Counts c)
        => $"{sceneName},{c.blue},{c.orange},{c.green},{c.rainbow},{c.element},{c.Total}";

    static void ShowSummary(List<string> rows, string title)
    {
        var csv = string.Join("\n", rows);
        Debug.Log($"[PegCounter] {title}\n{csv}");
        EditorUtility.DisplayDialog("PegCounter (OPEN Scene)", csv.Replace(",", " | "), "OK");
    }
}
#endif
