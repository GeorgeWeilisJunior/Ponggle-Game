using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BallController))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class AposdaFireball : MonoBehaviour
{
    [Header("Scale & Lifetime")]
    [SerializeField, Min(0.1f)] float scaleMultiplier = 1.6f;
    [SerializeField, Min(0.5f)] float maxLifetime = 6f;

    [Header("Collision Sensor (hit all pegs)")]
    [SerializeField] float sensorRadiusExtra = 0.15f;
    [SerializeField] float sensorGrowIfSmall = 0.1f;

    [Header("Sprite Swap & Target")]
    [SerializeField] string targetRendererNameContains = "Ball";
    [SerializeField] bool swapMainSprite = true;
    [SerializeField] Sprite fireballSprite;
    [SerializeField] Material fireballMaterial;
    [SerializeField] bool applyTint = false;
    [SerializeField] Color fireballTint = Color.white;
    [SerializeField] bool scaleSpriteOnly = true;

    [Header("VFX / SFX (optional)")]
    [SerializeField] GameObject trailVfxPrefab;
    [SerializeField] string shootSfxKey = "Aposda_Shoot";
    [SerializeField] string hitSfxKey = "Aposda_Hit";
    [Tooltip("Override key SFX pantul bucket (kosong = pakai dari BucketController).")]
    [SerializeField] string bucketBounceSfxKeyOverride = "";

    [Header("Rules")]
    [SerializeField] bool immuneToExternalForces = true;

    [Header("Killzone handling (MATI tanpa coin)")]
    [SerializeField] bool ignoreKillZone = true;
    [SerializeField] LayerMask killZoneLayers = 0;
    [SerializeField] float despawnBelowViewportMargin = 0.6f;

    [Header("Animation (rotation only)")]
    [SerializeField] bool spinEnabled = true;
    [SerializeField] float spinSpeed = 180f;
    [SerializeField] bool randomStartAngle = true;
    [SerializeField] bool rotateSpriteOnly = true;

    [Header("Balance")]
    [Tooltip("Fireball akan hancur setelah mengenai sebanyak ini (unik) peg.")]
    [SerializeField, Min(1)] int maxPegHits = 15;

    // ───────────────────────────── NEW: CEILING AVOIDANCE ─────────────────────────────
    [Header("Ceiling Avoidance")]
    [Tooltip("Cegah fireball langsung menyentuh plafon saat ditembak dari ujung.")]
    [SerializeField] bool preventCeilingHitOnSpawn = true;
    [Tooltip("Layer plafon/tembok atas (opsional). Jika diisi, akan di-ignore sebentar saat spawn).")]
    [SerializeField] LayerMask ceilingLayers = 0;
    [Tooltip("Jarak aman dari plafon saat spawn.")]
    [SerializeField] float ceilingClearance = 0.45f;
    [Tooltip("Kecepatan vertikal awal minimum ke bawah (agar tidak ada komponen ke atas).")]
    [SerializeField] float initialDownwardVy = 4f;
    [Tooltip("Durasi mengabaikan collision dengan plafon setelah spawn.")]
    [SerializeField] float ignoreCeilingDuration = 0.12f;
    // ────────────────────────────────────────────────────────────────────────────────

    [Header("Advanced")]
    [Tooltip("Jika SR terpasang di root bersama Collider2D, buat proxy visual child agar rotasi/scale tidak mengubah hitbox.")]
    [SerializeField] bool autoProxyVisualIfRendererOnRoot = true;

    // runtime
    Collider2D bodyCol;
    Rigidbody2D rb;
    CircleCollider2D sensor;
    GameObject vfxInstance;
    float life;

    // visual cache
    SpriteRenderer mainSR;
    SpriteRenderer originalRootSR;
    GameObject proxyGO;
    bool usingProxy;

    Sprite originalSprite;
    Material originalMat;
    Color originalColor;
    Vector3 originalSRLocalScale;
    Vector3 originalRootScale;
    bool visualsApplied;
    bool keepOverride;

    // physics cache
    float baseGravity, baseDrag, baseAngularDrag;

    // killzone line
    float killLineY = float.NegativeInfinity;
    Camera cam;

    // ceiling line
    float ceilingY = float.PositiveInfinity;
    readonly List<Collider2D> tempIgnoredCeiling = new List<Collider2D>();

    // peg-hit counter (unik)
    int pegHits = 0;
    HashSet<int> hitPegIds = new HashSet<int>();

    /* ===================== CONFIG dari Launcher ===================== */
    public void Configure(float scaleMul, GameObject trailFx, string shootKey, string hitKey)
    {
        scaleMultiplier = scaleMul > 0f ? scaleMul : scaleMultiplier;
        trailVfxPrefab = trailFx;
        if (!string.IsNullOrEmpty(shootKey)) shootSfxKey = shootKey;
        if (!string.IsNullOrEmpty(hitKey)) hitSfxKey = hitKey;

        if (isActiveAndEnabled) ApplyOrReapplyVisuals();
    }

    public void Configure(
        float scaleMul,
        GameObject trailFx,
        string shootKey,
        string hitKey,
        bool swapSprite,
        Sprite sprite,
        Material mat = null,
        Color? tint = null,
        bool useTint = false,
        bool spriteOnly = true,
        string targetNameContains = "")
    {
        Configure(scaleMul, trailFx, shootKey, hitKey);
        swapMainSprite = swapSprite;
        fireballSprite = sprite;
        fireballMaterial = mat;
        if (tint.HasValue) { fireballTint = tint.Value; applyTint = useTint || applyTint; }
        scaleSpriteOnly = spriteOnly;
        if (!string.IsNullOrEmpty(targetNameContains))
            targetRendererNameContains = targetNameContains;

        if (isActiveAndEnabled) ApplyOrReapplyVisuals();
    }

    /* =========================== LIFECYCLE =========================== */
    void Awake()
    {
        bodyCol = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        baseGravity = rb.gravityScale;
        baseDrag = rb.drag;
        baseAngularDrag = rb.angularDrag;

        originalRootScale = transform.localScale;
        cam = Camera.main;
    }

    void OnEnable()
    {
        // sensor untuk menandai peg (bukan fisika pantul)
        sensor = gameObject.AddComponent<CircleCollider2D>();
        sensor.isTrigger = true;

        // tembus semua peg
        foreach (var peg in FindObjectsOfType<PegController>())
        {
            var col = peg.GetComponent<Collider2D>();
            if (col) Physics2D.IgnoreCollision(bodyCol, col, true);
        }

        // bucket: jangan tangkap + mulut solid
        BucketController.Instance?.SetCatchEnabled(false);
        BucketController.Instance?.ForceSolidMouth(true);

        // SFX/VFX
        if (!string.IsNullOrEmpty(shootSfxKey))
            AudioManager.I.Play(shootSfxKey, transform.position);
        if (trailVfxPrefab) vfxInstance = Instantiate(trailVfxPrefab, transform);

        // killzone: garis Y & ignore
        SetupKillLineAndIgnore();

        // CEILING: garis Y, jaga jarak awal, dan grace ignore
        if (preventCeilingHitOnSpawn) SetupCeilingLineAndGraceIgnore();

        // visual (scale + sprite swap)
        ApplyOrReapplyVisuals();

        // start angle acak (visual)
        if (randomStartAngle)
        {
            float z = Random.Range(0f, 360f);
            if (rotateSpriteOnly)
            {
                if (!mainSR) mainSR = ChooseMainRenderer();
                if (mainSR) mainSR.transform.localEulerAngles = new Vector3(0, 0, z);
            }
            else transform.localEulerAngles = new Vector3(0, 0, z);
        }

        life = 0f;
        pegHits = 0;
        hitPegIds.Clear();
    }

    void FixedUpdate()
    {
        if (!immuneToExternalForces) return;
        rb.gravityScale = baseGravity;
        rb.drag = baseDrag;
        rb.angularDrag = baseAngularDrag;
    }

    void Update()
    {
        life += Time.deltaTime;

        // Rotasi visual
        if (spinEnabled)
        {
            float dz = spinSpeed * Time.deltaTime;
            if (rotateSpriteOnly)
            {
                if (!mainSR) mainSR = ChooseMainRenderer();
                if (mainSR) mainSR.transform.Rotate(0, 0, dz);
            }
            else transform.Rotate(0, 0, dz);
        }

        // Mati seperti kena killzone, tapi tanpa coin flip
        if (transform.position.y < killLineY) Destroy(gameObject);

        if (life >= maxLifetime) Destroy(gameObject);
    }

    /* ======================= VISUAL HELPERS ======================= */
    void ApplyOrReapplyVisuals()
    {
        // rollback jika sudah apply
        if (visualsApplied)
        {
            if (scaleSpriteOnly && mainSR) mainSR.transform.localScale = originalSRLocalScale;
            else transform.localScale = originalRootScale;

            if (keepOverride && mainSR && originalSprite)
            {
                mainSR.sprite = originalSprite;
                mainSR.sharedMaterial = originalMat;
                mainSR.color = originalColor;
            }
            keepOverride = false;
        }

        // pilih SR utama (auto buat proxy agar collider tidak ikut di-scale/rotate)
        mainSR = ChooseMainRenderer();
        if (autoProxyVisualIfRendererOnRoot && (scaleSpriteOnly || rotateSpriteOnly))
        {
            if (mainSR && mainSR.gameObject == gameObject)
            {
                CreateVisualProxyFrom(mainSR);
                mainSR = proxyGO.GetComponent<SpriteRenderer>();
            }
        }

        if (mainSR) originalSRLocalScale = mainSR.transform.localScale;

        // scale
        if (scaleSpriteOnly && mainSR) mainSR.transform.localScale = originalSRLocalScale * scaleMultiplier;
        else transform.localScale = originalRootScale * scaleMultiplier;

        // sensor radius
        float baseR = 0.35f;
        if (bodyCol is CircleCollider2D cc)
            baseR = cc.radius * (scaleSpriteOnly ? 1f : scaleMultiplier);
        sensor.radius = baseR + Mathf.Max(sensorRadiusExtra, sensorGrowIfSmall);

        // sprite swap
        if (swapMainSprite && mainSR && fireballSprite)
        {
            originalSprite = mainSR.sprite;
            originalMat = mainSR.sharedMaterial;
            originalColor = mainSR.color;

            mainSR.sprite = fireballSprite;
            if (fireballMaterial) mainSR.sharedMaterial = fireballMaterial;
            if (applyTint) mainSR.color = fireballTint;

            keepOverride = true;
        }

        visualsApplied = true;
    }

    void CreateVisualProxyFrom(SpriteRenderer src)
    {
        if (proxyGO) Destroy(proxyGO);

        proxyGO = new GameObject("VisualProxy");
        proxyGO.transform.SetParent(transform, false);
        proxyGO.transform.localPosition = Vector3.zero;
        proxyGO.transform.localRotation = Quaternion.identity;
        proxyGO.transform.localScale = Vector3.one;
        proxyGO.layer = gameObject.layer;

        var proxySR = proxyGO.AddComponent<SpriteRenderer>();
        proxySR.sprite = src.sprite;
        proxySR.sharedMaterial = src.sharedMaterial;
        proxySR.color = src.color;
        proxySR.sortingLayerID = src.sortingLayerID;
        proxySR.sortingOrder = src.sortingOrder;
#if UNITY_2021_2_OR_NEWER
        proxySR.maskInteraction = src.maskInteraction;
#endif

        originalRootSR = src;
        originalRootSR.enabled = false;
        usingProxy = true;
    }

    SpriteRenderer ChooseMainRenderer()
    {
        if (!string.IsNullOrEmpty(targetRendererNameContains))
        {
            var srs = GetComponentsInChildren<SpriteRenderer>(true);
            string hint = targetRendererNameContains.ToLowerInvariant();
            foreach (var r in srs)
                if (r && r.enabled && r.name.ToLowerInvariant().Contains(hint))
                    return r;
        }

        SpriteRenderer best = null; float bestArea = -1f;
        foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!r.enabled) continue;
            float area = (r.sprite ? r.sprite.rect.width * r.sprite.rect.height : 0f);
            if (area > bestArea) { bestArea = area; best = r; }
        }

        if (!best) best = GetComponent<SpriteRenderer>();
        return best;
    }

    /* =================== KILLZONE: TANPA COIN =================== */
    void SetupKillLineAndIgnore()
    {
        cam = cam ? cam : Camera.main;

        if (cam)
        {
            var bottom = cam.ViewportToWorldPoint(new Vector3(0.5f, 0f, Mathf.Abs(cam.transform.position.z - transform.position.z)));
            killLineY = bottom.y - despawnBelowViewportMargin;
        }

        var all = FindObjectsOfType<Collider2D>(includeInactive: false);
        foreach (var c in all)
        {
            if (!c || !c.enabled) continue;

            bool isKZLayer = ((killZoneLayers.value & (1 << c.gameObject.layer)) != 0);
            bool isKZTag = c.CompareTag("KillZone") || c.CompareTag("DeadZone") || c.CompareTag("OutOfBounds");
            string nm = c.name.ToLowerInvariant();
            bool isKZName = nm.Contains("kill") || nm.Contains("dead") || nm.Contains("outof") || nm.Contains("oob") || nm.Contains("bottom") || nm.Contains("floor");

            if (isKZLayer || isKZTag || isKZName)
            {
                killLineY = Mathf.Max(killLineY, c.bounds.max.y);

                if (ignoreKillZone)
                {
                    Physics2D.IgnoreCollision(bodyCol, c, true);
                    if (sensor) Physics2D.IgnoreCollision(sensor, c, true);
                }
            }
        }
    }

    // ───────────────────────────── NEW: CEILING HELPERS ─────────────────────────────
    void SetupCeilingLineAndGraceIgnore()
    {
        // Ambil garis plafon dari kamera sebagai fallback
        if (cam)
        {
            var top = cam.ViewportToWorldPoint(new Vector3(0.5f, 1f, Mathf.Abs(cam.transform.position.z - transform.position.z)));
            ceilingY = top.y;
        }

        // Coba cari collider plafon sesuai Layer/Name/Tag (opsional)
        var all = FindObjectsOfType<Collider2D>(includeInactive: false);
        foreach (var c in all)
        {
            if (!c || !c.enabled) continue;

            bool inLayer = (ceilingLayers.value != 0) && ((ceilingLayers.value & (1 << c.gameObject.layer)) != 0);
            bool isTag = (c.CompareTag("TopWall"));
            string nm = c.name.ToLowerInvariant();
            bool isName = nm.Contains("ceiling") || nm.Contains("top") || nm.Contains("roof") || nm.Contains("upper") || nm.Contains("wall_top");

            if (inLayer || isTag || isName)
            {
                ceilingY = Mathf.Min(ceilingY, c.bounds.min.y); // bibir bawah plafon
                // ignore sementara supaya tidak langsung mentul
                Physics2D.IgnoreCollision(bodyCol, c, true);
                if (sensor) Physics2D.IgnoreCollision(sensor, c, true);
                tempIgnoredCeiling.Add(c);
            }
        }

        // Pastikan posisi & velocity awal aman
        float rad = GetBodyRadius();
        float maxY = (ceilingY == float.PositiveInfinity) ? transform.position.y : (ceilingY - (rad + ceilingClearance));
        if (transform.position.y > maxY)
            transform.position = new Vector3(transform.position.x, maxY, transform.position.z);

        if (rb.velocity.y >= 0f) // kalau ada komponen ke atas, paksa sedikit ke bawah
        {
            float vy = -Mathf.Max(initialDownwardVy, Mathf.Abs(rb.velocity.y));
            rb.velocity = new Vector2(rb.velocity.x, vy);
        }

        // re-enable collision setelah grace
        if (tempIgnoredCeiling.Count > 0 && ignoreCeilingDuration > 0f)
            StartCoroutine(ReenableCeilingLater(ignoreCeilingDuration));
    }

    System.Collections.IEnumerator ReenableCeilingLater(float t)
    {
        yield return new WaitForSeconds(t);
        foreach (var c in tempIgnoredCeiling)
        {
            if (c) { Physics2D.IgnoreCollision(bodyCol, c, false); if (sensor) Physics2D.IgnoreCollision(sensor, c, false); }
        }
        tempIgnoredCeiling.Clear();
    }

    float GetBodyRadius()
    {
        var cc = bodyCol as CircleCollider2D;
        if (cc) return cc.radius;
        // perkiraan wajar jika bukan lingkaran
        return 0.3f;
    }
    // ─────────────────────────────────────────────────────────────────────────────

    /* =================== HIT PEG & BOUNCE SFX =================== */
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Peg"))
        {
            var peg = other.GetComponent<PegController>();
            if (!peg) return;

            int id = peg.GetInstanceID();
            if (!hitPegIds.Contains(id))
            {
                hitPegIds.Add(id);
                pegHits++;
            }

            if (peg.MarkHitFromSpecial())
            {
                if (!string.IsNullOrEmpty(hitSfxKey))
                    AudioManager.I.Play(hitSfxKey, peg.transform.position);
            }

            if (pegHits >= maxPegHits)
            {
                Destroy(gameObject);
            }
        }
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (!col.collider) return;
        var bucket = col.collider.GetComponentInParent<BucketController>();
        if (!bucket) return;

        string key = !string.IsNullOrEmpty(bucketBounceSfxKeyOverride)
                        ? bucketBounceSfxKeyOverride
                        : (BucketController.Instance != null ? BucketController.Instance.FireballBounceSfxKey : "");

        if (!string.IsNullOrEmpty(key))
        {
            Vector3 p = (col.contactCount > 0) ? (Vector3)col.GetContact(0).point : transform.position;
            AudioManager.I.Play(key, p);
        }
    }

    /* ============================ CLEANUP ============================ */
    void OnDestroy()
    {
        if (visualsApplied)
        {
            if (scaleSpriteOnly && mainSR) mainSR.transform.localScale = originalSRLocalScale;
            else transform.localScale = originalRootScale;
        }
        keepOverride = false;

        if (usingProxy)
        {
            if (proxyGO) Destroy(proxyGO);
            if (originalRootSR) originalRootSR.enabled = true;
        }
        else if (swapMainSprite && mainSR && originalSprite)
        {
            mainSR.sprite = originalSprite;
            mainSR.sharedMaterial = originalMat;
            mainSR.color = originalColor;
        }

        BucketController.Instance?.ForceSolidMouth(false);
        BucketController.Instance?.SetCatchEnabled(true);

        if (vfxInstance) Destroy(vfxInstance);

        GameManager.Instance?.NotifyBallEnded();
    }
}
