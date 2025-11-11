using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

/*  ReactionExecutorV2
 *  - WindRain (Water+Wind): impact VFX + rain VFX (miring), spawn N bola netral mental acak
 *  - MudRain  (Water+Earth): TANPA PhysicsMaterial; pakai MudBallModifier (script-only)
 *      • gravityScale ↑, drag ↑, setiap tabrakan velocity *= collisionDamping (bounce kecil)
 *      • Rain VFX PERSIST: tetap aktif selama X tembakan berikutnya (default 2),
 *        dan counter BARU berkurang setelah turn (semua bola) selesai.
 *      • ★ Global modifiers: MudMoveSpeedMultiplier (perlambat mover/rotator) & HardPegHitsOverride (hard peg jadi 1x)
 *  - Tornado (Wind+Earth):
 *      • Tampilkan VFX tornado + VFX angin (opsional) selama tornadoDuration
 *      • Overlay abu-abu: fade in → hold → fade out, sinkron dengan durasi tornado
 *      • Di pertengahan durasi (atau waktu custom), acak posisi N peg (permute di antara mereka)
 *      • Menahan end-turn sampai tornado selesai (via RequestEndTurnDelay); overlay diset supaya hilang bareng.
 *  End-turn: selain Tornado, sistem tetap menunggu SEMUA bola habis (tak ada hold manual).
 */
public class ReactionExecutorV2 : MonoBehaviour
{
    /* ───────── VFX Sorting (opsional) ───────── */
    [Header("VFX Sorting (optional)")]
    [SerializeField] string vfxSortingLayer = "Default";
    [SerializeField] int vfxSortingOrder = 20;

    /* ───────── Wind Rain ───────── */
    [Header("Wind Rain")]
    [Tooltip("Prefab bola yang sama dengan yang dipakai Launcher.")]
    [SerializeField] BallController ballPrefab;

    [Tooltip("VFX impact di titik kejadian (one-shot).")]
    [SerializeField] GameObject impactVfxPrefab;
    [SerializeField] float impactVfxScale = 1f;

    [Tooltip("VFX hujan area (visual saja).")]
    [SerializeField] GameObject rainAreaVfxPrefab;
    [SerializeField] float rainAreaScale = 1f;
    [SerializeField] float rainDuration = 3f;
    [SerializeField] float rainYOffset = 0f;

    [Header("Rain Tilt")]
    [Tooltip("Kemiringan hujan (derajat). Negatif = miring ke kanan-bawah.")]
    [SerializeField] float rainAngleDeg = -25f;
    [Tooltip("Paksa semua ParticleSystem hujan ke Simulation Space = Local agar rotasi berpengaruh.")]
    [SerializeField] bool forceRainLocalSpace = true;

    [Header("Extra Balls")]
    [SerializeField] int extraBalls = 3;
    [SerializeField] float speedMin = 6f;
    [SerializeField] float speedMax = 9f;
    [SerializeField] float angularVelRange = 360f;
    [SerializeField] float angleJitter = 18f;

    /* ───────── Mud Rain ───────── */
    [Header("Mud Rain")]
    [Tooltip("VFX impact untuk mud (one-shot).")]
    [SerializeField] GameObject mudImpactVfxPrefab;
    [SerializeField] float mudImpactScale = 1f;

    [Tooltip("VFX hujan lumpur area (loop, visual).")]
    [SerializeField] GameObject mudRainVfxPrefab;
    [SerializeField] float mudRainScale = 1f;
    [SerializeField] float mudRainYOffset = 0f;
    [SerializeField] float mudRainAngleDeg = -15f;
    [SerializeField] bool forceMudLocalSpace = true;

    [Header("Mud Rain Physics (Script-only, NO PhysMaterial2D)")]
    [Tooltip("Perkalian gravitasi untuk bola penembak (lebih berat). 1 = tidak berubah.")]
    [SerializeField] float mudGravityMultiplier = 1.8f;
    [Tooltip("Tambah drag linear agar laju terasa lebih seret.")]
    [SerializeField] float mudExtraLinearDrag = 0.06f;
    [Tooltip("Tambah drag sudut (putaran) agar cepat mereda.")]
    [SerializeField] float mudExtraAngularDrag = 0.15f;
    [Tooltip("Dikalikan pada velocity setiap tabrakan. 0.6–0.85 enak untuk 'empuk'.")]
    [SerializeField, Range(0.5f, 1f)] float mudCollisionDamping = 0.75f;

