using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReactionExecutor : MonoBehaviour
{
    /*──────────────────── VFX Umum ────────────────────*/
    [Header("Sorting untuk semua VFX (opsional)")]
    [SerializeField] string vfxSortingLayer = "Default";
    [SerializeField] int vfxSortingOrder = 20;

    /*──────────────────── STEAM ────────────────────*/
    [Header("Steam VFX (Fire + Water)")]
    [SerializeField] GameObject steamVFXPrefab;
    [SerializeField] float steamVfxScale = 1.0f;
    [SerializeField] float steamRadius = 2.75f;
    [SerializeField] string sfxSteam = "Steam";
    [Tooltip("Perkiraan durasi SFX Steam untuk menahan end-turn.")]
    [SerializeField] float sfxSteamEstimatedLength = 2.0f;

    [Header("Steam: End Turn Hold")]
    [SerializeField] bool delayEndTurnOnSteam = true;
    [SerializeField] float minHoldSeconds = 2.0f;

    /*──────────────────── FIRESTORM ────────────────────*/
    [Header("Firestorm (Fire + Wind)")]
    [Tooltip("Prefab visual jalur api (opsional, hanya untuk tampilan).")]
    [SerializeField] GameObject fireLanePrefab;

    [Tooltip("TINGGI VISUAL (skala Y) – tidak mempengaruhi hit.")]
    [SerializeField] float fireLaneVfxHeight = 1.0f;

    [Tooltip("TINGGI AREA HIT (tidak mengubah visual).")]
    [SerializeField] float fireLaneHitHeight = 3.6f;

    [Tooltip("SKALA LEBAR AREA HIT relatif terhadap lebar visual (grow). 1 = sama.")]
    [Range(0.2f, 3.0f)]
    [SerializeField] float fireLaneHitWidthScale = 1.0f;

    [Tooltip("Panjang maksimum PER SISI (world units).")]
    [SerializeField] float fireLaneMaxLength = 10f;

    [Tooltip("Waktu memanjang 0 → maxLength (detik).")]
    [SerializeField] float fireLaneGrowDuration = 1.3f;

    [Tooltip("Diam setelah memanjang (detik).")]
    [SerializeField] float fireLaneHoldDuration = 2f;

    [Tooltip("Geser posisi Firestorm vertikal (negatif = turun).")]
    [SerializeField] float fireLaneYOffset = -0.25f;

    [Tooltip("Anchor area hit ke bawah (dasar api). Kalau aktif, menaikkan tinggi hanya menambah ke atas).")]
    [SerializeField] bool fireLaneAnchorBottom = true;

    [SerializeField] string sfxFirestorm = "Firestorm";
    [Tooltip("Tambahan hold berdasarkan estimasi partikel (opsional).")]
    [SerializeField] float fireVfxExtraHold = 0f;

    [Header("Firestorm Shape Controls")]
    [Range(0.1f, 0.9f)]
    [SerializeField] float topCurve = 0.40f;
    [Range(0f, 0.4f)]
    [SerializeField] float edgeTaper = 0.12f;

    /*──────────────────── FLAME STRIKE ────────────────────*/
    [Header("Flame Strike (Fire + Earth)")]
    [SerializeField] GameObject flameVfxPrefab;
    [SerializeField] GameObject explosionVfxPrefab;
    [SerializeField] float flameDuration = 2.0f;
    [SerializeField] float flameSpeed = 3.5f;
    [SerializeField] float flameWidth = 1.2f;
    [SerializeField] float flameLength = 2.0f;
    [SerializeField] float flameYOffset = 0.0f;
    [SerializeField] float flameVfxRotationZ = -90f;
    [SerializeField] float flameHitboxYOffset = 0.35f;

    [SerializeField] string sfxFlame = "Flame";
    [SerializeField] string sfxExplosion = "Explosion";
    [SerializeField] float explosionRadius = 2.2f;

    [Header("Flame Strike: Hold & VFX")]
    [SerializeField] float explosionHoldMax = 1.0f;   // batasi delay end-turn karena ledakan
    [SerializeField] float endTurnPad = 0.10f;        // padding kecil agar aman
    [SerializeField] float explosionVfxScale = 1.0f;  // skala VISUAL ledakan (tidak mengubah radius hit)

    /*──────────────────── Collision & Debug ────────────────────*/
    [Header("Collision Filter")]
    [SerializeField] LayerMask pegMask = ~0;

    [Header("Debug")]
    [SerializeField] bool debugDrawHit = false;

    /* internal */
    PolygonCollider2D queryPoly;
    readonly List<Collider2D> queryResults = new List<Collider2D>(64);
    ContactFilter2D filter;
    bool flameStrikeActive = false; // guard

    /*──────────────────── Listener ────────────────────*/
    void OnEnable()
    {
        ElementReactions.OnReaction += Handle;
        EnsureQueryPolygon();
    }
    void OnDisable() => ElementReactions.OnReaction -= Handle;

    void Handle(ReactionType type, Vector2 at, BallController ball, ElementType a, ElementType b)
    {
        switch (type)
        {
            case ReactionType.Steam:
                StartCoroutine(DoSteam(at, ball));
                break;
            case ReactionType.Firestorm:
                StartCoroutine(DoFirestorm(at));
                break;
            case ReactionType.FlameStrike:
                if (!flameStrikeActive) StartCoroutine(DoFlameStrike(at, ball));
                break;
        }
    }

    /*══════════════════════ STEAM ══════════════════════*/
    IEnumerator DoSteam(Vector2 at, BallController ball)
    {
        if (!string.IsNullOrEmpty(sfxSteam))
            AudioManager.I.Play(sfxSteam, at);

        var vfx = SpawnVFX(steamVFXPrefab, at, steamVfxScale, Quaternion.identity);
        float vfxDuration = EstimateParticlesDuration(vfx);

        if (delayEndTurnOnSteam)
        {
            float hold = Mathf.Max(minHoldSeconds, sfxSteamEstimatedLength, vfxDuration);
            GameManager.Instance?.SendMessage("RequestEndTurnDelay", hold, SendMessageOptions.DontRequireReceiver);
        }

        foreach (var col in Physics2D.OverlapCircleAll(at, steamRadius, pegMask))
        {
            var peg = col.GetComponentInParent<PegController>();
            if (peg != null)
            {
                peg.SimulateHitFromExplosion();
                GameManager.Instance?.RegisterIndirectPegHit(peg);
            }
        }

        if (ball) Destroy(ball.gameObject);

        yield return WaitForParticles(vfx);
        if (vfx) Destroy(vfx);
    }

    /*══════════ FIRESTORM ══════════*/
    IEnumerator DoFirestorm(Vector2 at)
    {
        float baselineY = at.y + fireLaneYOffset;

        if (!string.IsNullOrEmpty(sfxFirestorm))
            AudioManager.I.Play(sfxFirestorm, at);

        float totalHold = fireLaneGrowDuration + fireLaneHoldDuration + Mathf.Max(0f, fireVfxExtraHold);
        GameManager.Instance?.SendMessage("RequestEndTurnDelay", totalHold, SendMessageOptions.DontRequireReceiver);

        var root = new GameObject("FirestormRoot").transform;
        root.position = new Vector2(at.x, baselineY);

        Transform leftVfx = null, rightVfx = null;
        if (fireLanePrefab)
        {
            var leftGO = new GameObject("FireLaneVFX_Left"); leftGO.transform.SetParent(root, true); leftGO.transform.position = new Vector2(at.x, baselineY);
            var rightGO = new GameObject("FireLaneVFX_Right"); rightGO.transform.SetParent(root, true); rightGO.transform.position = new Vector2(at.x, baselineY); rightGO.transform.localEulerAngles = new Vector3(0f, 180f, 0f);

            leftVfx = Instantiate(fireLanePrefab, leftGO.transform).transform;
            rightVfx = Instantiate(fireLanePrefab, rightGO.transform).transform;
            leftVfx.localPosition = rightVfx.localPosition = Vector3.zero;

            ApplySortingToChildren(leftVfx.gameObject, vfxSortingLayer, vfxSortingOrder);
            ApplySortingToChildren(rightVfx.gameObject, vfxSortingLayer, vfxSortingOrder);
        }

        var processed = new HashSet<PegController>();

        // GROW
        float t = 0f;
        while (t < fireLaneGrowDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fireLaneGrowDuration);
            float visLen = EaseOutCubic(0f, fireLaneMaxLength, k);
            float hitWidth = visLen * 2f * Mathf.Max(0.2f, fireLaneHitWidthScale);

            StretchVfx(leftVfx, visLen, fireLaneVfxHeight);
            StretchVfx(rightVfx, visLen, fireLaneVfxHeight);

            HitFlamePolygon(baselineY, at.x, hitWidth, fireLaneHitHeight, processed);
            yield return null;
        }

        // HOLD
        float maxVisLen = fireLaneMaxLength;
        float maxHitW = maxVisLen * 2f * Mathf.Max(0.2f, fireLaneHitWidthScale);
        float holdT = 0f;

        while (holdT < fireLaneHoldDuration)
        {
            holdT += Time.deltaTime;
            StretchVfx(leftVfx, maxVisLen, fireLaneVfxHeight);
            StretchVfx(rightVfx, maxVisLen, fireLaneVfxHeight);
            HitFlamePolygon(baselineY, at.x, maxHitW, fireLaneHitHeight, processed);
            yield return null;
        }

        if (root) Destroy(root.gameObject);
    }

    /*══════════ FLAME STRIKE: turun → explode (one-shot) ══════════*/
    IEnumerator DoFlameStrike(Vector2 at, BallController ball)
    {
        flameStrikeActive = true;

        // Hancurkan bola pemicu
        if (ball) Destroy(ball.gameObject);

        // Flame turun
        if (!string.IsNullOrEmpty(sfxFlame)) AudioManager.I.Play(sfxFlame, at);

        Vector2 startPos = new Vector2(at.x, at.y + flameYOffset);
        var rot = Quaternion.Euler(0f, 0f, flameVfxRotationZ); // hadap bawah
        var flameGO = SpawnVFX(flameVfxPrefab, startPos, 1f, rot);
        var flameT = flameGO ? flameGO.transform : null;

        GameManager.Instance?.SendMessage("RequestEndTurnDelay", flameDuration, SendMessageOptions.DontRequireReceiver);

        var processed = new HashSet<PegController>();
        float t = 0f;
        while (t < flameDuration)
        {
            t += Time.deltaTime;

            if (flameT)
                flameT.position += Vector3.down * (flameSpeed * Time.deltaTime);

            // hitbox kecil nempel api
            Vector2 hbCenter =
                (flameT ? (Vector2)flameT.position : startPos)
                + Vector2.down * (flameHitboxYOffset + flameLength * 0.5f);
            Vector2 hbSize = new Vector2(Mathf.Max(0.05f, flameWidth), Mathf.Max(0.05f, flameLength));

            var hits = Physics2D.OverlapBoxAll(hbCenter, hbSize, 0f, pegMask);
            for (int i = 0; i < hits.Length; i++)
            {
                var peg = hits[i].GetComponentInParent<PegController>();
                if (peg != null && !processed.Contains(peg))
                {
                    processed.Add(peg);
                    peg.SimulateHitFromExplosion();
                    GameManager.Instance?.RegisterIndirectPegHit(peg);
                }
            }

            if (debugDrawHit)
            {
                Vector2 half = hbSize * 0.5f;
                Vector3 a = hbCenter + new Vector2(-half.x, -half.y);
                Vector3 b = hbCenter + new Vector2(+half.x, -half.y);
                Vector3 c = hbCenter + new Vector2(+half.x, +half.y);
                Vector3 d = hbCenter + new Vector2(-half.x, +half.y);
                Debug.DrawLine(a, b, Color.red, .02f);
                Debug.DrawLine(b, c, Color.red, .02f);
                Debug.DrawLine(c, d, Color.red, .02f);
                Debug.DrawLine(d, a, Color.red, .02f);
            }

            yield return null;
        }

        Vector2 endPos = flameT ? (Vector2)flameT.position : startPos;
        if (flameGO) Destroy(flameGO);

        // Explosion (ONE-SHOT)
        if (!string.IsNullOrEmpty(sfxExplosion)) AudioManager.I.Play(sfxExplosion, endPos);

        var boom = SpawnVFX(explosionVfxPrefab, endPos, Mathf.Max(0.05f, explosionVfxScale), Quaternion.identity);
        ForceOneShot(boom); // ← cegah redo / loop

        // hit radius (terpisah dari skala visual)
        var cols = Physics2D.OverlapCircleAll(endPos, Mathf.Max(0.05f, explosionRadius), pegMask);
        for (int i = 0; i < cols.Length; i++)
        {
            var peg = cols[i].GetComponentInParent<PegController>();
            if (peg != null)
            {
                peg.SimulateHitFromExplosion();
                GameManager.Instance?.RegisterIndirectPegHit(peg);
            }
        }

        // delay end-turn dibatasi
        float boomDur = Mathf.Max(0f, EstimateParticlesDuration(boom));
        float hold = Mathf.Min(boomDur, Mathf.Max(0f, explosionHoldMax)) + Mathf.Max(0f, endTurnPad);
        if (hold > 0f)
            GameManager.Instance?.SendMessage("RequestEndTurnDelay", hold, SendMessageOptions.DontRequireReceiver);

        yield return WaitForParticles(boom);
        if (boom) Destroy(boom);

        flameStrikeActive = false;
    }

    /*──────────────────── Helpers ────────────────────*/

    void EnsureQueryPolygon()
    {
        if (queryPoly != null) return;

        var go = new GameObject("FirestormQueryPolygon");
        go.hideFlags = HideFlags.HideInHierarchy;
        go.layer = LayerMask.NameToLayer("Ignore Raycast"); // ← tidak bisa kena ray/raycast 2D
        go.transform.SetParent(transform, false);

        queryPoly = go.AddComponent<PolygonCollider2D>();
        queryPoly.isTrigger = true;
        queryPoly.enabled = false; // ← off saat idle

        filter = new ContactFilter2D();
        filter.useTriggers = false;   // overlap hanya ke collider non-trigger
        filter.SetLayerMask(pegMask);
    }

    // Paksa semua ParticleSystem pada root menjadi one-shot (tanpa loop/ulang)
    void ForceOneShot(GameObject root)
    {
        if (!root) return;
        var systems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            var main = ps.main;
            main.loop = false;
            // opsional: biar otomatis destroy, tapi kita sudah Destroy manual
            // main.stopAction = ParticleSystemStopAction.None;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f; // hentikan emit berkelanjutan

#if UNITY_2018_3_OR_NEWER
            for (int b = 0; b < emission.burstCount; b++)
            {
                var burst = emission.GetBurst(b);
                burst.cycleCount = 1;
                burst.repeatInterval = 9999f; // effectively no repeat
                emission.SetBurst(b, burst);
            }
#endif
        }
    }

    // Poligon "bantal api" untuk Firestorm
    void HitFlamePolygon(float baselineY, float centerX, float totalWidth, float totalHeight, HashSet<PegController> processed)
    {
        if (totalWidth <= 0f || totalHeight <= 0f) return;

        float centerY = fireLaneAnchorBottom ? (baselineY + totalHeight * 0.5f) : baselineY;
        Vector2 center = new Vector2(centerX, centerY);

        float halfW = totalWidth * 0.5f;
        float halfH = totalHeight * 0.5f;

        float yBottom = -halfH;
        float hRect = totalHeight * (1f - topCurve);
        float yTopRect = yBottom + hRect;

        float rx = Mathf.Max(0.01f, halfW - halfW * edgeTaper);
        float ry = Mathf.Max(0.01f, totalHeight - hRect);

        const int ARC_SEG = 10;
        var pts = new List<Vector2>(2 + 2 + ARC_SEG + 1);

        pts.Add(new Vector2(-halfW, yBottom));
        pts.Add(new Vector2(+halfW, yBottom));
        pts.Add(new Vector2(+halfW, yTopRect));
        for (int i = 0; i <= ARC_SEG; i++)
        {
            float t = Mathf.Lerp(0f, Mathf.PI, i);
            float x = rx * Mathf.Cos(t);
            float y = yTopRect + ry * Mathf.Sin(t);
            pts.Add(new Vector2(x, y));
        }
        pts.Add(new Vector2(-halfW, yTopRect));

        queryPoly.transform.position = center;
        queryPoly.SetPath(0, pts.ToArray());

        queryResults.Clear();
        queryPoly.enabled = true;                    // ← ON hanya saat query
        queryPoly.OverlapCollider(filter, queryResults);
        queryPoly.enabled = false;                   // ← OFF lagi setelahnya

        for (int i = 0; i < queryResults.Count; i++)
        {
            var peg = queryResults[i].GetComponentInParent<PegController>();
            if (peg != null && !processed.Contains(peg))
            {
                processed.Add(peg);
                peg.SimulateHitFromExplosion();
                GameManager.Instance?.RegisterIndirectPegHit(peg);
            }
        }

        if (debugDrawHit)
        {
            for (int i = 0; i < pts.Count; i++)
            {
                var a = (Vector2)center + pts[i];
                var b = (Vector2)center + pts[(i + 1) % pts.Count];
                Debug.DrawLine(a, b, Color.white, .02f);
            }
        }
    }

    static float EaseOutCubic(float a, float b, float t)
    {
        t = 1f - Mathf.Pow(1f - t, 3f);
        return Mathf.Lerp(a, b, t);
    }

    void StretchVfx(Transform vfx, float length, float height)
    {
        if (!vfx) return;
        vfx.localScale = new Vector3(Mathf.Max(0.01f, length), Mathf.Max(0.01f, height), 1f);
    }

    void ApplySortingToChildren(GameObject go, string layer, int order)
    {
        if (!go) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        { r.sortingLayerName = layer; r.sortingOrder = order; }
    }

    GameObject SpawnVFX(GameObject prefab, Vector2 pos, float scale = 1f)
        => SpawnVFX(prefab, pos, scale, Quaternion.identity);

    GameObject SpawnVFX(GameObject prefab, Vector2 pos, float scale, Quaternion rotation)
    {
        if (!prefab) return null;
        var go = Instantiate(prefab, pos, rotation);
        go.transform.localScale = Vector3.one * Mathf.Max(0.0001f, scale);
        ApplySortingToChildren(go, vfxSortingLayer, vfxSortingOrder);
        return go;
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
            float life = 0f;
            switch (main.startLifetime.mode)
            {
                case ParticleSystemCurveMode.TwoConstants: life = main.startLifetime.constantMax; break;
                case ParticleSystemCurveMode.TwoCurves: life = main.startLifetime.curveMax.Evaluate(1f); break;
                default: life = main.startLifetime.constant; break;
            }
            max = Mathf.Max(max, dur + life);
        }
        return max;
    }

    IEnumerator WaitForParticles(GameObject root)
    {
        float wait = EstimateParticlesDuration(root);
        if (wait > 0f) yield return new WaitForSeconds(wait + 0.10f);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Preview kasar area hit Firestorm (opsional)
        float baselineY = transform.position.y + fireLaneYOffset;
        float visW = fireLaneMaxLength * 2f;
        float hitW = visW * Mathf.Max(0.2f, fireLaneHitWidthScale);
        float h = fireLaneHitHeight;

        float centerY = fireLaneAnchorBottom ? (baselineY + h * 0.5f) : baselineY;
        Vector2 center = new Vector2(transform.position.x, centerY);

        float halfW = hitW * 0.5f;
        float halfH = h * 0.5f;
        float yBottom = -halfH;
        float yTopRect = yBottom + h * (1f - topCurve);

        Gizmos.color = new Color(1f, .6f, .1f, .25f);
        Vector3 a = center + new Vector2(-halfW, yBottom);
        Vector3 b = center + new Vector2(+halfW, yBottom);
        Vector3 c = center + new Vector2(+halfW, yTopRect);
        Vector3 d = center + new Vector2(-halfW, yTopRect);
        Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
    }
#endif
}
