using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Mengaktifkan efek kartu yang dipilih di Card Management untuk level saat ini.
/// Dibaca di GameManager.StartLevel() → ApplyPickedEffectsFromSave().
/// </summary>
public class CardEffects : MonoBehaviour
{
    public static CardEffects I { get; private set; }

    // --- Auto-bootstrap: selalu ada tanpa perlu GO di scene ---
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (I != null) return;
        var go = new GameObject("CardEffects (Auto)");
        I = go.AddComponent<CardEffects>();
        DontDestroyOnLoad(go);
    }

    // ====== STATE COMMON CARDS ======
    public ElementType? firstShotElement { get; private set; }       // Flame/Water/Wind/Earth Infusion
    public int stoneBreakerRemaining { get; private set; } = 0;      // 2 hard pegs auto clear
    public float coinFreeBonus { get; private set; } = 0f;           // +0.25f => total 75%
    public float scoreBoostMultiplier { get; private set; } = 1f;    // 1.10f => +10% skor
    public float firstShotSpeedMultiplier { get; private set; } = 1f;// 1.10f => +10% speed tembakan 1
    public int freeBallThresholdDelta { get; private set; } = 0;     // -2 threshold
    public int killZoneBonusScore { get; private set; } = 0;         // +1000 saat mati oleh kill-zone

    // ====== STATE RARE CARDS ======
    /// <summary>Jumlah tembakan yang masih mendapatkan bucket kedua. Tiap kartu = +2 shot.</summary>
    public int doubleBucketShotsRemaining { get; private set; } = 0;
    public int saviorCharges { get; private set; } = 0;
    public bool tinySplitReady { get; private set; } = false;
    public bool freePowerReady { get; private set; } = false;
    public int extraGreenCount { get; private set; } = 0;

    // EPIC — Mirror Shot: jumlah tembakan yang masih dimirror (tiap kartu = +2 shot)
    public int mirrorShotsRemaining { get; private set; } = 0;
    public int minusOrangeCount { get; private set; } = 0;
    public bool elementRechargeActive { get; private set; } = false;
    public int finalGambitCharges { get; private set; } = 0; // tiap kartu = 1 charge (sekali selamatkan)
    public bool elementaryMasteryActive { get; private set; } = false; // dipakai Legendary 2

    bool _appliedToScoreMgr = false;
    public bool overdriveActive { get; private set; } = false;
    public int freeBallHitMultiplier { get; private set; } = 1; // 1x normal, 2x saat Overdrive

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        ResetAll();
    }

    public void ResetAll()
    {
        //Common
        firstShotElement = null;
        stoneBreakerRemaining = 0;
        coinFreeBonus = 0f;
        scoreBoostMultiplier = 1f;
        firstShotSpeedMultiplier = 1f;
        freeBallThresholdDelta = 0;
        killZoneBonusScore = 0;

        //Rare
        doubleBucketShotsRemaining = 0;
        saviorCharges = 0;
        tinySplitReady = false;
        freePowerReady = false;
        extraGreenCount = 0;

        //Epic
        mirrorShotsRemaining = 0;
        minusOrangeCount = 0;
        elementRechargeActive = false;
        finalGambitCharges = 0;
        elementaryMasteryActive = false;

        _appliedToScoreMgr = false;

        //Legendary
        overdriveActive = false;
        freeBallHitMultiplier = 1;
    }


    // ====== APPLY FROM SAVE ======
    /// <summary>Dipanggil GameManager.StartLevel()</summary>
    public void ApplyPickedEffectsFromSave()
    {
        // 1) reset state efek di awal level
        ResetAll();

        List<string> ids = null;

        if (SaveManager.I != null)
        {
            // a) Jika sudah ada snapshot aktif utk level ini → gunakan itu (Continue/Restart)
            var active = SaveManager.I.GetActiveCardsThisLevel();
            if (active != null && active.Count > 0)
            {
                ids = new List<string>(active);
            }
            else
            {
                // b) Pertama kali masuk level → konsumsi pickedForNext, kunci sebagai active, kurangi inventory
                ids = SaveManager.I.ConsumePickedForNext();        // ← ini mengosongkan pickedForNext + save
                if (ids != null && ids.Count > 0)
                {
                    SaveManager.I.SetActiveCardsThisLevel(ids);    // ← lock-in untuk level ini + save
                    CardInventory.I?.ConsumeOwnedByIds(ids);       // ← potong stok
                }
            }
        }
        else
        {
            ids = new List<string>();
        }

#if UNITY_EDITOR
        Debug.Log("[CardEffects] ConsumePickedForNext → [" + string.Join(",", ids) + "]");
#endif
        // NEW — kurangi stok kepemilikan 1x per id yang dipakai
        // (implementasikan CardInventory.ConsumeOwnedByIds(ids) sesuai struktur datamu)
        if (ids != null && ids.Count > 0)
        {
            CardInventory.I?.ConsumeOwnedByIds(ids);
        }

        // helper untuk normalisasi key (hapus spasi, lowercase)
        string Norm(string s) => string.IsNullOrWhiteSpace(s)
            ? ""
            : new string(s.Where(ch => !char.IsWhiteSpace(ch)).ToArray()).ToLowerInvariant();

        // 3) Terapkan efek satu per satu
        foreach (var id in ids)
        {
            var cd = CardLibrary.GetById(id);
            if (!cd) continue;

            var key = Norm(cd.effectKey);
            switch (key)
            {
                // ====== COMMON ======
                // Element Infusion — ambil yang pertama saja untuk tembakan pertama
                case "flameinfusion":
                case "waterinfusion":
                case "windinfusion":
                case "earthinfusion":
                    if (!firstShotElement.HasValue)
                    {
                        firstShotElement = key switch
                        {
                            "flameinfusion" => ElementType.Fire,
                            "waterinfusion" => ElementType.Water,
                            "windinfusion" => ElementType.Wind,
                            _ => ElementType.Earth,
                        };
                    }
                    break;

                case "stonebreaker": stoneBreakerRemaining += 2; break;
                case "luckyball": coinFreeBonus += 0.25f; break;
                case "scorebooster": scoreBoostMultiplier *= 1.10f; break;
                case "fasterball": firstShotSpeedMultiplier *= 1.10f; break;
                case "softtouch": freeBallThresholdDelta -= 2; break;

                // izinkan dua ejaan
                case "betterkillzone":
                case "betterkill-zone": killZoneBonusScore += 1000; break;

                // ====== RARE ======
                case "doublebucket": doubleBucketShotsRemaining += 2; break; // tiap kartu +2 tembakan
                case "savior": saviorCharges += 3; break; // tiap kartu +3 nyawa selamat
                case "tinysplit": tinySplitReady = true; break; // flag (sekali saja cukup)
                case "freepower": freePowerReady = true; break; // sekali pakai per level
                case "powerup": extraGreenCount += 1; break; // tiap kartu +1 green peg

                // ====== EPIC ======
                case "mirrorshot": mirrorShotsRemaining += 2; break; // 1 kartu = 2 tembakan awal
                case "minusorange": minusOrangeCount += 2; break; // 1 kartu = -2 orange
                case "elementrecharge": elementRechargeActive = true; break;
                case "finalgambit": finalGambitCharges += 1; break;
                case "elementarymastery": elementaryMasteryActive = true; break;

                case "overdrive":
                    overdriveActive = true;
                    freeBallHitMultiplier = 2;
                    break;
            }
        }

        // 4) Apply efek turunan ke sistem lain
        if (firstShotElement.HasValue)
            ElementSystem.SetNext(firstShotElement.Value);

        // jaga multiplier global minimal 1
        if (scoreBoostMultiplier < 1f) scoreBoostMultiplier = 1f;
        ScoreManager.SetGlobalScoreMultiplier(scoreBoostMultiplier);
        _appliedToScoreMgr = scoreBoostMultiplier > 1f;

#if UNITY_EDITOR
        Debug.Log(
            $"[CardEffects] Applied → elem={firstShotElement}, " +
            $"stoneBreaker={stoneBreakerRemaining}, coin+={coinFreeBonus}, " +
            $"scoreMul={scoreBoostMultiplier}, speed1st={firstShotSpeedMultiplier}, " +
            $"freeBallΔ={freeBallThresholdDelta}, kill+={killZoneBonusScore}, " +
            $"doubleBucketShots={doubleBucketShotsRemaining}, minusOrange={minusOrangeCount}, " +
            $"tinySplit={tinySplitReady}, freePower={freePowerReady}, mirrorShots={mirrorShotsRemaining}, " +
            $"savior={saviorCharges}, finalGambit={finalGambitCharges}, elemRecharge={elementRechargeActive}, " +
            $"elemMastery={elementaryMasteryActive}, extraGreen={extraGreenCount}"
        );
#endif
    }


    // ====== HOOKS dipanggil subsistem lain ======

    /// <summary>GameManager.UseBall() → setelah shotsTaken++</summary>
    public void AfterFirstShotApplied()
    {
        // Reset ke netral SETELAH peluru pertama sudah pakai infusion
        if (firstShotElement.HasValue)
        {
            firstShotElement = null;
            ElementSystem.SetNext(ElementType.Neutral);
        }
    }

    /// <summary>GameManager.RegisterHitPeg(...) untuk auto-clear hard peg (Stone Breaker).</summary>
    public bool TryConsumeStoneBreakerFor(PegController peg)
    {
        if (stoneBreakerRemaining <= 0) return false;
        if (peg == null || !peg.IsHard) return false;

        stoneBreakerRemaining--;
        return true; // caller silakan paksa clear
    }

    /// <summary>Dipanggil KillZone/BallController saat bola mati oleh kill-zone.</summary>
    public void OnBallKilledByKillZone()
    {
        if (killZoneBonusScore <= 0) return;

        // Masukkan ke antrean (aman kalau ada sistem lain yang baca)
        GameManager.Instance?.QueueKillZoneBonus(killZoneBonusScore);

        // ✅ tapi langsung bayar sekarang supaya HUD naik saat itu juga
        GameManager.Instance?.ApplyKillZoneBonusNow();
    }

    /// <summary>
    /// Dipanggil di awal setiap tembakan. Return true kalau DoubleBucket aktif untuk tembakan ini
    /// lalu menurunkan sisa shot.
    /// </summary>
    public bool ConsumeDoubleBucketForThisShot()
    {
        if (doubleBucketShotsRemaining <= 0) return false;
        doubleBucketShotsRemaining--;
        return true;
    }

    /// <summary>Consume 1 charge Savior jika masih ada. Return true jika terpakai.</summary>
    public bool TryConsumeSavior()
    {
        if (saviorCharges <= 0) return false;
        saviorCharges--;
        return true;
    }

    /// <summary>Consume Tiny Split jika masih ready. Return true jika terpakai.</summary>
    public bool TryConsumeTinySplit()
    {
        if (!tinySplitReady) return false;
        tinySplitReady = false;
        return true;
    }
    public bool TryConsumeFreePower()
    {
        if (!freePowerReady) return false;
        freePowerReady = false;
        return true;
    }
    public int TakeExtraGreenCount()
    {
        int c = extraGreenCount;
        extraGreenCount = 0;
        return c;
    }
    public bool ConsumeMirrorForThisShot()
    {
        if (mirrorShotsRemaining <= 0) return false;
        mirrorShotsRemaining--;
        return true;
    }
    public int TakeMinusOrangeCount()
    {
        int c = minusOrangeCount;
        minusOrangeCount = 0;
        return c;
    }

    public bool TryConsumeFinalGambit()
    {
        if (finalGambitCharges <= 0) return false;
        finalGambitCharges--;
        return true;
    }

    public static ElementType OppositeOf(ElementType e)
    {
        switch (e)
        {
            case ElementType.Fire: return ElementType.Water;
            case ElementType.Water: return ElementType.Fire;
            case ElementType.Wind: return ElementType.Earth;
            case ElementType.Earth: return ElementType.Wind;
            default: return ElementType.Neutral;
        }
    }
}