    [Header("Mud Rain Persistence")]
    [Tooltip("Berapa kali tembakan BERIKUTNYA rain tetap ada (tidak termasuk tembakan yang memicu).")]
    [SerializeField, Min(0)] int mudPersistShots = 2;
    [Tooltip("Batas waktu darurat (detik) kalau tidak ada tembakan lagi, 0=tak terbatas.")]
    [SerializeField] float mudRainMaxLifetime = 0f;

    /* ───────── Tornado ───────── */
    [Header("Tornado")]
    [Tooltip("VFX tornado (loop selama durasi).")]
    [SerializeField] GameObject tornadoVfxPrefab;
    [SerializeField] float tornadoVfxScale = 1.0f;
    [SerializeField] bool tornadoIgnoreMovingAndRotatingPegs = true;   // ⬅️ ADD
    [SerializeField] bool tornadoAlsoShuffleStatics = true;         // acak peg statis global?
    [SerializeField] bool tornadoSwapWithinRotGroups = true;         // swap di dalam RotatingArea yang sama
    [SerializeField] bool tornadoSwapWithinMoverGroups = true;

    [Tooltip("Tambahan VFX garis angin untuk sapuan horizontal saat tornado (opsional).")]
    [SerializeField] GameObject tornadoWindVfxPrefab;
    [SerializeField] bool tornadoWindAttachToCamera = true;
    [SerializeField] Vector3 tornadoWindOffset = Vector3.zero;
    [SerializeField] float tornadoWindScale = 1.0f;

    [Tooltip("Durasi total efek tornado (detik).")]
    [SerializeField] float tornadoDuration = 4.0f;
    [Tooltip("Waktu (detik) setelah mulai saat peg diacak. Default pertengahan durasi.")]
    [SerializeField] float tornadoShuffleTime = -1f; // -1 => 0.5 * duration
    [Tooltip("Jumlah peg yang diacak posisinya (dipilih acak, lalu dipermutasikan).")]
    [SerializeField, Min(1)] int tornadoShuffleCount = 10;
    [Tooltip("Sedikit padding supaya end turn aman setelah VFX selesai.")]
    [SerializeField] float tornadoEndTurnPad = 0.1f;

    [Header("Transition Overlay (abu-abu)")]
    [Tooltip("CanvasGroup pada WindOverlay (UI Image full-screen).")]
    [SerializeField] CanvasGroup overlayGroup;
    [Tooltip("Opsional: Image di overlay agar warna bisa dipaksa abu-abu).")]
    [SerializeField] Image overlayImage;
    [SerializeField, Range(0f, 1f)] float overlayMaxAlpha = 0.35f;
    [SerializeField, Min(0.05f)] float overlayFadeIn = 0.12f;
    [SerializeField, Min(0.05f)] float overlayFadeOut = 0.25f;

    [Header("SFX Keys (optional)")]
    [SerializeField] string sfxWindImpact = "WindImpact";
    [SerializeField] string sfxWindRain = "WindRain";
    [SerializeField] string sfxMudImpact = "MudImpact";
    [SerializeField] string sfxMudRain = "MudRain";
    [SerializeField] string sfxTornadoStart = "TornadoStart";
    [SerializeField] string sfxTornadoLoop = "Tornado";
    [SerializeField] string sfxTornadoEnd = "TornadoEnd";
    [SerializeField] float sfxTornadoEstimatedLength = 0f; // extra hold jika SFX lebih lama

    /* ───────── internal ───────── */
    static readonly List<ReactionExecutorV2> s_instances = new List<ReactionExecutorV2>();
    GameObject activeMudRain;
    int shotsLeftForMudRain = 0;
    float mudRainStartTime = 0f;
    bool mudCountdownRunning = false; // mencegah dobel countdown

    // ★★★ GLOBAL MODIFIERS (dibaca oleh script lain) ★★★
    public static bool MudActive => s_mudShotsLeft > 0;
    public static float MudMoveSpeedMultiplier { get; private set; } = 1f; // kalikan kecepatan mover/rotator
    public static int? MudHardPegHitsOverride { get; private set; } = null; // 1 = hard peg cukup 1x
    static int s_mudShotsLeft = 0;

