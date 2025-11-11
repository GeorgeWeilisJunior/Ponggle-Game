using UnityEngine;
using UnityEngine.EventSystems;

public class Launcher : MonoBehaviour
{
    public static Launcher Instance { get; private set; }

    /* ───── Inspector ───── */
    [Header("Transforms")]
    [SerializeField] Transform pivot;
    [SerializeField] Transform spawnPoint;
    [SerializeField] Transform barrel;

    [Header("Prefabs & Refs")]
    [SerializeField] BallController ballPrefab;
    [SerializeField] AimGuide aimGuide;

    [Header("Meteor (optional)")]
    [Tooltip("Jika diisi, saat Fireball aktif kita spawn prefab ini sebagai peluru.")]
    [SerializeField] BallController meteorPrefab;

    [Header("Half-Circle Settings")]
    [SerializeField] float minDeg = -135f;
    [SerializeField] float maxDeg = -45f;
    [SerializeField] float radius = 0f;

    [Header("Launch Settings")]
    [SerializeField] float timeToPeak = .45f;
    [SerializeField] float aimDistance = 8f;

    [Header("Triple-Shot")]
    [SerializeField] float tripleSpreadDeg = 10f;
    [SerializeField] float tripleSpeedScale = 1f;
    [SerializeField] float lateralSpawnOffset = 0.05f;

    [Header("Sprite Orientation")]
    [Tooltip("90  ⇒ sprite menghadap ke bawah (-Y)\n0   ⇒ sprite menghadap ke kanan (+X)")]
    [SerializeField] float spriteForwardOffset = 90f;

    [Header("Aposda Fireball")]
    [Tooltip("VFX indikator di launcher saat fireball READY (optional)")]
    [SerializeField] GameObject aposdaReadyVfx;
    [Tooltip("VFX di moncong saat ditembak (optional)")]
    [SerializeField] GameObject aposdaMuzzleVfxPrefab;
    [Tooltip("Jejak/VFX yang ditempel ke bola fireball (optional)")]
    [SerializeField] GameObject aposdaBallTrailVfxPrefab;
    [SerializeField] string aposdaShootSfxKey = "Aposda_Shoot";
    [SerializeField] string aposdaHitSfxKey = "Aposda_Hit";
    [SerializeField, Min(.1f)] float aposdaScale = 1.6f;

    [Header("Aposda Fireball Sprite Swap")]
    [SerializeField] Sprite aposdaFireballSprite;         // sprite fireball (untuk swap jika tidak pakai meteorPrefab)
    [SerializeField] Material aposdaFireballMaterial;       // opsional
    [SerializeField] Color aposdaFireballTint = Color.white; // opsional
    [SerializeField] bool aposdaApplyTint = false;           // aktifkan tint?
    [Tooltip("Besar visual saja (SpriteRenderer), collider tidak ikut membesar.")]
    [SerializeField] bool aposdaScaleSpriteOnly = true;
    [Tooltip("Jika prefab bola punya beberapa SR, beri petunjuk nama SR bola (mis. 'Ball').")]
    [SerializeField] string aposdaTargetRendererNameContains = "Ball";

    [Header("Accuracy Options")]
    [SerializeField] bool firstShotLinear = true;
    public bool FirstShotLinear => firstShotLinear;


    /* ───── Runtime ───── */
    bool inputLocked;
    Camera cam;

    public Vector3 PivotPos => pivot.position;
    public Vector3 SpawnPos => spawnPoint.position;

    public BallController GetBallPrefab() => ballPrefab;
    public float TimeToPeak => timeToPeak;
    public float AimDistance => aimDistance;

    public float TripleSpreadDeg => tripleSpreadDeg;
    public float TripleSpeedScale => tripleSpeedScale;
    public BallController MeteorPrefab => meteorPrefab;
    /* ══════════════ LIFECYCLE ══════════════ */
    void Awake()
    {
        Instance = this;
        cam = Camera.main;

        if (radius <= 0f && pivot && spawnPoint)
            radius = Vector2.Distance(pivot.position, spawnPoint.position);
    }

