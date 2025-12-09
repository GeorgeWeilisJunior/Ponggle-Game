using UnityEngine;
using System.Collections.Generic;

/// Lintasan bidik akurat & sinkron dengan Launcher.
public class AimGuide : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] GameObject dotPrefab;
    [SerializeField] BallController ballPrefab;   // prefab bola yg sama dg Launcher

    [Header("Guide Settings")]
    [SerializeField] float timeToPeak = .45f;     // akan di-override dari Launcher
    [SerializeField] float baseDistance = 6f;     // akan di-override dari Launcher
    [SerializeField] float minFactor = 0.5f, maxFactor = 2f;
    [SerializeField] int maxDots = 75;
    [SerializeField] LayerMask hitMask = ~0;

    [Header("Triple-Shot")]
    [SerializeField] float tripleSpreadDeg = 10f;

    [Header("End Ball Visual (Optional)")]
    [SerializeField] GameObject endBallPrefab;
    [Range(0, 1)][SerializeField] float endBallAlpha = 0.55f;
    [SerializeField] string endBallSortingLayer = "Default";
    [SerializeField] int endBallSortingOrder = 2;

    [Header("Tuning")]
    [Tooltip("Tambah skala radius utk konservatif (1.0 = sama persis).")]
    [SerializeField] float radiusScale = 1.03f;
    [Tooltip("Margin ekstra (world units).")]
    [SerializeField] float radiusBias = 0.002f;

    [Header("Accuracy")]
    [SerializeField, Range(1, 15)] int subSteps = 3;     // simulasi lebih rapat dari fixedDelta
    [SerializeField] float conservativeBias = 0.006f;   // backoff saat kontak (bukan utk membesarkan radius)

    // === Guide Length -> MaxFactor mapping ===
    [SerializeField] float maxFactorMin = 0.30f;   // saat slider 0
    [SerializeField] float maxFactorMax = 3.00f;   // saat slider 1
    float runtimeMaxFactor;                         // dipakai di perhitungan

    readonly List<Transform> dotsC = new();
    readonly List<Transform> dotsL = new();
    readonly List<Transform> dotsR = new();

    Transform endC, endL, endR;
    SpriteRenderer ballSR;
    float ballRadius = .2f;

    void Awake()
    {
        runtimeMaxFactor = Mathf.Clamp(maxFactor, maxFactorMin, maxFactorMax);

        // pool titik
        for (int i = 0; i < maxDots; i++)
        {
            dotsC.Add(Instantiate(dotPrefab, transform).transform);
            dotsL.Add(Instantiate(dotPrefab, transform).transform);
            dotsR.Add(Instantiate(dotPrefab, transform).transform);
            dotsC[i].gameObject.SetActive(false);
            dotsL[i].gameObject.SetActive(false);
            dotsR[i].gameObject.SetActive(false);
        }

        // ghost-ball
        if (endBallPrefab)
        {
            endC = BuildEndBall();
            endL = BuildEndBall();
            endR = BuildEndBall();
        }
    }

    void OnEnable()
    {
        // hitung radius akurat dengan membuat 1 instance dummy (root, tanpa parent scale)
        RecomputeBallRadiusRuntime();
    }
    public void ApplyGuideLength01(float t01)
    {
        t01 = Mathf.Clamp01(t01);
        runtimeMaxFactor = Mathf.Lerp(maxFactorMin, maxFactorMax, t01);
    }
    // ===== hitung radius bola akurat (pakai collider child & lossyScale) =====
    void RecomputeBallRadiusRuntime()
    {
        if (!ballPrefab) return;

        var dummy = Instantiate(ballPrefab, Vector3.one * 9999f, Quaternion.identity);

        var col = dummy.GetComponentInChildren<CircleCollider2D>();
        float r = .2f;

        if (col)
        {
            float scl = Mathf.Max(col.transform.lossyScale.x, col.transform.lossyScale.y);
            r = col.radius * scl;
        }
        else
        {
            ballSR = dummy.GetComponentInChildren<SpriteRenderer>();
            if (ballSR) r = ballSR.bounds.extents.x;
        }

        DestroyImmediate(dummy.gameObject);

        // ⛳ radius real + sedikit margin (TANPA conservativeBias)
        ballRadius = r * radiusScale + radiusBias;
    }

    Transform BuildEndBall()
    {
        var t = Instantiate(endBallPrefab, transform).transform;
        t.gameObject.SetActive(false);
        if (t.TryGetComponent<SpriteRenderer>(out var sr))
        {
            if (!sr.sprite)
            {
                if (!ballSR) ballSR = ballPrefab ? ballPrefab.GetComponentInChildren<SpriteRenderer>() : null;
                if (ballSR) sr.sprite = ballSR.sprite;
            }
            if (ballSR && sr.bounds.size.x > 0f)
            {
                float scale = ballSR.bounds.size.x / sr.bounds.size.x;
                t.localScale *= scale;
            }
            sr.sortingLayerName = endBallSortingLayer;
            sr.sortingOrder = endBallSortingOrder;
            var c = sr.color; c.a = endBallAlpha; sr.color = c;
        }
        return t;
    }

    void OnDisable()
    {
        HidePool(dotsC); HidePool(dotsL); HidePool(dotsR);
        if (endC) endC.gameObject.SetActive(false);
        if (endL) endL.gameObject.SetActive(false);
        if (endR) endR.gameObject.SetActive(false);
    }

    void HidePool(List<Transform> pool)
    {
        for (int i = 0; i < pool.Count; i++)
            if (pool[i]) pool[i].gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (!Launcher.Instance) { OnDisable(); return; }
        var cam = Camera.main; if (!cam) return;

        // --- sinkron nilai dari Launcher (anti mismatch Inspector)
        timeToPeak = Launcher.Instance.TimeToPeak;
        baseDistance = Launcher.Instance.AimDistance;

        // arah bidik dari posisi pivot
        var mp = Input.mousePosition;
        mp.z = Mathf.Abs(cam.transform.position.z - Launcher.Instance.PivotPos.z);
        Vector3 mouse = cam.ScreenToWorldPoint(mp);
        mouse.z = Launcher.Instance.PivotPos.z;

        Vector2 dirUnit = Launcher.Instance.ClampDir((Vector2)(mouse - Launcher.Instance.PivotPos));

        float factor = Mathf.Lerp(minFactor, runtimeMaxFactor, Mathf.Clamp01(GlobalSettings.GuideLength));
        float usedDistance = Launcher.Instance.AimDistance * factor;
        Vector3 origin = Launcher.Instance.SpawnPos;
        Vector3 target = origin + (Vector3)dirUnit * usedDistance;

        bool fireballNext = CharacterPowerManager.Instance &&
                            CharacterPowerManager.Instance.nextShotFireball;

        float g = Physics2D.gravity.y * (ballPrefab ? ballPrefab.GravityScale : 1f);

        // first-shot linear? (gravitasi 0 agar benar-benar lurus)
        bool isFirstShot = GameManager.Instance && GameManager.Instance.CurrentShotId == 0;
        if (isFirstShot && Launcher.Instance.FirstShotLinear)
            g = 0f;

        float speedMul = 1f;
        if (isFirstShot && CardEffects.I)
            speedMul = CardEffects.I.firstShotSpeedMultiplier;

        Vector2 v0C = CalculateV0(origin, target, timeToPeak, g) * speedMul;

        bool triple = CharacterPowerManager.Instance &&
                      CharacterPowerManager.Instance.nextShotTripleBall &&
                      !fireballNext;

        Vector2 lastC; int usedC = DrawPath(dotsC, v0C, g, true, out lastC);

        if (triple)
        {
            float spread = Launcher.Instance.TripleSpreadDeg;
            float speedScale = Launcher.Instance.TripleSpeedScale;

            Vector2 vL = (Vector2)(Quaternion.Euler(0, 0, spread) * v0C) * speedScale;
            Vector2 vR = (Vector2)(Quaternion.Euler(0, 0, -spread) * v0C) * speedScale;

            Vector2 lastL, lastR;
            int usedL = DrawPath(dotsL, vL, g, true, out lastL);
            int usedR = DrawPath(dotsR, vR, g, true, out lastR);

            SetEnd(endC, usedC > 0, lastC);
            SetEnd(endL, usedL > 0, lastL);
            SetEnd(endR, usedR > 0, lastR);
        }
        else
        {
            DrawPath(dotsL, v0C, g, false, out _);
            DrawPath(dotsR, v0C, g, false, out _);
            SetEnd(endL, false, Vector2.zero);
            SetEnd(endR, false, Vector2.zero);
            SetEnd(endC, usedC > 0, lastC);
        }
    }

    // ===== path integrator: fixedDeltaTime + CircleCast + sub-steps =====
    int DrawPath(List<Transform> pool, Vector2 v0, float g, bool on, out Vector2 lastPos)
    {
        // pastikan cast tidak mengenai trigger
        Physics2D.queriesHitTriggers = false;

        Vector2 pos = Launcher.Instance.SpawnPos;
        Vector2 vel = v0;
        int used = 0; lastPos = pos;

        float dt = Time.fixedDeltaTime;
        int subBase = Mathf.Max(1, subSteps);

        for (int i = 0; i < maxDots; i++)
        {
            pool[i].gameObject.SetActive(on);
            if (!on) continue;

            pool[i].position = pos; used++; lastPos = pos;

            bool stop = false;

            // sub-stepping adaptif: segmen maksimum ~ setengah radius
            float targetSegLen = Mathf.Max(0.5f * ballRadius, 0.02f);
            float estSegLen = (vel.magnitude * dt) / subBase;
            int sub = Mathf.Max(subBase, Mathf.CeilToInt(estSegLen / targetSegLen));
            float h = dt / sub;

            for (int s = 0; s < sub; s++)
            {
                Vector2 nxt = pos + vel * h + 0.5f * Vector2.up * g * h * h;
                Vector2 seg = nxt - pos;
                float dist = seg.magnitude;

                // (1) swept circle cast di sepanjang segmen
                if (dist > 0f)
                {
                    Vector2 dir = seg / dist;
                    var hit = Physics2D.CircleCast(pos, ballRadius, dir, dist, hitMask);
                    if (hit.collider != null)
                    {
                        // backoff: radius + conservativeBias (tanpa memperbesar radius global)
                        lastPos = hit.centroid - dir * (ballRadius + conservativeBias);
                        stop = true;
                        break;
                    }
                }

                // (2) overlap check pada posisi akhir sub-step
                vel += Vector2.up * g * h;
                pos = nxt;

                var overlap = Physics2D.OverlapCircle(pos, ballRadius, hitMask);
                if (overlap != null)
                {
                    Vector2 backDir = (seg.sqrMagnitude > 1e-6f) ? seg.normalized : Vector2.down;
                    lastPos = pos - backDir * (ballRadius + conservativeBias);
                    stop = true;
                    break;
                }
            }

            if (stop) break;
        }

        for (int i = used; i < maxDots; i++)
            if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);

        return used;
    }

    void SetEnd(Transform end, bool on, Vector2 p)
    {
        if (!end) return;
        end.gameObject.SetActive(on);
        if (on) end.position = p;
    }

    Vector2 CalculateV0(Vector3 o, Vector3 t, float tp, float g)
    {
        Vector3 d = t - o;
        float v0x = d.x / tp;
        float v0y = d.y / tp - 0.5f * g * tp;
        return new Vector2(v0x, v0y);
    }
}