    /// <summary>Helper untuk baca jumlah hit efektif hard peg.</summary>
    public static int GetEffectiveHardPegHits(int defaultHits = 2)
        => MudHardPegHitsOverride.HasValue ? Mathf.Max(1, MudHardPegHitsOverride.Value) : defaultHits;

    void OnEnable() { ElementReactions.OnReaction += Handle; s_instances.Add(this); }
    void OnDisable() { ElementReactions.OnReaction -= Handle; s_instances.Remove(this); }

    void Update()
    {
        if (activeMudRain && mudRainMaxLifetime > 0f)
        {
            if (Time.time - mudRainStartTime > mudRainMaxLifetime)
                DestroyActiveMudRain();
        }
    }

    void Handle(ReactionType type, Vector2 at, BallController ball, ElementType a, ElementType b)
    {
        switch (type)
        {
            case ReactionType.WindRain: StartCoroutine(DoWindRain(at, ball)); break;
            case ReactionType.MudRain: StartCoroutine(DoMudRain(at, ball)); break;
            case ReactionType.Tornado: StartCoroutine(DoTornado(at)); break;
        }
    }

    /* ==================== WIND RAIN ==================== */
    IEnumerator DoWindRain(Vector2 at, BallController triggeringBall)
    {
        if (!string.IsNullOrEmpty(sfxWindImpact)) AudioManager.I.Play(sfxWindImpact, at);
        if (!string.IsNullOrEmpty(sfxWindRain)) AudioManager.I.Play(sfxWindRain, at);
        // 1) Impact VFX (sekali ledak)
        var impact = SpawnVFX(impactVfxPrefab, at, impactVfxScale, Quaternion.identity);
        ForceOneShot(impact);

        // 2) Rain Area VFX (visual saja) — DIMIRINGKAN
        Vector2 rainPos = new Vector2(at.x, at.y + rainYOffset);
        var rainRot = Quaternion.Euler(0f, 0f, rainAngleDeg);
        var rain = SpawnVFX(rainAreaVfxPrefab, rainPos, Mathf.Max(0.05f, rainAreaScale), rainRot);
        if (forceRainLocalSpace) ForceSimulationSpace(rain, ParticleSystemSimulationSpace.Local);

        // 3) Siapkan arah mental untuk (bola pemicu + ekstra)
        int total = Mathf.Max(1, extraBalls + 1);
        float baseAngle = Random.Range(0f, 360f);
        var dirs = new List<Vector2>(total);
        for (int i = 0; i < total; i++)
        {
            float ang = baseAngle + (360f / total) * i + Random.Range(-angleJitter, angleJitter);
            float r = ang * Mathf.Deg2Rad;
            dirs.Add(new Vector2(Mathf.Cos(r), Mathf.Sin(r)).normalized);
        }

        // 4) Bola pemicu
        if (triggeringBall)
        {
            triggeringBall.transform.position = at;
            float spd = Random.Range(speedMin, speedMax);
            triggeringBall.Shoot(dirs[0] * spd);

            var rb = triggeringBall.GetComponent<Rigidbody2D>();
            if (rb) rb.angularVelocity = Random.Range(-angularVelRange, angularVelRange);
        }

        // 5) Spawn bola ekstra
        for (int i = 1; i < total; i++)
        {
            var b = Instantiate(ballPrefab, at, Quaternion.identity);
            float spd = Random.Range(speedMin, speedMax);
            b.Shoot(dirs[i] * spd);

            var rb = b.GetComponent<Rigidbody2D>();
            if (rb) rb.angularVelocity = Random.Range(-angularVelRange, angularVelRange);
        }

        // 6) Bereskan VFX
        yield return WaitForParticles(impact);
        if (impact) Destroy(impact);

        if (rainDuration > 0f) { yield return new WaitForSeconds(rainDuration); }
        if (rain) Destroy(rain);
    }

