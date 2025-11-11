// Assets/Editor/PegShadowGenerator.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEditorInternal;

// NOTE: KeepShadowFollow & OffsetSpace adalah RUNTIME script (di luar Editor/)

public static class PegShadowGenerator
{
    // ====== Parameters shown in the window ======
    public static string ShadowChildName = "__Shadow";
    public static Color ShadowColor = new(0f, 0f, 0f, 0.45f);
    public static float ShadowScale = 1.10f;
    public static Vector2 ShadowOffset = new(-0.03f, -0.06f);
    public static int SortingOrderOffset = -1;

    public static bool RotateWithPeg = true;
    public static OffsetSpace OffsetSpaceMode = OffsetSpace.World;

    // Filters
    public static bool RestrictToParent = true;
    public static Transform ParentRoot = null;

    public static bool DetectByTag = true;
    public static string TagName = "Peg";

    public static bool DetectByComponent = true; // PegController presence

    public static bool DetectByName = true;
    public static string NameKeywordsCsv = "peg,brick,ring,circle";

    public static bool DetectByLayer = false;
    public static LayerMask AllowedLayers = ~0;

    [MenuItem("Tools/Ponggle/Peg Shadow Generator…")]
    public static void OpenWindow() => PegShadowGeneratorWindow.Open();

    [MenuItem("Tools/Ponggle/Generate Peg Shadows (Scene)")]
    public static void GenerateAllMenu() => GenerateAll(false);

    [MenuItem("Tools/Ponggle/Update Existing Shadows (Scene)")]
    public static void UpdateExistingMenu() => GenerateAll(true);

    [MenuItem("Tools/Ponggle/Remove Peg Shadows (Scene)")]
    public static void RemoveAllMenu() => RemoveAll();

    [MenuItem("Tools/Ponggle/Select All Shadows (Scene)")]
    public static void SelectAllMenu() => SelectAllShadows();

    public static void GenerateAll(bool onlyUpdateExisting)
    {
        int created = 0, updated = 0;

        foreach (var sr in FindCandidateSpriteRenderers())
        {
            var host = sr.gameObject;

            var shadowT = host.transform.Find(ShadowChildName);
            if (onlyUpdateExisting && !shadowT) continue;

            SpriteRenderer shadowSR;
            if (!shadowT)
            {
                shadowT = new GameObject(ShadowChildName).transform;
                shadowT.SetParent(host.transform, false);
                shadowSR = shadowT.gameObject.AddComponent<SpriteRenderer>();
                created++;
            }
            else
            {
                shadowSR = shadowT.GetComponent<SpriteRenderer>() ?? shadowT.gameObject.AddComponent<SpriteRenderer>();
                updated++;
            }

            // Copy sprite look
            shadowSR.sprite = sr.sprite;
            shadowSR.flipX = sr.flipX;
            shadowSR.flipY = sr.flipY;
            shadowSR.sharedMaterial = sr.sharedMaterial;
            shadowSR.maskInteraction = SpriteMaskInteraction.None;

            // Draw behind host
            shadowSR.sortingLayerID = sr.sortingLayerID;
            shadowSR.sortingOrder = sr.sortingOrder + SortingOrderOffset;

            // Tint & scale
            shadowSR.color = ShadowColor;
            shadowT.localScale = Vector3.one * ShadowScale;

            // First placement
            ApplyTransform(shadowT);

            // Ensure runtime follower exists & configured
            var keep = shadowT.GetComponent<KeepShadowFollow>();
            if (!keep) keep = shadowT.gameObject.AddComponent<KeepShadowFollow>();
            keep.offset = ShadowOffset;
            keep.scale = ShadowScale;
            keep.rotateWithPeg = RotateWithPeg;
            keep.offsetSpace = OffsetSpaceMode;

            EditorUtility.SetDirty(shadowSR);
            EditorUtility.SetDirty(host);
            PrefabUtility.RecordPrefabInstancePropertyModifications(shadowSR);
            PrefabUtility.RecordPrefabInstancePropertyModifications(keep);
        }

        Debug.Log($"PegShadowGenerator: created {created}, updated {updated} shadow(s).");
    }

