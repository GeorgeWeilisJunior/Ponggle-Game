using DG.Tweening;
using UnityEngine;
using static GameManager;

[RequireComponent(typeof(CapsuleCollider2D))]
public class BucketController : MonoBehaviour
{
    [Header("Gerak Normal")]
    [SerializeField] float speed = 2.5f;
    [SerializeField] float leftLimit = -8f;
    [SerializeField] float rightLimit = 8f;

    [Header("Collider & Visual")]
    [Tooltip("Trigger penangkap saat mode normal. Diubah jadi solid sementara saat Fireball.")]
    [SerializeField] CapsuleCollider2D normalCollider;
    [SerializeField] SpriteRenderer bodyRenderer;
    [Tooltip("Dipakai saat mulut dibuat solid agar mantul (Friction=0, Bounciness~0.9–1, Combine=Maximum).")]
    [SerializeField] PhysicsMaterial2D rimBouncyMaterial;

    [Header("Fireball Bounce Indicator (opsional)")]
    [Tooltip("Kalau SUDAH ada child indicator (mis. 'Trampoline'), drag ke sini. Kita ON/OFF saja.")]
    [SerializeField] GameObject fireballBounceIndicatorObject;
    [Tooltip("Atau instantiate prefab indikator saat Fireball aktif.")]
    [SerializeField] GameObject fireballBounceIndicatorPrefab;
    [Tooltip("Tempat spawn prefab indikator. Kosong = parent ke Bucket.")]
    [SerializeField] Transform indicatorAnchor;
    [Tooltip("OFF: true=Destroy, false=SetActive(false).")]
    [SerializeField] bool destroyIndicatorOnOff = false;

    [Header("SFX")]
    [Tooltip("Key di AudioManager untuk suara pantulan fireball di bucket.")]
    [SerializeField] string fireballBounceSfxKey = "fireballbounce";
    [Tooltip("Cooldown antar bunyi agar tidak spam.")]
    [SerializeField] float bounceSfxCooldown = 0.08f;
    [Tooltip("Kecepatan relatif minimum agar bunyi diputar.")]
    [SerializeField] float minRelativeSpeedForSfx = 0.2f;

    [Header("Skor Hole – Fever Basic (L→R)")]
    [SerializeField] int[] holeScoresBasic = { 10000, 25000, 50000, 25000, 10000 };

    [Header("Fever External")]
    [SerializeField] GameObject feverRoot;
    [SerializeField] GameObject holeWall;
    [SerializeField] AudioSource feverAS;

    [Header("Fever Intro Animation")]
    [Tooltip("Seberapa jauh ‘muncul dari bawah’ (satuan: local units).")]
    [SerializeField] float feverIntroYOffset = 1.2f;
    [SerializeField] float feverIntroDuration = 0.35f;
    [SerializeField] float drumIntroDelay = 0.06f;   // jeda kecil antara holes & drum/wall
    [SerializeField] float fadeDuration = 0.18f;
    [SerializeField] Ease introEase = Ease.OutBack;

    /* ==================== FEEDBACK TANGKAP ==================== */
    [Header("Feedback on Catch (Non-Fever)")]
    [Tooltip("Prefab pop-up teks (pakai yang biasa dipakai untuk score popup).")]
    [SerializeField] PegScorePopup freeBallPopupPrefab;
    [Tooltip("Tulisan pop-up ketika bola masuk ember.")]
    [SerializeField] string freeBallText = "FREE BALL!";
    [SerializeField] Color freeBallTextColor = new Color(1f, 0.95f, 0.35f, 1f);
    [Tooltip("Jarak vertikal pop-up dari mulut ember.")]
    [SerializeField] float freeBallPopupYOffset = 0.9f;
    [Tooltip("Tampilkan juga pop-up skor +25.000 (visual saja).")]
    [SerializeField] bool alsoShowBucketScorePopup = true;
    [SerializeField] string bucketScoreText = "+25 000";
    [SerializeField] Color bucketScoreTextColor = new Color(1f, 0.9f, 0.2f, 1f);
    [Tooltip("Durasi tampil pop-up.")]
    [SerializeField] float popupDuration = 0.7f;

    [Tooltip("Partikel kecil saat bola masuk ember (opsional).")]
    [SerializeField] ParticleSystem catchBurstPrefab;
    [Tooltip("Sedikit punch-scale ember saat tangkap (0=off).")]
    [SerializeField] float catchPunchScale = 0.15f;
    [SerializeField] float catchPunchDuration = 0.25f;
    [SerializeField] int catchPunchVibrato = 12;
    [SerializeField] float catchPunchElasticity = 0.9f;

    Vector3 feverRootDefaultLocalPos;
    Vector3 holeWallDefaultLocalPos;

    public string FireballBounceSfxKey => fireballBounceSfxKey;
    public bool IsExtremeFever { get; private set; } = false;

