using System;
using UnityEngine;

/// <summary>
/// Σ(base × mult) on-the-fly, lalu di akhir shot diberi BONUS × pegCount (gaya Peggle).
/// Multiplier TIDAK lagi naik di tengah shot; ia hanya mengikuti FeverMeter fill.
/// </summary>
public static class ScoreManager
{
    /* ───── PUBLIC READ-ONLY ───── */
    public static int TotalScore { get; private set; }
    public static int LevelScore { get; private set; }
    public static int ShotPoints { get; private set; }    // Σ(base×mult) selama shot
    public static int Multiplier { get; private set; } = 1;
    public static int FeverPoints { get; private set; }

    public static event Action<int, int> OnScoreChanged;    // total, delta
    public static event Action<int> OnMultiplierChanged;

    /* ───── COUNTERS ───── */
    static int pegCountThisShot;
    static float _globalMultiplier = 1f;

    /* ───── KONSTANTA ───── */
    const int blueValue = 100;
    const int orangeValue = 100;
    const int rainbowValue = 500;       // jika dipakai
    const int feverValue = 1_000;
    const int freeBallBonus = 25_000;

    /* ═════════ PEG HIT ═════════ */
    public static void AddPegHit(PegType type, int orangeCleared, int orangeTotal)
    {
        pegCountThisShot++;

        bool inFever = GameManager.Instance && GameManager.Instance.InFever;

        int baseValue = inFever
                        ? feverValue
                        : type switch
                        {
                            PegType.Orange => orangeValue,
                            PegType.Rainbow => rainbowValue,
                            _ => blueValue
                        };

        int delta = baseValue * Multiplier;
        ShotPoints += delta;
        AddToScores(delta);
        if (inFever) FeverPoints += delta;

        // ❌ Tidak ada lagi logika yang mengubah Multiplier di sini.
        // Multiplier hanya di-set via SetMultiplierByFeverFill() dari FeverMeter.
    }

    /// <summary>Dipanggil CardEffects; 1.0 = normal, 1.10 = +10% skor global.</summary>
    public static void SetGlobalScoreMultiplier(float m)
    {
        _globalMultiplier = Mathf.Max(0.01f, m);
    }


    /// <summary>Tambah poin flat (dipakai BetterKillZone, dsb.). Ikut multiplier global.</summary>
    public static void AddPoints(int pts)
    {
        int p = Mathf.RoundToInt(pts * _globalMultiplier);

        // Pastikan properti/field ini ada di ScoreManager-mu.
        TotalScore += p;
        LevelScore += p;
        ShotPoints += p;      // kalau kamu tidak mau ini masuk "shot points", boleh dihapus baris ini.
    }

    public static void AddDirectBonus(int pts)
    {
        if (pts <= 0) return;
        int p = Mathf.RoundToInt(pts * _globalMultiplier);
        LevelScore += p;
        TotalScore += p;
        OnScoreChanged?.Invoke(TotalScore, p);
    }

    /* ═════════ END SHOT ═════════ */
    public static void EndShot()
    {
        // Bonus gaya Peggle: total shot × jumlah peg yang kena
        if (pegCountThisShot > 0)
        {
            int finalShot = ShotPoints * pegCountThisShot;  // eg. 140×3 = 420
            int bonus = finalShot - ShotPoints;
            if (bonus > 0) AddToScores(bonus);
        }

        pegCountThisShot = 0;
        ShotPoints = 0;
        // Multiplier dipertahankan lintas shot (sesuai fill meter); di-reset saat level reset.
    }

    /* ═════════ BONUS ═════════ */
    public static void AddFever(int pts)
    {
        FeverPoints += pts;
        AddToScores(pts);
    }
    public static void AddFreeBallBonus() => AddFever(freeBallBonus);
    public static void AddFeverBallBonus(int n, int p) => AddFever(n * p);

    /* ═════════ RESET ═════════ */
    public static void ResetLevelScores()
    {
        TotalScore = 0;     // ⬅️ penting: HUD kembali 0 di awal level
        LevelScore = 0;
        FeverPoints = 0;
        ShotPoints = 0;
        pegCountThisShot = 0;
        SetMultiplier(1);
        OnScoreChanged?.Invoke(TotalScore, 0);
    }

    /* ───── API dari FeverMeter ─────
       Panggil ini dengan fill 0..1. Threshold sesuai permintaanmu:
       25% ⇒ ×2, 50% ⇒ ×4, 75% ⇒ ×5, 90% ⇒ ×10
    */
    public static void SetMultiplierByFeverFill(float fill01)
    {
        SetMultiplierFromFever(fill01, 0.25f, 0.50f, 0.75f, 0.90f, 0f);
    }

    /* ───── HELPERS ───── */
    static void AddToScores(int delta)
    {
        LevelScore += delta;
        TotalScore += delta;
        OnScoreChanged?.Invoke(TotalScore, delta);
    }

    static void SetMultiplier(int m)
    {
        if (m == Multiplier) return;
        Multiplier = m;
        OnMultiplierChanged?.Invoke(Multiplier);
    }

    public static void SetMultiplierFromFever(
    float fill01, float t2, float t3, float t5, float t10, float slack)
    {
        // Gunakan ambang dari FeverMeter + toleransi (slack)
        int m =
            (fill01 >= t10 - slack) ? 10 :
            (fill01 >= t5 - slack) ? 5 :
            (fill01 >= t3 - slack) ? 3 :
            (fill01 >= t2 - slack) ? 2 : 1;

        SetMultiplier(m); // tetap lewat helper agar event terpanggil
    }

    /* ───── API utk UI ───── */
    public static int GetPegCount() => pegCountThisShot;

    /* ═════════ SAVE INTEGRATION ═════════
       Set skor total dari Save di awal level/perjalanan tanpa memunculkan popup “+delta”.
       Memicu OnScoreChanged dengan delta=0 agar HUD sinkron. */
    public static void SetTotalScore(int valueFromSave)
    {
        TotalScore = Mathf.Max(0, valueFromSave);
        OnScoreChanged?.Invoke(TotalScore, 0);
    }
}
