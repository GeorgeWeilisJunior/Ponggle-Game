using System.Collections.Generic;
using UnityEngine;

public enum StyleShot
{
    ElementalFinale,   // hook eksternal (reaksi elemen)
    ReverseBucket,     // top wall → bucket
    AllInOne,          // semua jenis peg dalam 1 tembakan (versi dasar: Blue/Orange/Green)
    DoublePower,       // hook eksternal (2 green satu tembakan) — opsional
    LongShot,          // non-blue → ≥1/2 layar → non-blue lain
    SuperLongShot,     // non-blue → ≥2/3 layar → non-blue lain
    FreeBallSkill,     // kena 1 peg lalu langsung masuk bucket
    KickTheBucket,     // hook eksternal (bucket bounce → last orange) — opsional
    EndlessBounce,     // >20 detik sebelum masuk bucket
    ComboClear         // >50% peg level hancur dalam 1 tembakan
}

public static class StyleShotManager
{
    /* —— konfigurasi —— */
    const float endlessBounceSec = 20f;

    static readonly Dictionary<StyleShot, int> Points = new()
    {
        { StyleShot.ElementalFinale, 25_000 },
        { StyleShot.ReverseBucket,   25_000 },
        { StyleShot.AllInOne,        25_000 },
        { StyleShot.DoublePower,     25_000 },
        { StyleShot.LongShot,        50_000 },
        { StyleShot.SuperLongShot,   50_000 },
        { StyleShot.FreeBallSkill,   50_000 },
        { StyleShot.KickTheBucket,   50_000 },
        { StyleShot.EndlessBounce,   50_000 },
        { StyleShot.ComboClear,      75_000 },
    };

    /* —— state per tembakan —— */
    static bool started;
    static float shotStartTime;
    static int hitsThisShot;
    static bool touchedTopWall;
    static readonly HashSet<PegType> hitTypes = new();

    // long shot tracker
    static bool longShotGiven, superLongGiven;
    static Vector3? firstNonBluePos;

    // dimensinya dihitung saat StartShot
    static float halfScreenWorld, twoThirdScreenWorld;

    // untuk Combo Clear
    static int totalPegsLevel;

    public static void InitLevel(int totalPegs) => totalPegsLevel = Mathf.Max(1, totalPegs);

    public static void StartShot()
    {
        started = true;
        shotStartTime = Time.time;
        hitsThisShot = 0;
        touchedTopWall = false;
        hitTypes.Clear();
        longShotGiven = superLongGiven = false;
        firstNonBluePos = null;

        // konversi tinggi layar ke world unit
        var cam = Camera.main;
        float worldH = cam.orthographicSize * 2f;
        halfScreenWorld = worldH * 0.5f;
        twoThirdScreenWorld = worldH * (2f / 3f);
    }

    public static void OnPegHit(PegType type, Vector3 worldPos)
    {
        if (!started) return;
        hitsThisShot++;
        hitTypes.Add(type);

        // Long / Super Long: hanya untuk non-blue pair
        if (type != PegType.Blue)
        {
            if (firstNonBluePos == null)
            {
                firstNonBluePos = worldPos;
            }
            else
            {
                float d = Vector3.Distance(firstNonBluePos.Value, worldPos);
                if (!superLongGiven && d >= twoThirdScreenWorld)
                {
                    Award(StyleShot.SuperLongShot, worldPos);
                    superLongGiven = true;
                }
                else if (!longShotGiven && d >= halfScreenWorld)
                {
                    Award(StyleShot.LongShot, worldPos);
                    longShotGiven = true;
                }
            }
        }
    }

    public static void OnTopWallBounce() { if (started) touchedTopWall = true; }

    public static void OnBucketCatch(Vector3 atWorld)
    {
        if (!started) return;

        // Reverse Bucket
        if (touchedTopWall) Award(StyleShot.ReverseBucket, atWorld);

        // Free Ball Skill (1 peg → langsung bucket)
        if (hitsThisShot == 1) Award(StyleShot.FreeBallSkill, atWorld);

        // Endless Bounce
        if (Time.time - shotStartTime >= endlessBounceSec)
            Award(StyleShot.EndlessBounce, atWorld);
    }

    /// <summary> Panggil setelah cleanup tembakan selesai, kirim berapa peg yang benar-benar hancur pada tembakan ini. </summary>
    public static void EndShot(Vector3 popupCenter, int clearedThisShot)
    {
        if (!started) return;

        // All-In-One (versi dasar: Blue + Orange + Green)
        if (hitTypes.Contains(PegType.Blue) && hitTypes.Contains(PegType.Orange) && hitTypes.Contains(PegType.Green))
            Award(StyleShot.AllInOne, popupCenter);

        // Combo Clear
        if (clearedThisShot > totalPegsLevel * 0.5f)
            Award(StyleShot.ComboClear, popupCenter);

        started = false;
    }

    /* —— Hook eksternal untuk style yang berasal dari sistem lain —— */
    public static void TriggerElementalFinale(Vector3 worldPos) =>
        Award(StyleShot.ElementalFinale, worldPos);

    public static void TriggerKickTheBucket(Vector3 worldPos) =>
        Award(StyleShot.KickTheBucket, worldPos);

    public static void TriggerDoublePower(Vector3 worldPos) =>
        Award(StyleShot.DoublePower, worldPos);

    /* —— internal —— */
    static void Award(StyleShot type, Vector3 worldPos)
    {
        if (!Points.TryGetValue(type, out int pts)) return;

        // skor bonus (jalur yang sama dengan free ball / hole)
        ScoreManager.AddFever(pts);

        // popup
        if (ScoreUI.Instance)
            ScoreUI.Instance.ShowStyle(type.ToString().Replace("_", " "), pts, worldPos);

        // SFX opsional
        AudioManager.I?.Play("StyleShot", worldPos);

        // setelah diberi, nolkan poinnya agar tidak dobel di tembakan yang sama
        Points[type] = 0;
    }

    /// <summary> Reset pool poin style agar bisa dapat lagi di tembakan/level berikutnya. </summary>
    public static void ResetStylePointPool()
    {
        Points[StyleShot.ElementalFinale] = 25_000;
        Points[StyleShot.ReverseBucket] = 25_000;
        Points[StyleShot.AllInOne] = 25_000;
        Points[StyleShot.DoublePower] = 25_000;
        Points[StyleShot.LongShot] = 50_000;
        Points[StyleShot.SuperLongShot] = 50_000;
        Points[StyleShot.FreeBallSkill] = 50_000;
        Points[StyleShot.KickTheBucket] = 50_000;
        Points[StyleShot.EndlessBounce] = 50_000;
        Points[StyleShot.ComboClear] = 75_000;
    }
}