    /* ==================== MUD RAIN (persist by shots) ==================== */
    IEnumerator DoMudRain(Vector2 at, BallController triggeringBall)
    {
        if (!string.IsNullOrEmpty(sfxMudImpact)) AudioManager.I.Play(sfxMudImpact, at);
        if (!string.IsNullOrEmpty(sfxMudRain)) AudioManager.I.Play(sfxMudRain, at);

        // 1) Impact VFX
        var impact = SpawnVFX(mudImpactVfxPrefab, at, mudImpactScale, Quaternion.identity);
        ForceOneShot(impact);

        // 2) Rain VFX persist
        if (activeMudRain) Destroy(activeMudRain);

        Vector2 pos = new Vector2(at.x, at.y + mudRainYOffset);
        var rot = Quaternion.Euler(0f, 0f, mudRainAngleDeg);
        activeMudRain = SpawnVFX(mudRainVfxPrefab, pos, Mathf.Max(0.05f, mudRainScale), rot);
        if (forceMudLocalSpace) ForceSimulationSpace(activeMudRain, ParticleSystemSimulationSpace.Local);

        shotsLeftForMudRain = Mathf.Max(0, mudPersistShots);
        mudRainStartTime = Time.time;

        // ★ Sinkronkan GLOBAL MODIFIERS
        SyncMudStaticFromInstance();

        // 3) Berat & bounce kecil (script-only) untuk bola pemicu
        if (triggeringBall)
        {
            var mod = triggeringBall.GetComponent<MudBallModifier>();
            if (!mod) mod = triggeringBall.gameObject.AddComponent<MudBallModifier>();

            mod.Apply(
                gravityMultiplier: Mathf.Max(0.01f, mudGravityMultiplier),
                extraLinearDrag: Mathf.Max(0f, mudExtraLinearDrag),
                extraAngularDrag: Mathf.Max(0f, mudExtraAngularDrag),
                collisionDamping: Mathf.Clamp(mudCollisionDamping, 0.5f, 1f)
            );
        }

        // 4) Bereskan impact
        yield return WaitForParticles(impact);
        if (impact) Destroy(impact);
    }

    /* ==================== TORNADO (dengan overlay + wind-lines) ==================== */
    IEnumerator DoTornado(Vector2 at)
    {
        // ⬅ UPDATE: delay end-turn = fadeIn + durasi tornado + fadeOut + padding
        float totalDelay =
            Mathf.Max(0f, overlayFadeIn) +
            Mathf.Max(0f, tornadoDuration) +
            Mathf.Max(0f, overlayFadeOut) +
            Mathf.Max(0f, tornadoEndTurnPad);

        if (GameManager.Instance)
            GameManager.Instance.SendMessage("RequestEndTurnDelay", totalDelay, SendMessageOptions.DontRequireReceiver);
        if (!string.IsNullOrEmpty(sfxTornadoStart)) AudioManager.I.Play(sfxTornadoStart, at);
        if (!string.IsNullOrEmpty(sfxTornadoLoop)) AudioManager.I.Play(sfxTornadoLoop, at);

        // 1) Mulai overlay (fade in → hold → fade out) sinkron dgn durasi tornado
        Coroutine overlayCo = null;
        if (overlayGroup)
            overlayCo = StartCoroutine(CoOverlaySequence(tornadoDuration));

        // 2) Spawn VFX tornado (loop selama durasi)
        var tornado = SpawnVFX(
            tornadoVfxPrefab,
            at,
            Mathf.Max(0.05f, tornadoVfxScale),
            Quaternion.identity,
            keepPrefabRotationAndScale: true
        );

        // 3) Spawn VFX angin tambahan (opsional)
        GameObject wind = null;
        if (tornadoWindVfxPrefab)
        {
            Vector3 windPos = at;
            if (tornadoWindAttachToCamera && Camera.main)
            {
                windPos = Camera.main.transform.position + tornadoWindOffset;
                wind = SpawnVFX(tornadoWindVfxPrefab, windPos, Mathf.Max(0.05f, tornadoWindScale), Quaternion.identity, true);
                wind.transform.SetParent(Camera.main.transform, true); // ikut kamera
            }
            else
            {
                windPos += tornadoWindOffset;
                wind = SpawnVFX(tornadoWindVfxPrefab, windPos, Mathf.Max(0.05f, tornadoWindScale), Quaternion.identity, true);
            }
        }

        // 4) Tunggu sampai waktu shuffle (default pertengahan)
        float tShuffle = (tornadoShuffleTime > 0f) ? tornadoShuffleTime : tornadoDuration * 0.5f;
        tShuffle = Mathf.Clamp(tShuffle, 0f, tornadoDuration);
        if (tShuffle > 0f) yield return new WaitForSeconds(tShuffle);

        // 5) ACak posisi peg-peg (permute di antara mereka)
        ShuffleSomePegs(tornadoShuffleCount);

        // 6) Tunggu sisa durasi
        float remain = Mathf.Max(0f, tornadoDuration - tShuffle);
        if (remain > 0f) yield return new WaitForSeconds(remain);

        // 7) Bereskan VFX
        if (tornado) Destroy(tornado);
        if (wind) Destroy(wind);

        if (!string.IsNullOrEmpty(sfxTornadoEnd)) AudioManager.I.Play(sfxTornadoEnd, at);

        // 8) Pastikan overlay selesai fade-out
        if (overlayCo != null) yield return overlayCo;
    }