    public int GetActiveHoleScore(int idx)
    {
        if (activeHoleScores == null) return 10000;
        if (idx >= 0 && idx < activeHoleScores.Length) return activeHoleScores[idx];
        return 10000;
    }
    public static BucketController Instance { get; private set; }

    // runtime state
    bool inFever;
    int[] activeHoleScores;
    float dir = 1f;
    bool hasCaughtThisTurn;
    bool holeClaimed;
    bool catchEnabled = true;

    // Fireball support
    bool forceMouthSolid = false;     // saat true: mulut non-trigger (memantul)
    GameObject indicatorInstance;     // jika pakai prefab
    float lastBounceSfxTime = -999f;

    void Awake()
    {
        Instance = this;

        if (feverRoot) feverRoot.SetActive(false);
        if (holeWall) holeWall.SetActive(false);

        // cache posisi awal untuk animasi masuk
        if (feverRoot) feverRootDefaultLocalPos = feverRoot.transform.localPosition;
        if (holeWall) holeWallDefaultLocalPos = holeWall.transform.localPosition;

        activeHoleScores = holeScoresBasic;

        if (!normalCollider) normalCollider = GetComponent<CapsuleCollider2D>();
        UpdateColliderEnabled();
    }

    void Update()
    {
        if (inFever) return;

        transform.Translate(Vector2.right * dir * speed * Time.deltaTime);

        if (transform.position.x >= rightLimit && dir > 0f) dir = -1f;
        else if (transform.position.x <= leftLimit && dir < 0f) dir = 1f;
    }

    /* ==================== API Umum ==================== */

    public void SetCatchEnabled(bool on)
    {
        catchEnabled = on;
        UpdateColliderEnabled();
    }

    public bool IsCatchEnabled => catchEnabled;

    /// <summary>Paksa mulut ember jadi SOLID (non-trigger) supaya Fireball memantul. Toggle indikator.</summary>
    public void ForceSolidMouth(bool solid)
    {
        forceMouthSolid = solid;

        if (normalCollider)
        {
            normalCollider.isTrigger = !solid;
            normalCollider.sharedMaterial = solid ? rimBouncyMaterial : null;
            UpdateColliderEnabled();
        }

        // === INDICATOR ===
        if (fireballBounceIndicatorObject)
        {
            fireballBounceIndicatorObject.SetActive(solid);
        }
        else
        {
            if (solid)
            {
                if (fireballBounceIndicatorPrefab)
                {
                    if (!indicatorInstance)
                    {
                        var parent = indicatorAnchor ? indicatorAnchor : transform;
                        indicatorInstance = Instantiate(fireballBounceIndicatorPrefab, parent);
                        indicatorInstance.transform.localPosition = Vector3.zero;
                        indicatorInstance.transform.localRotation = Quaternion.identity;
                        indicatorInstance.transform.localScale = Vector3.one;
                    }
                    else indicatorInstance.SetActive(true);
                }
            }
            else
            {
                if (indicatorInstance)
                {
                    if (destroyIndicatorOnOff) { Destroy(indicatorInstance); indicatorInstance = null; }
                    else indicatorInstance.SetActive(false);
                }
            }
        }
    }

    void UpdateColliderEnabled()
    {
        if (!normalCollider) return;
        // Aktif bila:
        // - mode normal & catchEnabled & bukan fever, ATAU
        // - dipaksa solid (Fireball)
        normalCollider.enabled = (!inFever && catchEnabled) || forceMouthSolid;
    }

    /* ==================== FEVER ==================== */

    public void EnterFeverMode(bool all100k)
    {
        inFever = true;
        IsExtremeFever = all100k;
        if (normalCollider) normalCollider.enabled = false;
        if (bodyRenderer) bodyRenderer.enabled = false;

        // siapkan posisi start (turun ke bawah sedikit) + aktifkan objek
        if (feverRoot)
        {
            feverRoot.SetActive(true);
            var t = feverRoot.transform;
            t.DOKill();
            t.localPosition = feverRootDefaultLocalPos + Vector3.down * feverIntroYOffset;
            t.DOBlendableLocalMoveBy(Vector3.up * feverIntroYOffset, feverIntroDuration)
             .SetEase(introEase);
            FadeAllSpritesUnder(feverRoot, 0f, 0f);       // set alpha 0
            FadeAllSpritesUnder(feverRoot, 1f, fadeDuration);
        }

        if (holeWall)
        {
            holeWall.SetActive(true);
            var t = holeWall.transform;
            t.DOKill();
            t.localPosition = holeWallDefaultLocalPos + Vector3.down * feverIntroYOffset;
            t.DOBlendableLocalMoveBy(Vector3.up * feverIntroYOffset, feverIntroDuration)
             .SetEase(introEase)
             .SetDelay(drumIntroDelay);                   // jeda kecil biar berasa “susul”
            FadeAllSpritesUnder(holeWall, 0f, 0f);
            FadeAllSpritesUnder(holeWall, 1f, fadeDuration).SetDelay(drumIntroDelay);
        }

        activeHoleScores = all100k
            ? new int[] { 50_000, 50_000, 50_000, 50_000, 50_000 }
            : holeScoresBasic;
    }