    void Update()
    {
        if (inputLocked) return;
        if (GameManager.Instance && GameManager.Instance.IsFlipping) return;
        if (BallController.ActiveBalls > 0) return;

        if (!cam) cam = Camera.main;

        // Indikator VFX saat fireball READY
        if (aposdaReadyVfx)
        {
            bool fireReady = CharacterPowerManager.Instance &&
                             CharacterPowerManager.Instance.nextShotFireball;
            if (aposdaReadyVfx.activeSelf != fireReady)
                aposdaReadyVfx.SetActive(fireReady);
        }

        // Aim di busur
        var mp = Input.mousePosition;
        mp.z = Mathf.Abs(cam.transform.position.z - pivot.position.z);
        Vector3 mouse = cam.ScreenToWorldPoint(mp);
        Vector2 rawDir = mouse - pivot.position;
        Vector2 dirUnit = ClampDir(rawDir);

        float ang = Mathf.Atan2(dirUnit.y, dirUnit.x) * Mathf.Rad2Deg;

        Vector3 pos = pivot.position + (Vector3)(dirUnit * radius);
        if (barrel) barrel.position = pos;
        if (spawnPoint) spawnPoint.position = pos;

        if (barrel) barrel.rotation = Quaternion.Euler(0, 0, ang - spriteForwardOffset);

        if (aimGuide && !aimGuide.gameObject.activeSelf)
            aimGuide.gameObject.SetActive(true);

        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;
            if (!GameManager.Instance.CanShoot()) return;
            Shoot(dirUnit);
        }
    }

    /* ══════════════ SHOOT ══════════════ */
    void Shoot(Vector2 dirUnit)
    {
        float g = Physics2D.gravity.y * ballPrefab.GravityScale;

        bool isFirstShot = GameManager.Instance && GameManager.Instance.CurrentShotId == 0;
        if (isFirstShot && firstShotLinear)
            g = 0f;


        Vector3 target = spawnPoint.position + (Vector3)dirUnit * aimDistance;
        Vector2 v0Center = CalculateV0(spawnPoint.position, target, timeToPeak, g);

        if (isFirstShot && CardEffects.I)
            v0Center *= CardEffects.I.firstShotSpeedMultiplier;

        if (GameManager.Instance && GameManager.Instance.CurrentShotId == 0 && CardEffects.I)
        {
            v0Center *= CardEffects.I.firstShotSpeedMultiplier;
        }

        GameManager.Instance.UseBall();

        // Ambil element untuk bola normal (tetap dipanggil agar pipeline Next Ball/ HUD tetap konsisten).
        ElementType elemForThisShot = ElementSystem.ConsumeForNewBall();

        bool fireball = CharacterPowerManager.Instance &&
                        CharacterPowerManager.Instance.nextShotFireball;

        bool triple = CharacterPowerManager.Instance &&
                      CharacterPowerManager.Instance.nextShotTripleBall &&
                      !fireball;

        if (fireball)
        {
            if (aposdaMuzzleVfxPrefab)
                Instantiate(aposdaMuzzleVfxPrefab, spawnPoint.position, Quaternion.identity);

            BallController ball;

            if (meteorPrefab) // ====== PAKAI PREFAB METEOR KHUSUS ======
            {
                ball = Instantiate(meteorPrefab, spawnPoint.position, Quaternion.identity);
                ball.Shoot(v0Center);

                // Kalau ada BallElement, pastikan neutral agar visual/efek element tidak nempel.
                var be = ball.GetComponent<BallElement>();
                if (be) be.SetElement(ElementType.Neutral);
            }
            else             // ====== PAKAI BALL PREFAB BIASA + SWAP SPRITE ======
            {
                ball = Spawn(spawnPoint.position, v0Center, elemForThisShot);
            }

            // Pastikan komponen AposdaFireball ada (hindari dobel)
            var fb = ball.GetComponent<AposdaFireball>();
            if (!fb) fb = ball.gameObject.AddComponent<AposdaFireball>();

            // Configure extended (sprite swap + scale sprite-only + target name hint)
            fb.Configure(
                aposdaScale,
                aposdaBallTrailVfxPrefab,
                aposdaShootSfxKey,
                aposdaHitSfxKey,
                meteorPrefab ? false : true,                 // kalau pakai meteorPrefab biasanya tak perlu swap
                aposdaFireballSprite,
                aposdaFireballMaterial,
                aposdaFireballTint,
                aposdaApplyTint,
                aposdaScaleSpriteOnly,                       // besar visual saja
                aposdaTargetRendererNameContains             // hint nama SR utama
            );
        }
        else if (triple)
        {
            Spawn(spawnPoint.position, v0Center, elemForThisShot);

            Vector2 vLeft = Quaternion.Euler(0, 0, tripleSpreadDeg) * v0Center * tripleSpeedScale;
            Vector2 vRight = Quaternion.Euler(0, 0, -tripleSpreadDeg) * v0Center * tripleSpeedScale;

            Vector3 leftPos = spawnPoint.position + Vector3.left * lateralSpawnOffset;
            Vector3 rightPos = spawnPoint.position + Vector3.right * lateralSpawnOffset;

            Spawn(leftPos, vLeft, elemForThisShot);
            Spawn(rightPos, vRight, elemForThisShot);
        }
        else
        {
            Spawn(spawnPoint.position, v0Center, elemForThisShot);
        }
        if (!fireball && CardEffects.I != null && CardEffects.I.ConsumeMirrorForThisShot())
        {
            // Mirror secara horizontal arah vektor pusat (v0Center)
            Vector2 vMirror = new Vector2(-v0Center.x, v0Center.y);

            // Spawn satu bola tambahan dengan elemen yang sama
            Spawn(spawnPoint.position, vMirror, elemForThisShot);

            // (opsional) sfx kecil agar terasa ada efek
            try { AudioManager.I.Play("Split", spawnPoint.position); } catch { }
        }
        CardEffects.I?.AfterFirstShotApplied();
        CharacterPowerManager.Instance?.OnBallShot();

        ReactionExecutorV2.RegisterShotFired();
        AudioManager.I.Play("CannonShot", spawnPoint.position);
        if (aimGuide) aimGuide.gameObject.SetActive(false);
    }

    // Spawn bola + set elemen visual dari Next Ball
    BallController Spawn(Vector3 pos, Vector2 v0, ElementType elem)
    {
        var ball = Instantiate(ballPrefab, pos, Quaternion.identity);
        var be = ball.GetComponent<BallElement>();
        if (be) be.SetElement(elem);

        // ACTIVATE linear mode untuk shot pertama jika di-toggle
        bool isFirstShot = GameManager.Instance && GameManager.Instance.CurrentShotId == 0;
        if (isFirstShot && firstShotLinear)
            ball.EnableNoGravityUntilFirstBounce();

        ball.Shoot(v0);
        return ball;
    }

    /* ───── API eksternal ───── */
    public void LockInput() => inputLocked = true;
    public void UnlockInput() => inputLocked = false;

    /* ══════════════ UTILITIES ══════════════ */
    Vector2 CalculateV0(Vector3 o, Vector3 t, float tp, float g)
    {
        Vector3 d = t - o;
        float v0x = d.x / tp;                       // komponen horizontal
        float v0y = d.y / tp - 0.5f * g * tp;       // komponen vertikal (parabola)
        return new Vector2(v0x, v0y);
    }

    /// <summary>Clamp arah ke rentang busur tanpa melompat (support min/max negatif).</summary>
    public Vector2 ClampDir(Vector2 raw)
    {
        if (raw.sqrMagnitude < 1e-4f) return Vector2.down;

        float ang = Mathf.Atan2(raw.y, raw.x) * Mathf.Rad2Deg;
        if (ang < 0f) ang += 360f;

        float min = (minDeg < 0f) ? minDeg + 360f : minDeg;
        float max = (maxDeg < 0f) ? maxDeg + 360f : maxDeg;

        if (ang < min || ang > max)
        {
            float dMin = Mathf.Abs(Mathf.DeltaAngle(ang, min));
            float dMax = Mathf.Abs(Mathf.DeltaAngle(ang, max));
            ang = (dMin < dMax) ? min : max;
        }

        float rad = ang * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
    }
}