    // Overlay sequence: fade in → hold → fade out
    IEnumerator CoOverlaySequence(float totalDuration)
    {
        float inDur = Mathf.Max(0.01f, overlayFadeIn);
        float outDur = Mathf.Max(0.01f, overlayFadeOut);
        float hold = Mathf.Max(0f, totalDuration - inDur - outDur);

        if (overlayGroup)
        {
            overlayGroup.gameObject.SetActive(true);
            overlayGroup.blocksRaycasts = true;

            // pastikan warna abu-abu (jika image ada dan masih hitam penuh)
            if (overlayImage)
            {
                var c = overlayImage.color;
                if (Mathf.Approximately(c.r, 0f) && Mathf.Approximately(c.g, 0f) && Mathf.Approximately(c.b, 0f))
                {
                    c.r = c.g = c.b = 0.25f;
                    overlayImage.color = c;
                }
            }

            yield return FadeOverlay(0f, overlayMaxAlpha, inDur);
            if (hold > 0f) yield return new WaitForSeconds(hold);
            yield return FadeOverlay(overlayMaxAlpha, 0f, outDur);

            overlayGroup.blocksRaycasts = false;
            overlayGroup.gameObject.SetActive(false);
        }
    }

    IEnumerator FadeOverlay(float from, float to, float duration)
    {
        float t = 0f;
        overlayGroup.alpha = from;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            overlayGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        overlayGroup.alpha = to;
    }

    // Pilih hingga N peg aktif lalu permutasikan posisi mereka
    void ShuffleSomePegs(int count)
    {
        if (count <= 1) return;

        // Kumpulkan semua Peg aktif
        var all = new List<PegController>(FindObjectsOfType<PegController>(true));
        all.RemoveAll(p => !p || !p.gameObject.activeInHierarchy);
        if (all.Count < 2) return;

        // Kategorikan
        var staticList = new List<PegController>();
        var rotGroups = new Dictionary<RotatingArea, List<PegController>>();
        var moverGroups = new Dictionary<string, List<PegController>>(); // key = signature jalur

        foreach (var p in all)
        {
            var rot = p.transform.GetComponentInParent<RotatingArea>(true);
            if (rot && tornadoSwapWithinRotGroups)
            {
                if (!rotGroups.TryGetValue(rot, out var list)) { list = new List<PegController>(); rotGroups[rot] = list; }
                list.Add(p);
                continue;
            }

            var mv = p.GetComponent<PegMover>();
            if (mv && tornadoSwapWithinMoverGroups)
            {
                string key = GetMoverGroupKey(mv);
                if (!string.IsNullOrEmpty(key))
                {
                    if (!moverGroups.TryGetValue(key, out var list)) { list = new List<PegController>(); moverGroups[key] = list; }
                    list.Add(p);
                    continue;
                }
            }

            if (tornadoAlsoShuffleStatics)
                staticList.Add(p);
        }

        int remaining = Mathf.Min(count, all.Count);

        // 1) Static global
        if (remaining > 0 && staticList.Count >= 2)
        {
            int take = Mathf.Min(remaining, staticList.Count);
            PermuteWorldPositions(staticList, take);
            remaining -= take;
        }

        // 2) Rotating groups (swap local pos relatif ke root RotatingArea)
        if (remaining > 0)
        {
            // acak urutan grup supaya merata
            var rotKeys = new List<RotatingArea>(rotGroups.Keys);
            ShuffleList(rotKeys);
            foreach (var rg in rotKeys)
            {
                var list = rotGroups[rg];
                if (list.Count < 2) continue;
                if (remaining <= 0) break;

                int take = Mathf.Min(remaining, list.Count);
                PermuteLocalPositionsUnderRoot(rg.transform, list, take);
                remaining -= take;
            }
        }

        // 3) PegMover groups (Waypoints) → swap fase + posisi
        if (remaining > 0)
        {
            var mvKeys = new List<string>(moverGroups.Keys);
            ShuffleList(mvKeys);
            foreach (var key in mvKeys)
            {
                var list = moverGroups[key];
                if (list.Count < 2) continue;
                if (remaining <= 0) break;

                int take = Mathf.Min(remaining, list.Count);
                PermuteMoverStates(list, take);         // aman untuk MoveMode.Waypoints
                remaining -= take;
            }
        }
    }

