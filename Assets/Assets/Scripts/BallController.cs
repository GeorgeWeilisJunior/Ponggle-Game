using System.Collections.Generic;
using UnityEngine;
using static GameManager;

[RequireComponent(typeof(Rigidbody2D))]
public class BallController : MonoBehaviour
{
    /* ───────── Inspector ───────── */
    [Header("Launch Settings")]
    [SerializeField] float initialSpeed = 8f;
    [SerializeField] float gravityScale = 1.0f;   // saran: 1.0 agar jatuh terasa natural
    public float InitialSpeed => initialSpeed;
    public float GravityScale => gravityScale;

    [Header("After-Bounce (Energy)")]
    [SerializeField, Range(.5f, 1f)] float bounceDamping = .94f;

    [Header("Damping Mode Setelah Pantul")]
    [Tooltip("Jika ON, redam hanya sumbu X (horizontal) setelah pantul. Jika OFF, pakai drag fisika biasa.")]
    [SerializeField] bool useHorizontalOnlyDamping = true;

    [Tooltip("Persentase sisa kecepatan X per detik setelah pantul (0.90 = -10%/detik).")]
    [SerializeField, Range(0.5f, 0.999f)] float horizontalDampPerSecond = 0.92f;

    [Tooltip("MODE LAMA (tak disarankan): drag fisika global setelah pantul; ikut menahan jatuh (Y).")]
    [SerializeField] float dragAfterBounce = 0.15f;

    [Header("Stuck Handling")]
    [SerializeField] float stuckSpeed = .15f;
    [SerializeField] float stuckDelay = .4f;
    [SerializeField] float stopTime = 3f;

    /* ───────── Trail ───────── */
    public enum TrailMode { Off, Always, OnlyInFever }

    [Header("Trail")]
    [Tooltip("Isi dengan TrailRenderer pada child (opsional).")]
    [SerializeField] TrailRenderer trail;
    [Tooltip("Off = tanpa trail, Always = selalu nyala, OnlyInFever = nyala saat Fever saja.")]
    [SerializeField] TrailMode trailMode = TrailMode.Off;

    /* ───────── Runtime ───────── */
    readonly List<PegController> currentContacts = new();
    float stuckTimer, stationaryTimer;
    bool firstBounceDone;
    bool dampHorizontal;                  // NEW: flag per-bola setelah pantul pertama
    public bool HasHitPeg { get; private set; }

    /* ───────── Global counter ───────── */
    public static int ActiveBalls { get; private set; }
    public ElementType CurrentElement { get; private set; } = ElementType.Neutral;

    /* cache */
    Rigidbody2D rb;
    CircleCollider2D circle;

    /* 🔎 PUBLIC GETTER (dipakai GM utk cinematic) */
    public Vector2 Velocity => rb.velocity;
    public float Radius => circle.radius * transform.localScale.x;

    // >>> tambahkan di fields runtime
    bool noGravUntilFirstBounce;
    float cachedOrigGravity;

    /* ═══════════ LIFECYCLE ═══════════ */
    void OnEnable()
    {
        ActiveBalls++;
        InitTrailOnSpawn();
    }

