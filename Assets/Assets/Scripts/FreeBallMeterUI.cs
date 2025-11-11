using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class FreeBallMeterUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image fillBar;
    [SerializeField] private Image glowOverlay;

    [Header("Logic")]
    [Tooltip("Kebutuhan hit untuk tier pertama (contoh: 5).")]
    [SerializeField] private int pegThreshold = 5;

    [Tooltip("Kenaikan kebutuhan untuk tier berikutnya (contoh: +3 ⇒ 5,8,11,...)")]
    [SerializeField] private int thresholdStep = 3;

    [Tooltip("Batasi berapa kali free ball per shot (0 = tak terbatas).")]
    [SerializeField] private int maxAwardsPerShot = 0;

    [Tooltip("Kapasitas maksimum Ball-O-Tron. Jika tercapai, free ball tambahan diabaikan.")]
    [SerializeField] private int maxBallCapacity = 20;

    // Modifier dari kartu (opsional; kalau tidak ada CardEffects, hasilnya 0)
    int CardDelta => (CardEffects.I ? CardEffects.I.freeBallThresholdDelta : 0);

    [Header("Visual (opsional)")]
    [SerializeField]
    private Color[] tierColors = {
        new Color(0.35f, 0.95f, 0.35f),   // green
        new Color(0.75f, 0.45f, 0.95f),   // purple
        new Color(0.35f, 0.85f, 0.95f),   // cyan
        new Color(0.98f, 0.85f, 0.25f),   // yellow
        new Color(0.98f, 0.45f, 0.35f)    // red
    };

    int pegHitCount = 0;          // total hit kumulatif di tembakan ini
    int awardsThisShot = 0;       // sudah kasih free ball berapa kali di tembakan ini

    Tween glowTween;
    Vector3 fillOrigScale, glowOrigScale;

    void Awake()
    {
        if (fillBar) fillOrigScale = fillBar.transform.localScale;
        if (glowOverlay) glowOrigScale = glowOverlay.transform.localScale;
    }

    void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnBallUsed += ResetMeter;
    }
    void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnBallUsed -= ResetMeter;
    }

    // ==== Tier math (progresif) ====
    // tierIndex: 0 untuk tier pertama, 1 tier kedua, dst.
    int TierSize(int tierIndex)
    {
        int baseNeed = Mathf.Max(1, pegThreshold + CardDelta);
        return Mathf.Max(1, baseNeed + thresholdStep * tierIndex);
    }
    int SumNeedUntilTier(int lastTierIndexInclusive)
    {
        int sum = 0;
        for (int i = 0; i <= lastTierIndexInclusive; i++) sum += TierSize(i);
        return sum;
    }
    int SumNeedBeforeTier(int tierIndex) => (tierIndex <= 0) ? 0 : SumNeedUntilTier(tierIndex - 1);

    // Dipanggil setiap peg kena (lihat GameManager.RegisterHitPeg)
    public void RegisterPegHit()
    {
        int add = 1;
        if (CardEffects.I != null) add = Mathf.Max(1, CardEffects.I.freeBallHitMultiplier);
        pegHitCount += add;

        // 1) Progress tier berjalan (0..1) berdasarkan hit kumulatif
        int prevSum = SumNeedBeforeTier(awardsThisShot);
        float curTierSize = Mathf.Max(1f, TierSize(awardsThisShot));
        float tierProgress = Mathf.Clamp01((pegHitCount - prevSum) / curTierSize);

        // 2) Update fill hanya untuk porsi tier berjalan (bukan total)
        if (fillBar)
        {
            fillBar.DOFillAmount(tierProgress, 0.20f).SetEase(Ease.OutQuad);
            UpdateFillColor();
        }

        // 3) Selama melewati batas tier, berikan free ball berkali-kali (support loncat tier)
        int safety = 64; // guard
        while (pegHitCount >= SumNeedUntilTier(awardsThisShot) && safety-- > 0)
        {
            if (maxAwardsPerShot > 0 && awardsThisShot >= maxAwardsPerShot) break;

            awardsThisShot++;
            GiveFreeBallReward();
            PulseTierCompleteFX();

            // reset progress bar ke tier baru
            if (fillBar) fillBar.fillAmount = 0f;
            UpdateFillColor();
        }
    }

    public void ResetMeter()
    {
        pegHitCount = 0;
        awardsThisShot = 0;

        if (fillBar)
        {
            fillBar.DOFillAmount(0f, 0.25f).SetEase(Ease.InOutSine);
            fillBar.transform.localScale = fillOrigScale; // jaga-jaga
        }
        HideGlow();
        UpdateFillColor();
    }

    /* ────── Visual helpers ────── */
    void UpdateFillColor()
    {
        if (!fillBar) return;
        if (tierColors != null && tierColors.Length > 0)
        {
            int idx = (tierColors.Length == 0) ? 0 : (awardsThisShot % tierColors.Length);
            fillBar.color = tierColors[idx];
        }
    }

    void PulseTierCompleteFX()
    {
        ShowGlow();

        // Punch kecil pada batang (aman, tidak bikin keluar frame)
        if (fillBar)
        {
            var t = fillBar.transform;
            t.DOKill();
            t.localScale = fillOrigScale;
            t.DOPunchScale(Vector3.one * 0.10f, 0.20f, 7, 0.6f).SetUpdate(true);
        }

        try { AudioManager.I.Play("FreeBallMeter", Camera.main.transform.position); } catch { }
    }

    void ShowGlow()
    {
        if (!glowOverlay) return;
        glowOverlay.gameObject.SetActive(true);
        glowOverlay.color = new Color(1f, 1f, 1f, 0f);
        glowTween?.Kill();
        glowTween = glowOverlay
            .DOFade(0.55f, 0.55f)
            .SetLoops(2, LoopType.Yoyo);
    }

    void HideGlow()
    {
        if (!glowOverlay) return;
        glowTween?.Kill();
        glowOverlay.DOFade(0f, 0.15f).OnComplete(() => glowOverlay.gameObject.SetActive(false));
    }

    void GiveFreeBallReward()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // Cek kapasitas Ball-O-Tron. Kalau penuh, abaikan reward.
        if (gm.BallsLeft >= maxBallCapacity)
        {
            Debug.Log($"Ball-O-Tron penuh ({gm.BallsLeft}/{maxBallCapacity}) → free ball diabaikan");
            return;
        }

        gm.GainBall(1);                 // kompatibel dengan versi GainBall void atau yang mengembalikan int
        ScoreManager.AddFreeBallBonus();
        Debug.Log($"🎁 Free ball ke-{awardsThisShot} di tembakan ini (hits={pegHitCount})");
    }
}