    /* ============================ Helpers ============================ */

    // Static: permutasikan 'take' world position
    void PermuteWorldPositions(List<PegController> src, int take)
    {
        var pool = new List<PegController>(src);
        ShuffleList(pool);
        var sel = pool.GetRange(0, take);

        var pos = new Vector3[sel.Count];
        for (int i = 0; i < sel.Count; i++) pos[i] = sel[i].transform.position;
        ShuffleArray(pos);
        for (int i = 0; i < sel.Count; i++) sel[i].transform.position = pos[i];
    }

    // RotatingArea: simpan localPos relatif ke root, permutasikan, lalu apply
    void PermuteLocalPositionsUnderRoot(Transform root, List<PegController> group, int take)
    {
        var pool = new List<PegController>(group);
        ShuffleList(pool);
        var sel = pool.GetRange(0, take);

        var locals = new Vector3[sel.Count];
        for (int i = 0; i < sel.Count; i++)
            locals[i] = root.InverseTransformPoint(sel[i].transform.position);

        ShuffleArray(locals);

        for (int i = 0; i < sel.Count; i++)
            sel[i].transform.position = root.TransformPoint(locals[i]);
    }

    // PegMover grouping key: Waypoints → signature daftar Transform; PingPong → kosong (di-skip)
    string GetMoverGroupKey(PegMover mv)
    {
        // Hanya dukung Waypoints agar aman
        try
        {
            var mode = mv.mode; // public
            if (mode.ToString() != "Waypoints") return null;

            // Ambil field 'waypoints' (public atau non-public)
            var fi = typeof(PegMover).GetField("waypoints", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var arr = fi != null ? fi.GetValue(mv) as Transform[] : null;
            if (arr == null || arr.Length < 2) return null;

            // Signature: gabungan instanceID waypoint (urutan penting)
            System.Text.StringBuilder sb = new System.Text.StringBuilder("WP:");
            for (int i = 0; i < arr.Length; i++)
            {
                int id = arr[i] ? arr[i].GetInstanceID() : 0;
                sb.Append(id).Append('|');
            }
            // Bedakan juga looping vs tidak, biar benar-benar set yang sama
            var fLoop = typeof(PegMover).GetField("loop", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fLoop != null) sb.Append("L=").Append((bool)fLoop.GetValue(mv) ? "1" : "0");

            return sb.ToString();
        }
        catch { return null; }
    }

    // Capture/apply state private PegMover via reflection (khusus Waypoints)
    struct MoverState
    {
        public Vector3 pos;
        public int segFrom, segTo, dir;
        public float tOnSeg;
    }

    static readonly FieldInfo FI_segFrom = typeof(PegMover).GetField("_segFrom", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo FI_segTo = typeof(PegMover).GetField("_segTo", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo FI_dir = typeof(PegMover).GetField("_dir", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo FI_tOnSeg = typeof(PegMover).GetField("_tOnSegment", BindingFlags.Instance | BindingFlags.NonPublic);

    MoverState CaptureMoverState(PegMover mv, Transform t)
    {
        MoverState s = new MoverState
        {
            pos = t.position,
            segFrom = FI_segFrom != null ? (int)FI_segFrom.GetValue(mv) : 0,
            segTo = FI_segTo != null ? (int)FI_segTo.GetValue(mv) : 0,
            dir = FI_dir != null ? (int)FI_dir.GetValue(mv) : 1,
            tOnSeg = FI_tOnSeg != null ? (float)FI_tOnSeg.GetValue(mv) : 0f,
        };
        return s;
    }

    void ApplyMoverState(PegMover mv, Transform t, MoverState s)
    {
        if (FI_segFrom != null) FI_segFrom.SetValue(mv, s.segFrom);
        if (FI_segTo != null) FI_segTo.SetValue(mv, s.segTo);
        if (FI_dir != null) FI_dir.SetValue(mv, s.dir);
        if (FI_tOnSeg != null) FI_tOnSeg.SetValue(mv, s.tOnSeg);
        t.position = s.pos;
    }

    // Permutasikan state mover di dalam grup waypoint yang sama
    void PermuteMoverStates(List<PegController> group, int take)
    {
        var pool = new List<PegController>(group);
        ShuffleList(pool);
        var sel = pool.GetRange(0, take);

        var states = new MoverState[sel.Count];
        for (int i = 0; i < sel.Count; i++)
            states[i] = CaptureMoverState(sel[i].GetComponent<PegMover>(), sel[i].transform);

        ShuffleArray(states);

        for (int i = 0; i < sel.Count; i++)
            ApplyMoverState(sel[i].GetComponent<PegMover>(), sel[i].transform, states[i]);
    }

    // Utils – shuffle
    void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
    void ShuffleArray<T>(T[] arr)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            int j = Random.Range(i, arr.Length);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
    }

    bool IsDynamicPeg(PegController p)
    {
        if (!p) return false;

        // Punya mover sendiri? (ping-pong/waypoints)
        if (p.GetComponent<PegMover>() != null) return true;

        // Anak dari grup rotasi/orbit/sweep?
        if (p.transform.GetComponentInParent<RotatingArea>(true) != null) return true;

        // (opsional) kalau suatu peg punya RigidBody2D dinamis khusus, kamu bisa tambahkan cek di sini

        return false;
    }


    /* ───────── Shots bookkeeping (Mud Rain) ───────── */
    void OnShotFired_DecrementAfterTurn()
    {
        if (shotsLeftForMudRain <= 0) return;
        if (mudCountdownRunning) return;
        StartCoroutine(DecrementMudAfterTurnEnd());
    }

    IEnumerator DecrementMudAfterTurnEnd()
    {
        mudCountdownRunning = true;

        // Tunggu sampai turn ini benar-benar "berjalan" (ada bola aktif)
        while (BallController.ActiveBalls <= 0) yield return null;
        // Lalu tunggu sampai SEMUA bola habis
        while (BallController.ActiveBalls > 0) yield return null;

        shotsLeftForMudRain--;
        mudCountdownRunning = false;

        // ★ Sinkronkan GLOBAL MODIFIERS
        SyncMudStaticFromInstance();

        if (shotsLeftForMudRain <= 0)
            DestroyActiveMudRain();
    }

    void DestroyActiveMudRain()
    {
        if (activeMudRain)
        {
            Destroy(activeMudRain);
            activeMudRain = null;
        }
        shotsLeftForMudRain = 0;

        // ★ Sinkronkan GLOBAL MODIFIERS (reset ke normal)
        SyncMudStaticFromInstance();
    }

    /// <summary>
    /// Panggil ini dari Launcher SETIAP kali pemain menembak (sekali per aksi shoot).
    /// Counter MudRain akan berkurang SETELAH turn selesai.
    /// </summary>
    public static void RegisterShotFired()
    {
        for (int i = 0; i < s_instances.Count; i++)
            if (s_instances[i]) s_instances[i].OnShotFired_DecrementAfterTurn();
    }

    /* ───────── Helpers ───────── */
    GameObject SpawnVFX(
        GameObject prefab,
        Vector2 pos,
        float scale,
        Quaternion rot,
        bool keepPrefabRotationAndScale = false
    )
    {
        if (!prefab) return null;

        // Pakai rotasi prefab jika diminta, selain itu pakai rotasi dari argumen
        var useRot = keepPrefabRotationAndScale ? prefab.transform.rotation : rot;
        var go = Instantiate(prefab, pos, useRot);

        // Pakai scale prefab sebagai basis, lalu dikalikan "scale" dari inspector
        Vector3 baseScale = keepPrefabRotationAndScale ? prefab.transform.localScale : Vector3.one;
        go.transform.localScale = baseScale * Mathf.Max(0.0001f, scale);

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.sortingLayerName = vfxSortingLayer;
            r.sortingOrder = vfxSortingOrder;
        }
        return go;
    }

    // Paksa semua ParticleSystem jadi one-shot (tanpa loop)
    void ForceOneShot(GameObject root)
    {
        if (!root) return;
        var systems = root.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            var main = ps.main; main.loop = false;
            var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;
#if UNITY_2018_3_OR_NEWER
            for (int i = 0; i < em.burstCount; i++)
            {
                var b = em.GetBurst(i);
                b.cycleCount = 1;
                b.repeatInterval = 9999f;
                em.SetBurst(i, b);
            }
#endif
        }
    }

    // Pastikan rotasi transform memiringkan arah partikel
    void ForceSimulationSpace(GameObject root, ParticleSystemSimulationSpace space)
    {
        if (!root) return;
        var systems = root.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            var main = ps.main;
            main.simulationSpace = space; // Local agar rotasi transform terpakai
        }
    }

