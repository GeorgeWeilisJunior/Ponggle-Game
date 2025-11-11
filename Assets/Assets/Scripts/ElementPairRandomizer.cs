using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ElementPairRandomizer : MonoBehaviour
{
    public enum ApplyMode { AssignTypeOnly, ReplacePrefabInPlace }

    [Header("Scope (opsional)")]
    public Transform searchRoot;

    [Header("Kapan dijalankan")]
    public bool runOnStart = true;
    [Min(0)] public int waitFramesBeforeRun = 1;

    [Header("Elemen yang boleh dipilih (selalu 2 yang berbeda)")]
    public bool allowFire = true;
    public bool allowWater = true;
    public bool allowEarth = true;
    public bool allowWind = true;

    [Header("Cara menerapkan")]
    public ApplyMode mode = ApplyMode.AssignTypeOnly;

    [Header("Prefab mapping (untuk ReplacePrefabInPlace)")]
    public GameObject firePrefab;
    public GameObject waterPrefab;
    public GameObject earthPrefab;
    public GameObject windPrefab;

    [Header("Replace Settings")]
    [Tooltip("Copy tag/layer dan Sorting Layer/Order dari SpriteRenderer utama.")]
    public bool copyCommonVisualSettings = true;

    [Tooltip("Samakan localScale child SpriteRenderer baru dengan yang lama (mencegah terlihat mengecil).")]
    public bool copyChildSpriteLocalScale = true;

    [Header("Shadow Settings")]
    [Tooltip("Offset sortingOrder shadow relatif ke SR utama (biasanya negatif agar di belakang).")]
    public int shadowOrderOffset = -1;

    void Start()
    {
        if (runOnStart) StartCoroutine(DelayThenRun());
    }

    IEnumerator DelayThenRun()
    {
        for (int i = 0; i < waitFramesBeforeRun; i++) yield return null;
        RandomizeNow();
    }

    [ContextMenu("Randomize Now")]
    public void RandomizeNow()
    {
        // 1) Kumpulkan target secara robust
        var targets = GatherTargets();
        if (targets.Count == 0)
        {
            Debug.LogWarning("[ElementPairRandomizer] Tidak menemukan Element Peg yang valid (PegController.Type==Element atau ada ElementPeg).");
            return;
        }

        // 2) Pilih 2 elemen berbeda
        var pool = BuildAllowedPool();
        if (pool.Count < 2)
        {
            Debug.LogWarning("[ElementPairRandomizer] Minimal 2 elemen harus diizinkan.");
            return;
        }

        var a = pool[Random.Range(0, pool.Count)];
        ElementType b; do { b = pool[Random.Range(0, pool.Count)]; } while (b == a);

        // 3) Distribusi acak tapi seimbang
        var chosenForEach = DistributePair(targets.Count, a, b);

        // 4) Terapkan
        int applied = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (ApplyToTarget(t, chosenForEach[i])) applied++;
        }

        Debug.Log($"[ElementPairRandomizer] Pair: {a} & {b} | Target found: {targets.Count}, applied: {applied}, mode: {mode}");
    }

    // ===== Target discovery =====

    struct TargetInfo
    {
        public GameObject root;  // GO yang akan diubah/diganti
        public PegController pc; // PegController di root (kalau ada)
        public ElementPeg ep;    // ElementPeg di root/child (opsional)
    }

    List<TargetInfo> GatherTargets()
    {
        var list = new List<TargetInfo>(64);

        IEnumerable<Transform> Scope()
        {
            if (searchRoot)
                return searchRoot.GetComponentsInChildren<Transform>(true);
            else
                return FindObjectsOfType<Transform>(true);
        }

        // 1) Prioritas: PegController.Type == Element
        var allPC = Scope()
            .Select(t => t.GetComponent<PegController>())
            .Where(pc => pc && pc.gameObject.activeInHierarchy && pc.Type == PegType.Element);

        foreach (var pc in allPC)
        {
            var root = pc.gameObject;
            var ep = root.GetComponent<ElementPeg>() ?? root.GetComponentInChildren<ElementPeg>(true);
            list.Add(new TargetInfo { root = root, pc = pc, ep = ep });
        }

        // 2) Fallback: ElementPeg yang belum ter-cover
        var already = new HashSet<GameObject>(list.Select(x => x.root));
        var allEP = Scope()
            .Select(t => t.GetComponent<ElementPeg>())
            .Where(ep => ep && ep.gameObject.activeInHierarchy);

        foreach (var ep in allEP)
        {
            var root = ep.GetComponent<PegController>() ? ep.gameObject : ep.transform.root.gameObject;
            if (already.Contains(root)) continue;

            var pc = root.GetComponent<PegController>() ?? ep.GetComponentInParent<PegController>();
            list.Add(new TargetInfo { root = pc ? pc.gameObject : ep.gameObject, pc = pc, ep = ep });
        }

        // Unikkan by root
        return list.GroupBy(t => t.root).Select(g => g.First()).ToList();
    }

    List<ElementType> BuildAllowedPool()
    {
        var pool = new List<ElementType>(4);
        if (allowFire) pool.Add(ElementType.Fire);
        if (allowWater) pool.Add(ElementType.Water);
        if (allowEarth) pool.Add(ElementType.Earth);
        if (allowWind) pool.Add(ElementType.Wind);
        return pool;
    }

    List<ElementType> DistributePair(int count, ElementType a, ElementType b)
    {
        var list = new List<ElementType>(count);
        int half = count / 2;
        for (int i = 0; i < count; i++) list.Add(i < half ? a : b);

        // simple shuffle
        System.Random rng = new System.Random();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int k = rng.Next(i + 1);
            (list[i], list[k]) = (list[k], list[i]);
        }
        return list;
    }

    // ===== Shadow helpers =====

    static List<Transform> CollectShadowChildren(GameObject root)
    {
        var list = new List<Transform>();
        if (!root) return list;

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!t || t == root.transform) continue;
            string n = t.name.ToLowerInvariant();
            bool nameLooksLikeShadow = n.Contains("shadow");

            bool hasShadowLikeComp = false;
            var comps = ListPool<Component>.Get();
            t.GetComponents(comps);
            for (int i = 0; i < comps.Count; i++)
            {
                var c = comps[i];
                if (!c) continue;
                string cn = c.GetType().Name.ToLowerInvariant();
                if (cn.Contains("shadow")) { hasShadowLikeComp = true; break; }
            }
            ListPool<Component>.Release(comps);

            if (nameLooksLikeShadow || hasShadowLikeComp) list.Add(t);
        }
        return list;
    }

    static SpriteRenderer FindMainSR(GameObject go)
    {
        if (!go) return null;
        SpriteRenderer best = null;
        int bestOrder = int.MinValue;
        var srs = ListPool<SpriteRenderer>.Get();
        go.GetComponentsInChildren(true, srs);
        for (int i = 0; i < srs.Count; i++)
        {
            var sr = srs[i];
            if (!sr) continue;
            if (sr.sortingOrder >= bestOrder) { bestOrder = sr.sortingOrder; best = sr; }
        }
        ListPool<SpriteRenderer>.Release(srs);
        return best;
    }

    static void DetachShadows(List<Transform> shadows)
    {
        // Lepaskan dulu supaya tidak ikut terhapus saat visual dibangun ulang / prefab lama di-Destroy
        for (int i = 0; i < shadows.Count; i++)
        {
            var sh = shadows[i];
            if (!sh) continue;
            sh.SetParent(null, true); // tetap jaga world transform
        }
    }

    void AttachShadows(GameObject newRoot, List<Transform> shadows)
    {
        if (!newRoot || shadows == null || shadows.Count == 0) return;

        var mainSR = FindMainSR(newRoot);
        for (int i = 0; i < shadows.Count; i++)
        {
            var sh = shadows[i];
            if (!sh) continue;

            sh.SetParent(newRoot.transform, true);

            var shSR = sh.GetComponent<SpriteRenderer>();
            if (mainSR && shSR)
            {
                shSR.sortingLayerID = mainSR.sortingLayerID;
                shSR.sortingLayerName = mainSR.sortingLayerName;
                shSR.sortingOrder = mainSR.sortingOrder + shadowOrderOffset;
            }
        }
    }

    // ===== Apply =====

    bool ApplyToTarget(TargetInfo t, ElementType chosen)
    {
        if (mode == ApplyMode.AssignTypeOnly)
        {
            var pc = t.pc ?? t.root.GetComponent<PegController>() ?? t.root.GetComponentInChildren<PegController>(true);
            if (!pc)
            {
                Debug.LogWarning($"[ElementPairRandomizer] Skip AssignTypeOnly: tidak menemukan PegController di '{t.root.name}'.");
                return false;
            }

            // SELAMATKAN SHADOW (detach dulu biar tidak ikut terhapus saat refresh visual)
            var shadowCache = CollectShadowChildren(pc.gameObject);
            DetachShadows(shadowCache);

            // Set elemen via API yang memang ada di PegController kamu
            pc.SetElementType(chosen, true);

            // Re-attach + perbaiki sorting
            AttachShadows(pc.gameObject, shadowCache);
            return true;
        }

        // ReplacePrefabInPlace
        var prefab = GetPrefabFor(chosen);
        if (!prefab)
        {
            Debug.LogWarning($"[ElementPairRandomizer] Prefab untuk {chosen} belum di-assign.");
            return false;
        }

        var root = t.root;
        var parent = root.transform.parent;
        var pos = root.transform.position;
        var rot = root.transform.rotation;
        var scl = root.transform.localScale;

        // simpan visual basics dari instance lama
        int sortingLayerID = 0, sortingOrder = 0;
        string tag = root.tag;
        int layer = root.layer;

        // Ambil SR utama lama
        SpriteRenderer oldSR = null;
        {
            var allSR = root.GetComponentsInChildren<SpriteRenderer>(true);
            int bestOrder = int.MinValue;
            foreach (var sr in allSR)
            {
                if (!sr) continue;
                if (sr.sortingOrder >= bestOrder)
                {
                    bestOrder = sr.sortingOrder;
                    oldSR = sr;
                }
            }
        }
        if (copyCommonVisualSettings && oldSR)
        {
            sortingLayerID = oldSR.sortingLayerID;
            sortingOrder = oldSR.sortingOrder;
        }

        // simpan localScale SR child lama (agar ukuran sama)
        Vector3 oldSpriteLocalScale = Vector3.one;
        if (copyChildSpriteLocalScale && oldSR)
            oldSpriteLocalScale = oldSR.transform.localScale;

        // >>>> SELAMATKAN SHADOW DARI ROOT LAMA
        var shadowCacheReplace = CollectShadowChildren(root);
        DetachShadows(shadowCacheReplace);

        // spawn baru
        var newGo = Instantiate(prefab, pos, rot, parent);
        newGo.transform.localScale = scl;

        // pastikan komponen minimal
        var newPC = newGo.GetComponent<PegController>() ?? newGo.AddComponent<PegController>();
        newPC.ForceSetPegType(PegType.Element, refreshVisual: true);

        var newEP = newGo.GetComponent<ElementPeg>();
        if (!newEP) newEP = newGo.AddComponent<ElementPeg>();
        newEP.element = chosen;

        // set elemen via API agar visual sinkron
        newPC.SetElementType(chosen, true);

        // copy visual basics + child SR localScale
        if (copyCommonVisualSettings)
        {
            newGo.tag = tag;
            newGo.layer = layer;

            SpriteRenderer newSR = null;
            {
                var allSR = newGo.GetComponentsInChildren<SpriteRenderer>(true);
                int bestOrder = int.MinValue;
                foreach (var sr in allSR)
                {
                    if (!sr) continue;
                    if (sr.sortingOrder >= bestOrder)
                    {
                        bestOrder = sr.sortingOrder;
                        newSR = sr;
                    }
                }
            }

            if (newSR && oldSR)
            {
                newSR.sortingLayerID = sortingLayerID;
                newSR.sortingOrder = sortingOrder;

                if (copyChildSpriteLocalScale)
                    newSR.transform.localScale = oldSpriteLocalScale;
            }
        }

        // >>>> PASANG KEMBALI SHADOW KE INSTANCE BARU + atur sorting
        AttachShadows(newGo, shadowCacheReplace);

        // hapus lama
        if (Application.isPlaying) Destroy(root);
        else DestroyImmediate(root);

        return true;
    }

    GameObject GetPrefabFor(ElementType t)
    {
        switch (t)
        {
            case ElementType.Fire: return firePrefab;
            case ElementType.Water: return waterPrefab;
            case ElementType.Earth: return earthPrefab;
            case ElementType.Wind: return windPrefab;
        }
        return null;
    }
}

/// Pool sederhana untuk mengurangi GC
static class ListPool<T>
{
    static readonly Stack<List<T>> pool = new Stack<List<T>>();
    public static List<T> Get() => pool.Count > 0 ? pool.Pop() : new List<T>(16);
    public static void Release(List<T> list)
    {
        if (list == null) return;
        list.Clear();
        pool.Push(list);
    }
}