    public void OnBallEnteredHole(int holeIndex, GameObject ball)
    {
        if (feverAS && !feverAS.isPlaying) feverAS.Play();

        GameManager.Instance.RestoreTimeScale();

        if (!inFever || holeClaimed) return;
        holeClaimed = true;

        int bonus = (holeIndex >= 0 && holeIndex < activeHoleScores.Length)
            ? activeHoleScores[holeIndex]
            : 10000;

        ScoreManager.AddFever(bonus);

        Destroy(ball);
        GameManager.Instance.NotifyBallEnded();
    }

    /* ==================== CATCH NORMAL ==================== */

    void OnTriggerEnter2D(Collider2D other)
    {
        if (inFever) return;
        if (!catchEnabled) return;
        if (hasCaughtThisTurn || !other.CompareTag("Ball")) return;

        // Jangan tangkap Fireball (biarkan memantul)
        if (other.GetComponent<AposdaFireball>() != null) return;

        hasCaughtThisTurn = true;

        StyleShotManager.OnBucketCatch(transform.position);
        AudioManager.I.Play("BucketIn", transform.position);

        GameManager.Instance.GainBall(1);
        ScoreManager.AddFreeBallBonus();

        // ==== FEEDBACK VISUAL ====
        ShowCatchFeedback();

        GameManager.Instance?.NotifyBallEnd(BallEndReason.Bucket);
        Destroy(other.gameObject);
    }

    void ShowCatchFeedback()
    {
        // Punch scale ember (jika disetel)
        if (bodyRenderer && catchPunchScale > 0f && catchPunchDuration > 0f)
        {
            var t = bodyRenderer.transform;
            t.DOKill();
            t.DOPunchScale(new Vector3(catchPunchScale, catchPunchScale, 0f), catchPunchDuration, catchPunchVibrato, catchPunchElasticity);
        }

        // Partikel burst (opsional)
        if (catchBurstPrefab)
        {
            var ps = Instantiate(catchBurstPrefab, transform.position, Quaternion.identity);
            ps.Play();
            Destroy(ps.gameObject, 2f);
        }

        // Pop-up teks FREE BALL!
        if (freeBallPopupPrefab)
        {
            var p1 = Instantiate(freeBallPopupPrefab, transform.position + Vector3.up * freeBallPopupYOffset, Quaternion.identity);
            // Show(string text, Color color, float duration)
            p1.Show(freeBallText, freeBallTextColor, popupDuration);
        }

        // Pop-up skor +25.000 (visual)
        if (alsoShowBucketScorePopup && freeBallPopupPrefab)
        {
            var p2 = Instantiate(freeBallPopupPrefab, transform.position + Vector3.up * (freeBallPopupYOffset + 0.45f), Quaternion.identity);
            p2.Show(bucketScoreText, bucketScoreTextColor, popupDuration);
        }
    }

    /* ==================== SFX Pantul Fireball ==================== */

    // Dipanggil saat mulut sedang dibuat solid (forceMouthSolid = true).
    // Akan mainkan SFX ketika benturan pertama terjadi.
    void OnCollisionEnter2D(Collision2D col)
    {
        if (!forceMouthSolid) return;                 // hanya saat fireball-mode
        if (!col.collider || !col.collider.CompareTag("Ball")) return;

        // pastikan itu fireball-nya Aposda
        if (!col.collider.GetComponent<AposdaFireball>()) return;

        // throttle + threshold kecepatan
        if (col.relativeVelocity.magnitude < minRelativeSpeedForSfx) return;
        if (Time.time - lastBounceSfxTime < bounceSfxCooldown) return;

        Vector3 p = (col.contactCount > 0) ? (Vector3)col.GetContact(0).point : col.transform.position;
        if (!string.IsNullOrEmpty(fireballBounceSfxKey))
            AudioManager.I.Play(fireballBounceSfxKey, p);

        lastBounceSfxTime = Time.time;
    }

    public void ResetBucket()
    {
        hasCaughtThisTurn = false;
        holeClaimed = false;
    }

    Tween FadeAllSpritesUnder(GameObject root, float targetAlpha, float dur)
    {
        if (!root) return null;

        // gabungkan semua fade ke satu Sequence biar rapi
        var seq = DOTween.Sequence();
        var srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in srs)
        {
            if (!sr) continue;
            sr.DOKill();
            var c = sr.color;
            if (dur <= 0f)
            {
                c.a = targetAlpha;
                sr.color = c;
            }
            else
            {
                seq.Join(sr.DOFade(targetAlpha, dur));
            }
        }
        return seq;
    }

}