    float EstimateParticlesDuration(GameObject root)
    {
        if (!root) return 0f;
        float max = 0f;
        var psAll = root.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in psAll)
        {
            var main = ps.main;
            float dur = main.duration;
            float life = main.startLifetime.mode switch
            {
                ParticleSystemCurveMode.TwoConstants => main.startLifetime.constantMax,
                ParticleSystemCurveMode.TwoCurves => main.startLifetime.curveMax.Evaluate(1f),
                _ => main.startLifetime.constant
            };
            max = Mathf.Max(max, dur + life);
        }
        return max;
    }

    IEnumerator WaitForParticles(GameObject root)
    {
        float wait = EstimateParticlesDuration(root);
        if (wait > 0f) yield return new WaitForSeconds(wait + 0.05f);
    }

    // ★ Sinkronisasi global-modifier berdasarkan instance ini
    void SyncMudStaticFromInstance()
    {
        s_mudShotsLeft = shotsLeftForMudRain;
        if (s_mudShotsLeft > 0)
        {
            MudMoveSpeedMultiplier = 0.5f;   // << ubah multiplier lambat di sini (Inspector alternatif: expose)
            MudHardPegHitsOverride = 1;      // << hard peg cukup 1x selama aktif
        }
        else
        {
            MudMoveSpeedMultiplier = 1f;
            MudHardPegHitsOverride = null;
        }
    }
}

