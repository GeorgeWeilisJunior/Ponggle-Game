using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class PegRandomizer : MonoBehaviour
{
    // ===== Coordination with Intro / external orchestration =====
    public static bool IsRandomizing { get; private set; }
    public static bool HasRunThisScene { get; private set; }
    public static int LastRunFrame { get; private set; } = -1;
    public static System.Action OnRandomizeDone;

    // === Anti repeat antar-retry ===
    static int _lastLayoutHash = 0;

    [Header("What to Randomize")]
    [SerializeField] bool randomizeOrange = true;
    [SerializeField] bool randomizeGreen = true;

    [Header("When")]
    [SerializeField] bool runOnStart = true;
    [SerializeField, Min(0)] int waitFramesBeforeRun = 1;

    [Header("Safety")]
    [Tooltip("Kandidat Blue harus di parent/scope yang sama.")]
    [SerializeField] bool onlySwapWithinSameParent = true;

    [Tooltip("Kandidat Blue harus punya PegSwapGroup.group yang sama (dengan fallback kompatibel opsional).")]
    [SerializeField] bool onlySwapWithinSameGroup = true;

    [Tooltip("Jika aktif, kandidat Blue harus dari keluarga bentuk (Rounded/Brick/…) yang sama.")]
    [SerializeField] bool requireSameFamily = true;

    [Tooltip("Izinkan fallback group kompatibel ketika group identik tidak ada (lihat mapping).")]
    [SerializeField] bool allowCompatibleFallback = true;

    [Tooltip("Swap juga rotasi.")]
    [SerializeField] bool swapRotation = true;

    [Tooltip("Selalu swap scale bila kedua peg satu keluarga bentuk (aman untuk arc).")]
    [SerializeField] bool swapScaleIfSameFamily = true;

    [Header("Exclude Groups")]
    [Tooltip("Abaikan peg dengan group Hard dari proses randomize (disarankan untuk menjaga arc brick).")]
    [SerializeField] bool excludeHard = true;

    [Header("Orange Template")]
    [Tooltip("Gunakan komposisi ORANGE yang sudah ada di level sebagai template kuota random.")]
    [SerializeField] bool useOrangeTemplate = true;
    [Tooltip("Template juga mengikuti sebaran region (Top/Mid/Bottom).")]
    [SerializeField] bool templateTracksRegion = true;
    [Tooltip("Template juga mengikuti family (Rounded/Brick/RoundedBrick/...).")]
    [SerializeField] bool templateTracksFamily = true;

    [Header("Debug")]
    [SerializeField] bool logWhenNoCandidate = false;

    void OnEnable()
    {
        IsRandomizing = false;
    }

    void Start()
    {
        if (!runOnStart) return;
        StartCoroutine(RunAfterFrames(waitFramesBeforeRun));
    }

    IEnumerator RunAfterFrames(int frames)
    {
        // Kalau ada intro yang sedang main, tunggu dulu
        while (PegIntroPop.IsIntroPlaying) yield return null;
        for (int i = 0; i < frames; i++) yield return null;
        TryRandomize();
    }

    // ===== Utilities =====

    // Seed unik agar hasil benar-benar berubah pada setiap start level / retry
    static int MakeUniqueSeed()
    {
        unchecked
        {
            int s = (int)System.DateTime.UtcNow.Ticks;
            s ^= UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            s ^= System.Environment.TickCount;
            s ^= Time.frameCount;
            s ^= System.Guid.NewGuid().GetHashCode();
            return s;
        }
    }

    static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int k = rng.Next(i + 1);
            (list[i], list[k]) = (list[k], list[i]);
        }
    }

    PegSwapGroupId GetGroup(Transform t)
    {
        var g = t.GetComponent<PegSwapGroup>() ?? t.GetComponentInParent<PegSwapGroup>();
        return g ? g.group : PegSwapGroupId.Normal;
    }

    PegFamily GetFamily(Transform t)
    {
        var tag = t.GetComponent<PegFamilyTag>() ?? t.GetComponentInParent<PegFamilyTag>();
        if (tag) return tag.family;

        string n = t.name.ToLowerInvariant();
        PegFamily f = PegFamily.Unknown;
        if (n.Contains("moreroundedbrick") || n.Contains("more_rounded_brick")) f = PegFamily.MoreRoundedBrick;
        else if (n.Contains("roundedbrick") || n.Contains("rounded_brick")) f = PegFamily.RoundedBrick;
        else if (n.Contains("brick")) f = PegFamily.Brick;
        else if (n.Contains("round")) f = PegFamily.Rounded;

        var add = t.gameObject.AddComponent<PegFamilyTag>();
        add.family = f;
        return f;
    }

    int GetFamilyId(PegController p) => (int)GetFamily(p.transform);

    bool SameParent(Transform a, Transform b) => a.parent == b.parent;

    List<PegSwapGroupId> GetCompatibleGroups(PegSwapGroupId g)
    {
        var list = new List<PegSwapGroupId> { g };
        if (!allowCompatibleFallback || !onlySwapWithinSameGroup) return list;

        switch (g)
        {
            case PegSwapGroupId.AntiGravityAndDisappearing:
                list.Add(PegSwapGroupId.AntiGravity);
                list.Add(PegSwapGroupId.Disappearing);
                list.Add(PegSwapGroupId.Normal);
                break;
            case PegSwapGroupId.AntiGravity:
                list.Add(PegSwapGroupId.AntiGravityAndDisappearing);
                list.Add(PegSwapGroupId.Normal);
                break;
            case PegSwapGroupId.Disappearing:
                list.Add(PegSwapGroupId.AntiGravityAndDisappearing);
                list.Add(PegSwapGroupId.Normal);
                break;
        }
        return list;
    }

    bool IsExcludedByGroup(Transform t)
    {
        var g = GetGroup(t);
        if (excludeHard && g == PegSwapGroupId.Hard) return true;
        return false;
    }

    // Untuk meng-hash layout ORANGE (sidik jari sederhana)
    int HashOrangeLayout(IEnumerable<PegController> pegs)
    {
        unchecked
        {
            int h = 17;
            foreach (var p in pegs
                     .Where(x => x.Type == PegType.Orange && x.State != PegController.PegState.Cleared)
                     .OrderBy(x => x.transform.position.x)
                     .ThenBy(x => x.transform.position.y))
            {
                var v = p.transform.position;
                int hx = Mathf.RoundToInt(v.x * 1000f);
                int hy = Mathf.RoundToInt(v.y * 1000f);
                h = h * 31 + hx; h = h * 31 + hy;
            }
            return h;
        }
    }

    struct XForm
    {
        public Vector3 pos;
        public Quaternion rot;
        public Vector3 scale;
    }

    void ApplySwap(Transform a, Transform b, bool forceSwapScale)
    {
        Vector3 posA = a.position, posB = b.position;
        Quaternion rotA = a.rotation, rotB = b.rotation;
        Vector3 scaleA = a.localScale, scaleB = b.localScale;

        a.position = posB; b.position = posA;
        if (swapRotation) { a.rotation = rotB; b.rotation = rotA; }
        if (forceSwapScale) { a.localScale = scaleB; b.localScale = scaleA; }
    }

    // Kembalikan kandidat BLUE untuk target tertentu, sekaligus apakah aman menukar scale
    List<PegController> GetBlueCandidatesFor(
        PegController target,
        HashSet<PegController> alreadyTaken,
        out bool sameFamilyWillSwapScale,
        List<PegController> blues)
    {
        var targetGroup = GetGroup(target.transform);
        var targetFamily = GetFamily(target.transform);

        sameFamilyWillSwapScale = false;

        IEnumerable<PegController> q = blues.Where(b => !alreadyTaken.Contains(b))
                                            .Where(b => !IsExcludedByGroup(b.transform));

        if (IsExcludedByGroup(target.transform))
            return new List<PegController>();

        if (onlySwapWithinSameParent)
        {
            var tScope = GetSwapScope(target.transform);

            if (tScope != null)
            {
                // 🔒 Target ada di scope dinamis (moving/rotating) -> kunci di scope yang sama
                q = q.Where(b => GetSwapScope(b.transform) == tScope);
            }
            else
            {
                // 🔓 Target BUKAN di scope -> jangan ambil kandidat dari scope dinamis
                q = q.Where(b => GetSwapScope(b.transform) == null);

                // Kalau mau batasi parent statis yang sama, pakai baris di bawah:
                // q = q.Where(b => SameParent(target.transform, b.transform));
            }
        }

        if (requireSameFamily)
        {
            q = q.Where(b => GetFamily(b.transform) == targetFamily);
            sameFamilyWillSwapScale = true; // aman tukar scale
        }

        if (onlySwapWithinSameGroup)
        {
            var prio = GetCompatibleGroups(targetGroup);
            foreach (var g in prio)
            {
                var bucket = q.Where(b => GetGroup(b.transform) == g).ToList();
                if (bucket.Count > 0) return bucket;
            }
            return new List<PegController>();
        }

        return q.ToList();
    }

    // ============ PUBLIC API ============

    [ContextMenu("Randomize Now")]
    public void TryRandomize()
    {
        if (IsRandomizing) return;
        if (HasRunThisScene && LastRunFrame == Time.frameCount) return;
        if (PegIntroPop.IsIntroPlaying) return;

        IsRandomizing = true;

        // Kumpulkan semua peg aktif
        var all = FindObjectsOfType<PegController>(true)
                 .Where(p => p != null && p.gameObject.activeInHierarchy)
                 .Where(p => p.State != PegController.PegState.Cleared)
                 .ToList();

        if (all.Count == 0) { Done(); return; }

        var blues = all.Where(p => p.Type == PegType.Blue).ToList();
        var oranges = all.Where(p => p.Type == PegType.Orange).ToList();
        var greens = all.Where(p => p.Type == PegType.Green).ToList();

        // Simpan transform awal supaya bisa di-rollback bila hash sama
        var snapshot = new Dictionary<Transform, XForm>(all.Count);
        foreach (var p in all)
            snapshot[p.transform] = new XForm { pos = p.transform.position, rot = p.transform.rotation, scale = p.transform.localScale };

        // Jalankan sekali
        int curHash = DoSwaps(all, blues, oranges, greens, MakeUniqueSeed());

        // Anti repeat: jika sama dengan run sebelumnya → restore & reroll sekali
        if (curHash == _lastLayoutHash)
        {
            foreach (var kv in snapshot)
            {
                kv.Key.position = kv.Value.pos;
                kv.Key.rotation = kv.Value.rot;
                kv.Key.localScale = kv.Value.scale;
            }
            curHash = DoSwaps(all, blues, oranges, greens, MakeUniqueSeed());
        }

        _lastLayoutHash = curHash;
        Done();
    }

    int DoSwaps(
        List<PegController> all,
        List<PegController> blues,
        List<PegController> oranges,
        List<PegController> greens,
        int seed)
    {
        var rng = new System.Random(seed);

        // ===== Region setup =====
        const int REGIONS = 3; // top-mid-bottom
        float yMin = all.Min(p => p.transform.position.y);
        float yMax = all.Max(p => p.transform.position.y);

        // hitung distribusi BLUE per region (untuk fallback non-template)
        var blueRegionCounts = new int[REGIONS];
        foreach (var b in blues)
            blueRegionCounts[RegionIndex(b.transform, yMin, yMax, REGIONS)]++;

        int totalBlue = Mathf.Max(1, blues.Count);

        // Kuota default (proporsional ke BLUE) — dipakai kalau template dimatikan
        var targetOrangePerRegion_Default = new int[REGIONS];
        for (int i = 0; i < REGIONS; i++)
            targetOrangePerRegion_Default[i] = Mathf.RoundToInt((float)blueRegionCounts[i] / totalBlue * Mathf.Max(1, oranges.Count));

        var targetGreenPerRegion = new int[REGIONS];
        for (int i = 0; i < REGIONS; i++)
            targetGreenPerRegion[i] = Mathf.RoundToInt((float)blueRegionCounts[i] / totalBlue * Mathf.Max(1, greens.Count));

        // ===== Template dari ORANGE yang sudah ada di level =====
        // region: 0..2; family: int dari PegFamily
        var tmplOrangePerRegion = new int[REGIONS];
        var tmplOrangePerFamily = new Dictionary<int, int>();
        var tmplOrangePerRegFam = new Dictionary<(int region, int family), int>();

        if (useOrangeTemplate)
        {
            foreach (var o in oranges)
            {
                int r = RegionIndex(o.transform, yMin, yMax, REGIONS);
                int f = GetFamilyId(o);

                tmplOrangePerRegion[r]++;
                if (!tmplOrangePerFamily.ContainsKey(f)) tmplOrangePerFamily[f] = 0;
                tmplOrangePerFamily[f]++;

                var key = (r, f);
                if (!tmplOrangePerRegFam.ContainsKey(key)) tmplOrangePerRegFam[key] = 0;
                tmplOrangePerRegFam[key]++;
            }
        }

        // ===== ORANGE =====
        if (randomizeOrange && oranges.Count > 0)
        {
            Shuffle(oranges, rng);
            var taken = new HashSet<PegController>();

            foreach (var o in oranges)
            {
                bool willSwapScale;
                var cands = GetBlueCandidatesFor(o, taken, out willSwapScale, blues);
                if (cands.Count == 0)
                {
                    if (logWhenNoCandidate)
                        Debug.Log($"[PegRandomizer] No BLUE candidate for ORANGE '{o.name}' (group={GetGroup(o.transform)}, family={GetFamily(o.transform)})");
                    continue;
                }

                Shuffle(cands, rng);

                List<PegController> filtered = cands;
                int targetRegion = -1;
                int targetFamily = -1;

                if (useOrangeTemplate)
                {
                    // acak urutan region supaya tidak bias 0..1..2
                    int[] order = { 0, 1, 2 };
                    for (int i = 0; i < order.Length; i++)
                    { int j = rng.Next(i, order.Length); (order[i], order[j]) = (order[j], order[i]); }

                    if (templateTracksFamily)
                    {
                        // cari family yang masih butuh
                        foreach (var kv in tmplOrangePerFamily)
                        {
                            if (kv.Value > 0) { targetFamily = kv.Key; break; }
                        }
                    }

                    if (templateTracksRegion)
                    {
                        // cari region yang masih butuh; kalau juga track family, pastikan pair-nya masih ada
                        foreach (int r in order)
                        {
                            if (tmplOrangePerRegion[r] <= 0) continue;

                            if (templateTracksFamily && targetFamily != -1)
                            {
                                var key = (r, targetFamily);
                                if (tmplOrangePerRegFam.TryGetValue(key, out int q) && q > 0)
                                { targetRegion = r; break; }
                            }
                            else
                            { targetRegion = r; break; }
                        }
                    }

                    // filter ketat: region + family
                    if (templateTracksFamily && targetFamily != -1)
                        filtered = filtered.Where(p => GetFamilyId(p) == targetFamily).ToList();

                    if (templateTracksRegion && targetRegion != -1)
                        filtered = filtered.Where(p => RegionIndex(p.transform, yMin, yMax, REGIONS) == targetRegion).ToList();

                    // longgarkan bertahap bila kosong
                    if (filtered.Count == 0 && templateTracksRegion && targetRegion != -1)
                        filtered = cands.Where(p => RegionIndex(p.transform, yMin, yMax, REGIONS) == targetRegion).ToList();

                    if (filtered.Count == 0 && templateTracksFamily && targetFamily != -1)
                        filtered = cands.Where(p => GetFamilyId(p) == targetFamily).ToList();

                    if (filtered.Count == 0) filtered = cands; // total fallback
                }
                else
                {
                    // === Perilaku lama: pakai kuota proporsional BLUE per-region ===
                    int chooseRegion = -1;
                    for (int r = 0; r < REGIONS; r++)
                        if (targetOrangePerRegion_Default[r] > 0) { chooseRegion = r; break; }

                    if (chooseRegion >= 0)
                    {
                        var f = cands.Where(c => RegionIndex(c.transform, yMin, yMax, REGIONS) == chooseRegion).ToList();
                        if (f.Count > 0) filtered = f;
                    }
                }

                var chosen = filtered[0];
                taken.Add(chosen);

                bool forceScale = swapScaleIfSameFamily && willSwapScale;
                ApplySwap(o.transform, chosen.transform, forceScale);

                // Kurangi kuota yang terkait
                int br = RegionIndex(chosen.transform, yMin, yMax, REGIONS);
                int bf = GetFamilyId(chosen);

                if (useOrangeTemplate)
                {
                    if (templateTracksRegion && br >= 0 && br < REGIONS)
                        tmplOrangePerRegion[br] = Mathf.Max(0, tmplOrangePerRegion[br] - 1);

                    if (templateTracksFamily && tmplOrangePerFamily.ContainsKey(bf))
                        tmplOrangePerFamily[bf] = Mathf.Max(0, tmplOrangePerFamily[bf] - 1);

                    if (templateTracksRegion && templateTracksFamily)
                    {
                        var key = (br, bf);
                        if (tmplOrangePerRegFam.ContainsKey(key))
                            tmplOrangePerRegFam[key] = Mathf.Max(0, tmplOrangePerRegFam[key] - 1);
                    }
                }
                else
                {
                    if (br >= 0 && br < REGIONS)
                        targetOrangePerRegion_Default[br] = Mathf.Max(0, targetOrangePerRegion_Default[br] - 1);
                }
            }
        }

        // ===== GREEN =====
        if (randomizeGreen && greens.Count > 0)
        {
            var taken = new HashSet<PegController>();

            foreach (var g in greens.OrderBy(_ => rng.Next()))
            {
                bool willSwapScale;
                var cands = GetBlueCandidatesFor(g, taken, out willSwapScale, blues);
                if (cands.Count == 0)
                {
                    if (logWhenNoCandidate)
                        Debug.Log($"[PegRandomizer] No BLUE candidate for GREEN '{g.name}' (group={GetGroup(g.transform)}, family={GetFamily(g.transform)})");
                    continue;
                }

                Shuffle(cands, rng);

                // pilih region yang masih butuh hijau (proporsional BLUE)
                int chooseRegion = -1;
                for (int r = 0; r < REGIONS; r++)
                {
                    if (targetGreenPerRegion[r] > 0) { chooseRegion = r; break; }
                }

                var filtered = cands;
                if (chooseRegion >= 0)
                {
                    var f = cands.Where(c => RegionIndex(c.transform, yMin, yMax, REGIONS) == chooseRegion).ToList();
                    if (f.Count > 0) filtered = f;
                }

                var chosen = filtered[0];
                taken.Add(chosen);

                bool forceScale = swapScaleIfSameFamily && willSwapScale;
                ApplySwap(g.transform, chosen.transform, forceScale);

                int br = RegionIndex(chosen.transform, yMin, yMax, REGIONS);
                if (br >= 0 && br < REGIONS)
                    targetGreenPerRegion[br] = Mathf.Max(0, targetGreenPerRegion[br] - 1);
            }
        }

        return HashOrangeLayout(all);
    }

    Transform GetSwapScope(Transform t)
    {
        // jalan naik ke atas sampai ketemu parent yang punya SwapScope
        Transform p = t;
        while (p != null)
        {
            if (p.GetComponent<SwapScope>() != null) return p;
            p = p.parent;
        }
        return null; // tidak berada di scope khusus
    }

    void Done()
    {
        IsRandomizing = false;
        HasRunThisScene = true;
        LastRunFrame = Time.frameCount;
        OnRandomizeDone?.Invoke();
    }

    int RegionIndex(Transform t, float yMin, float yMax, int regions)
    {
        float y = t.position.y;
        float norm = (y - yMin) / Mathf.Max(0.0001f, (yMax - yMin));
        return Mathf.Clamp(Mathf.FloorToInt(norm * regions), 0, regions - 1);
    }

    public static void ResetRunGuards()
    {
        IsRandomizing = false;
        HasRunThisScene = false;
        LastRunFrame = -1;
        OnRandomizeDone = null;
        _lastLayoutHash = 0; // reset anti-repeat
    }
}