    static void ApplyTransform(Transform shadowT)
    {
        var p = shadowT.parent;
        if (OffsetSpaceMode == OffsetSpace.Local && p)
            shadowT.position = p.TransformPoint((Vector3)ShadowOffset);
        else
            shadowT.position = (p ? p.position : Vector3.zero) + (Vector3)ShadowOffset;

        shadowT.rotation = (RotateWithPeg && p) ? p.rotation : Quaternion.identity;
    }

    public static void RemoveAll()
    {
        int removed = 0;
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.hideFlags != HideFlags.None) continue; // skip assets
            if (t.name != ShadowChildName) continue;
            if (!t.parent) continue;
            if (!IsCandidate(t.parent.gameObject)) continue;

            Undo.DestroyObjectImmediate(t.gameObject);
            removed++;
        }
        Debug.Log($"PegShadowGenerator: removed {removed} shadow(s).");
    }

    public static void SelectAllShadows()
    {
        var list = new List<Object>();
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.hideFlags != HideFlags.None) continue;
            if (t.name != ShadowChildName) continue;
            if (!t.parent) continue;
            if (!IsCandidate(t.parent.gameObject)) continue;
            list.Add(t.gameObject);
        }
        Selection.objects = list.ToArray();
        Debug.Log($"PegShadowGenerator: selected {list.Count} shadow object(s).");
    }

    static IEnumerable<SpriteRenderer> FindCandidateSpriteRenderers()
    {
        foreach (var sr in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
        {
            if (sr.hideFlags != HideFlags.None) continue; // ignore prefab assets
            var go = sr.gameObject;
            if (!go.scene.IsValid()) continue;            // must be in scene
            if (!IsCandidate(go)) continue;
            yield return sr;
        }
    }

    static bool IsCandidate(GameObject go)
    {
        if (RestrictToParent && ParentRoot && !go.transform.IsChildOf(ParentRoot))
            return false;

        if (DetectByLayer && (AllowedLayers.value & (1 << go.layer)) == 0)
            return false;

        bool ok = false;

        if (DetectByTag && go.CompareTag(TagName)) ok = true;

        if (!ok && DetectByComponent && go.GetComponent<PegController>()) ok = true;

        if (!ok && DetectByName)
        {
            var keywords = ParseKeywords(NameKeywordsCsv);
            string nm = go.name.ToLowerInvariant();
            foreach (var kw in keywords)
            {
                if (string.IsNullOrWhiteSpace(kw)) continue;
                if (nm.Contains(kw)) { ok = true; break; }
            }
        }

        return ok;
    }

    static List<string> ParseKeywords(string csv)
    {
        if (string.IsNullOrEmpty(csv)) return new List<string>();
        return csv.Split(',')
                  .Select(s => s.Trim().ToLowerInvariant())
                  .Where(s => !string.IsNullOrEmpty(s))
                  .Distinct()
                  .ToList();
    }
}

// ====== Editor Window ======
public class PegShadowGeneratorWindow : EditorWindow
{
    public static void Open() => GetWindow<PegShadowGeneratorWindow>("Peg Shadow Generator");