    void OnDestroy()
    {
        ActiveBalls--;
        if (ActiveBalls <= 0 && GameManager.Instance)
            GameManager.Instance.NotifyBallEnded();
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        circle = GetComponent<CircleCollider2D>();

        rb.gravityScale = gravityScale;
        rb.drag = 0f; // jangan ada drag awal—biarkan gravitasi bekerja penuh
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;  // haluskan gerak di layar

        if (!trail) trail = GetComponentInChildren<TrailRenderer>(true);
    }
    public void EnableNoGravityUntilFirstBounce()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        cachedOrigGravity = gravityScale;     // simpan nilai dari inspector
        rb.gravityScale = 0f;                 // matikan gravitasi fisika
        noGravUntilFirstBounce = true;
    }
    void Start()
    {
        HasHitPeg = false;
        stuckTimer = stationaryTimer = 0f;
        firstBounceDone = false;
        dampHorizontal = false;
    }

    /* ═══════════ Shoot ═══════════ */
    public void Shoot(Vector2 v0)
    {
        rb.velocity = v0;
        rb.angularVelocity = 0f;
    }

    /* ═══════════ Collision ═══════════ */
    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.collider.CompareTag("Peg"))
        {
            HasHitPeg = true;
            var peg = col.collider.GetComponent<PegController>();
            if (peg != null && !currentContacts.Contains(peg))
                currentContacts.Add(peg);
        }
        if (noGravUntilFirstBounce)
        {
            rb.gravityScale = cachedOrigGravity;
            noGravUntilFirstBounce = false;
        }
        // —— style: bila menyentuh dinding atas
        if (col.collider.CompareTag("TopWall"))
            StyleShotManager.OnTopWallBounce();

        // Energi berkurang tiap pantul (seperti Peggle)
        rb.velocity *= bounceDamping;

        if (!firstBounceDone)
        {
            firstBounceDone = true;

            if (useHorizontalOnlyDamping)
            {
                dampHorizontal = true;     // aktifkan redaman sumbu X via FixedUpdate
                rb.drag = 0f;              // pastikan drag global tetap 0
            }
            else
            {
                // MODE LAMA: drag global → menahan X dan Y (jatuh terasa lebih “ringan”)
                rb.drag = dragAfterBounce;
                dampHorizontal = false;
            }
        }
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (col.collider.CompareTag("Peg"))
        {
            var peg = col.collider.GetComponent<PegController>();
            currentContacts.Remove(peg);
        }
    }

    /* ═══════════ Kill zone ═══════════ */
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("KillZone")) return;

        if (!HasHitPeg && GameManager.Instance != null)
            GameManager.Instance.FlipCoinFeedback();

        GameManager.Instance?.NotifyKillZoneThisTurn();

        GameManager.Instance?.NotifyBallEnd(GameManager.BallEndReason.KillZone);

        // ✅ kabari CardEffects supaya langsung bayar bonus
        CardEffects.I?.OnBallKilledByKillZone();

        Destroy(gameObject);
    }

    /* ═══════════ FixedUpdate ═══════════ */
    void FixedUpdate()
    {
        /* Cek last-orange lebih dulu agar window deteksinya maksimal */
        if (GameManager.Instance)
            GameManager.Instance.CheckLastOrangeCinematic(this);

        // NEW: Horizontal-only damping—redam X perlahan tanpa ganggu percepatan jatuh (Y)
        if (dampHorizontal && useHorizontalOnlyDamping)
        {
            // skala redaman agar konsisten dengan fixedDeltaTime yang bisa berubah saat slow-mo
            float baseStep = 0.02f;
            float k = Mathf.Pow(Mathf.Clamp01(horizontalDampPerSecond), Time.fixedDeltaTime / baseStep);
            var v = rb.velocity;
            v.x *= k;
            rb.velocity = v;
        }

        float speed = rb.velocity.magnitude;

        /* A. Stuck di peg */
        if (speed < stuckSpeed && currentContacts.Count > 0)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= stuckDelay)
            {
                var peg = currentContacts[0];
                currentContacts.RemoveAt(0);

                if (peg && peg.State != PegController.PegState.Cleared)
                {
                    // ───── Aturan “anti-nyangkut”:
                    // - Non-hard        → clear peg
                    // - Hard Glow_0X    → clear peg  (hitsRemaining == 0)
                    // - Hard Glow_1X/2X → HAPUS BOLA (end turn)
                    if (!peg.IsHard)
                    {
                        peg.ClearNow();
                    }
                    else
                    {
                        if (peg.HitsRemaining == 0)
                        {
                            peg.ClearNow(); // Glow_0X
                        }
                        else
                        {
                            // 1x atau 2x → korbankan bola
                            Destroy(gameObject); // OnDestroy() akan panggil NotifyBallEnded()
                        }
                    }
                }
                stuckTimer = 0f;
            }
        }
        else stuckTimer = 0f;

        /* B. Diam di udara terlalu lama */
        if (speed < stuckSpeed && currentContacts.Count == 0)
        {
            stationaryTimer += Time.fixedDeltaTime;
            if (stationaryTimer >= stopTime)
                Destroy(gameObject);
        }
        else stationaryTimer = 0f;
    }

    /* ═══════════ Fever helper ═══════════ */
    public void ApplyFeverGravity(float multiplier)
    {
        if (rb) rb.gravityScale *= multiplier;
    }

    /* Trail control saat Fever */
    public void NotifyFeverStarted()
    {
        if (trail && trailMode == TrailMode.OnlyInFever)
        {
            trail.Clear();
            trail.emitting = true;
        }
    }

    /* ====== Impulse helpers (dipakai ReactionExecutor) ====== */
    public void AddRandomImpulse(float magnitude)
    {
        Vector2 dir = Random.insideUnitCircle.normalized;
        AddImpulse(dir * magnitude);
    }

    public void AddImpulse(Vector2 impulse)
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        rb.velocity += impulse;
    }

    /* ====== Trail internal ====== */
    void InitTrailOnSpawn()
    {
        if (!trail) return;

        // default: matikan dulu & bersihkan jejak sisa
        trail.emitting = false;
        trail.Clear();

        switch (trailMode)
        {
            case TrailMode.Off:
                trail.emitting = false;
                break;
            case TrailMode.Always:
                trail.emitting = true;
                break;
            case TrailMode.OnlyInFever:
                trail.emitting = GameManager.Instance && GameManager.Instance.InFever;
                break;
        }
    }
}