/* ────────────────────────────────────────────────────────────────
 * Komponen sementara untuk Mud Rain (TANPA PhysicsMaterial2D)
 *  - dipasang runtime pada bola penembak
 *  - murni script: gravityScale↑, drag↑, dan meredam velocity saat tabrakan
 *  - tidak perlu revert karena bola akan hancur di akhir turn
 * ────────────────────────────────────────────────────────────────*/
[DisallowMultipleComponent]
public class MudBallModifier : MonoBehaviour
{
    Rigidbody2D rb;
    float collisionDamping = 0.8f;
    float addLinearDrag = 0.05f;
    float addAngularDrag = 0.1f;
    float gravityMultiplier = 1.6f;
    bool applied = false;

    void Awake() => rb = GetComponent<Rigidbody2D>();

    public void Apply(float gravityMultiplier, float extraLinearDrag, float extraAngularDrag, float collisionDamping)
    {
        this.gravityMultiplier = gravityMultiplier;
        this.addLinearDrag = extraLinearDrag;
        this.addAngularDrag = extraAngularDrag;
        this.collisionDamping = collisionDamping;

        if (rb && !applied)
        {
            rb.gravityScale *= this.gravityMultiplier;
            rb.drag += this.addLinearDrag;
            rb.angularDrag += this.addAngularDrag;
            applied = true;
        }
    }

    void OnCollisionEnter2D(Collision2D _)
    {
        if (!rb) return;
        rb.velocity *= collisionDamping; // empukkan pantulan di setiap tabrakan
    }
}