    // Helper: LayerMask chooser compatible with 2022.x
    static LayerMask LayerMaskField(string label, LayerMask mask)
    {
        var layerNames = InternalEditorUtility.layers; // only named layers
        int shownMask = 0;

        // Map real mask -> shown mask (indices 0..N-1)
        for (int i = 0; i < layerNames.Length; i++)
        {
            int layer = LayerMask.NameToLayer(layerNames[i]);
            if ((mask.value & (1 << layer)) != 0)
                shownMask |= (1 << i);
        }

        shownMask = EditorGUILayout.MaskField(label, shownMask, layerNames);

        // Map shown mask -> real mask bits
        int realMask = 0;
        for (int i = 0; i < layerNames.Length; i++)
        {
            if ((shownMask & (1 << i)) != 0)
            {
                int layer = LayerMask.NameToLayer(layerNames[i]);
                realMask |= (1 << layer);
            }
        }

        mask.value = realMask;
        return mask;
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Shadow Look", EditorStyles.boldLabel);
        PegShadowGenerator.ShadowOffset = EditorGUILayout.Vector2Field("Offset", PegShadowGenerator.ShadowOffset);
        PegShadowGenerator.ShadowScale = EditorGUILayout.Slider("Scale", PegShadowGenerator.ShadowScale, 0.8f, 1.2f);
        PegShadowGenerator.ShadowColor = EditorGUILayout.ColorField("Color (alpha)", PegShadowGenerator.ShadowColor);
        PegShadowGenerator.SortingOrderOffset = EditorGUILayout.IntField("Sorting Order Offset", PegShadowGenerator.SortingOrderOffset);

        EditorGUILayout.Space(4);
        PegShadowGenerator.RotateWithPeg = EditorGUILayout.Toggle("Rotate With Peg", PegShadowGenerator.RotateWithPeg);
        PegShadowGenerator.OffsetSpaceMode = (OffsetSpace)EditorGUILayout.EnumPopup("Offset Space", PegShadowGenerator.OffsetSpaceMode);

        PegShadowGenerator.ShadowChildName = EditorGUILayout.TextField("Child Name", PegShadowGenerator.ShadowChildName);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Filters (optional)", EditorStyles.boldLabel);

        PegShadowGenerator.RestrictToParent = EditorGUILayout.Toggle("Restrict to Parent", PegShadowGenerator.RestrictToParent);
        using (new EditorGUI.DisabledScope(!PegShadowGenerator.RestrictToParent))
            PegShadowGenerator.ParentRoot = (Transform)EditorGUILayout.ObjectField("Parent Root", PegShadowGenerator.ParentRoot, typeof(Transform), true);

        PegShadowGenerator.DetectByTag = EditorGUILayout.Toggle("Detect by Tag", PegShadowGenerator.DetectByTag);
        using (new EditorGUI.DisabledScope(!PegShadowGenerator.DetectByTag))
            PegShadowGenerator.TagName = EditorGUILayout.TextField("Tag Name", PegShadowGenerator.TagName);

        PegShadowGenerator.DetectByComponent = EditorGUILayout.Toggle("Detect by PegController", PegShadowGenerator.DetectByComponent);

        PegShadowGenerator.DetectByName = EditorGUILayout.Toggle("Detect by Name Keywords", PegShadowGenerator.DetectByName);
        using (new EditorGUI.DisabledScope(!PegShadowGenerator.DetectByName))
            PegShadowGenerator.NameKeywordsCsv = EditorGUILayout.TextField("Name Keywords (CSV)", PegShadowGenerator.NameKeywordsCsv);

        PegShadowGenerator.DetectByLayer = EditorGUILayout.Toggle("Detect by Layer", PegShadowGenerator.DetectByLayer);
        using (new EditorGUI.DisabledScope(!PegShadowGenerator.DetectByLayer))
            PegShadowGenerator.AllowedLayers = LayerMaskField("Allowed Layers", PegShadowGenerator.AllowedLayers);

        EditorGUILayout.Space(10);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate (Create or Update)")) PegShadowGenerator.GenerateAll(false);
            if (GUILayout.Button("Update Existing Only")) PegShadowGenerator.GenerateAll(true);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Remove All Shadows")) PegShadowGenerator.RemoveAll();
            if (GUILayout.Button("Select All Shadows")) PegShadowGenerator.SelectAllShadows();
        }
    }
}
#endif
