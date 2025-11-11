using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D; // Built-in RP PixelPerfectCamera
using URPPPC = UnityEngine.Experimental.Rendering.Universal.PixelPerfectCamera; // URP PPC
using DG.Tweening;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /*────────────────────── Inspector ──────────────────────*/
    [Header("Gameplay")]
    [SerializeField] int startingBalls = 10;
    [SerializeField] BucketController bucket;
    [SerializeField] FeverMeterController feverMeter;
    [SerializeField] FreeBallMeterUI freeBallMeter;

    [Header("End Panel")]
    [SerializeField] EndLevelPanel endPanel;              // ⬅️ panel Win/Lose

    [Header("Rainbow Peg")]
    [SerializeField] Color rainbowIdleColor = Color.white;

    [Header("Peg Popup")]
    [SerializeField] PegScorePopup scorePopupPrefab;
    [SerializeField] Transform popupParent;

    [Header("Fever Popup")]
    [SerializeField] FeverScorePopup feverScorePopup;
    [SerializeField] float feverHold = 2f;
    [SerializeField] int feverBallBonus = 10000;

    // === Coin result tanpa animator ===
    [Header("Coin-Flip Result (no animation)")]
    [SerializeField] Image feedbackImage;
    [SerializeField] Sprite coinFreeSprite;
    [SerializeField] Sprite coinNoSprite;

    [Header("Coin Result UI Layout")]
    [SerializeField] Vector2 coinResultSize = new Vector2(200, 200);
    [SerializeField] Vector2 coinResultAnchoredPos = Vector2.zero;
    [SerializeField] bool enforceCenterAnchor = true;

    [Header("Coin Result Transition")]
    [SerializeField, Min(0f)] float fadeInDuration = 0.18f;
    [SerializeField, Min(0f)] float holdDuration = 0.35f;
    [SerializeField, Min(0f)] float fadeOutDuration = 0.20f;
    [SerializeField, Range(0.5f, 1.2f)] float appearScale = 0.85f;

    [Header("Coin Result SFX")]
    [SerializeField] bool sfxUseUI = true;
    [SerializeField] Transform sfxAnchor;
    [SerializeField] string sfxFreeKey = "CoinFree";
    [SerializeField] string sfxNoKey = "CoinNo";

    // ======= Music Keys (pakai AudioManager) =======
    [Header("Music Keys (AudioManager)")]
    [SerializeField] string musicGameplayKey = "Music_Gameplay_Stage1";
    [SerializeField] string musicFeverKey = "Music_Fever";

    [Header("Fever Slow-mo")]
    [SerializeField, Range(.2f, 1f)] float feverSlowFactor = .5f;
    [SerializeField] float feverGravityFactor = 1.25f;

    [Header("Last-Orange Cinematic (time slow)")]
    [SerializeField] float cinematicSlowFactor = .1f;
    [SerializeField] float cinematicTriggerDist = .6f;
    [SerializeField] float cinematicDelay = .3f;

    [Header("Last-Orange Camera")]
    [SerializeField] bool cameraZoom = true;
    [SerializeField] float zoomDuration = 0.18f;
    [SerializeField] Vector2 zoomOffset = Vector2.zero;

    [Space(6)]
    [SerializeField] bool cameraZoomUsePPU = true;
    [SerializeField] int zoomPPU = 160;
    [SerializeField] float zoomOrthoSizeFallback = 3.8f;

    [Header("Last-Orange Prediction")]
    [SerializeField] LayerMask pegLayerMask;
    [SerializeField] float lookAheadTime = .3f;
    [SerializeField] float triggerMaxTimeAhead = 0.35f;
    [SerializeField] float willHitDistanceGate = 0.9f;
    [SerializeField] float exitCinematicDist = .75f;

    [Header("Last-Orange Timing")]
    [SerializeField] float minCinematicHold = 1.2f;
    [SerializeField, Range(0f, 1f)] float directionDotThreshold = 0.6f;
    [SerializeField] float rearmCooldown = 0.6f;

    [Header("Debug Camera Zoom")]
    [SerializeField] bool debugZoomHotkey = true;
    [SerializeField] KeyCode zoomTestKey = KeyCode.Z;
    [SerializeField] bool debugLogEveryFrame = false;

    [Header("Camera Bounds (prevent OOB)")]
    [SerializeField] bool confineCamera = true;
    [SerializeField] Collider2D cameraBoundsCollider;       // BoxCollider2D yang mengelilingi area playfield
    [SerializeField] Rect fallbackBounds = new Rect(-8.5f, -4.7f, 17f, 9.4f); // dipakai kalau collider null
    [SerializeField] float boundsPadding = 0.20f;           // jarak aman dari tepi

    /* ▼▼ Fever: Replace Fireball → Normal ▼▼ */
    [Header("Fever: Replace Fireball")]
    [Tooltip("Ubah semua AposdaFireball yang masih aktif jadi bola normal ketika Fever mulai.")]
    [SerializeField] bool replaceFireballOnFever = true;
    [Tooltip("Transisi halus (fade cross-fade) saat berubah ke bola normal.")]
    [SerializeField] bool smoothReplaceFireball = true;
    [SerializeField, Min(0f)] float replaceFadeDuration = 0.12f;

    [Tooltip("Prefab bola normal yang dipakai saat pengganti fireball (isi dengan prefab bola normal).")]
    [SerializeField] BallController normalBallPrefab;

    [Header("Fever Banner")]
    [SerializeField] FeverModeBanner feverBanner;
    /* ▲▲ -------------------------------- ▲▲ */

    [Header("Level Intro")]
    [SerializeField] bool playPegIntro = true;
    [SerializeField] PegIntroPop introPop;   // drag object LevelIntroFX ke sini

    [Header("Progress Saved (Level Start Toast)")]
    [SerializeField] CanvasGroup savedToast;      // drag HUD/Saved ke sini
    [SerializeField] Vector2 savedToastPos = new Vector2(520f, -490f);
    [SerializeField] float savedFadeIn = 0.20f;
    [SerializeField] float savedHold = 0.80f;
    [SerializeField] float savedFadeOut = 0.25f;
    [SerializeField] bool showSavedOnLevelStart = true;

    /*────────────────────── Runtime ──────────────────────*/
    public int BallsLeft { get; private set; }
    public int CurrentShotId => shotsTaken;
    public bool HasBalls => BallsLeft > 0;
    public GameState State { get; private set; } = GameState.Idle;
    public bool IsFlipping { get; private set; }
    public bool InFever => State == GameState.FeverFinalTurn;
    public static int Multiplier => ScoreManager.Multiplier;

    public enum BallEndReason { None, KillZone, Bucket, FeverHole }

    BallEndReason _lastBallEndReason = BallEndReason.None;

    public event Action<int> OnBallsLeftChanged;
    public event Action OnBallUsed, OnBallGained;
    public event Action<LevelStats> onLevelSuccess;

    int shotsTaken, freeBallsEarned; bool firstTry = true;
    int totalPegCount, clearedPegCount;

    int orangeTotal, orangeCleared, orangeRemaining;
    readonly List<PegController> hitPegs = new();

    PegController currentRainbowPeg;

    bool feverStarted; int ballsLeftWhenFeverBegan;

    bool IsScoring;
    bool lastOrangeMode;
    bool cinematicPlaying;
    Transform lastOrangePeg;
    float lastOrangeReadyTime;
    float cinematicStartRT;
    float nextCinematicAllowedTime;

    Camera cam;
    PixelPerfectCamera ppc2D;
    URPPPC ppcURP;
    float camOrigSize;
    Vector3 camOrigPos;

    int ppcOrigPPU;
    Coroutine ppuTween;
    bool zoomedByPPU;

    Coroutine feedbackRoutine;
    float endTurnDelayUntilRT = 0f;

    bool loseCommitted = false;

    int _pendingKillZoneBonus = 0;
    bool _killZoneHappenedThisTurn = false;

    BucketController extraBucket = null;
    bool extraBucketSpawnedThisShot = false;
    bool _randomizedThisLevel;

    readonly HashSet<int> _creditedHardOrange = new();

    /*═════════════ Helpers PPC ═════════════*/
    int GetCurrentPPU()
    {
        if (ppc2D) return ppc2D.assetsPPU;
        if (ppcURP) return ppcURP.assetsPPU;
        return -1;
    }

    int GetStartingBalls()
    {
        // Baca dari GlobalSettings kalau ada, fallback ke PlayerPrefs
        bool easy = PlayerPrefs.GetInt("set_easymode", 0) == 1;
        try { easy |= GlobalSettings.EasyMode; } catch { /* jika classnya belum ada */ }

        int baseBalls = Mathf.Max(0, startingBalls); // startingBalls milik inspector (default 10)
        return baseBalls + (easy ? 5 : 0);           // 10 atau 15
    }
    void SetCurrentPPU(int v)
    {
        if (ppc2D && ppc2D.assetsPPU != v) ppc2D.assetsPPU = v;
        if (ppcURP && ppcURP.assetsPPU != v) ppcURP.assetsPPU = v;
    }
    bool HasAnyPPC() => ppc2D || ppcURP;

    void ResolveCameraAndPPC()
    {
        if (!cam) cam = Camera.main;

        if (cam)
        {
            if (!ppc2D) ppc2D = cam.GetComponent<PixelPerfectCamera>();
            if (!ppcURP) ppcURP = cam.GetComponent<URPPPC>();
        }

        if (!ppc2D) ppc2D = FindObjectOfType<PixelPerfectCamera>(true);
        if (!ppcURP) ppcURP = FindObjectOfType<URPPPC>(true);

        if (!cam)
        {
            var c2d = ppc2D ? ppc2D.GetComponent<Camera>() : null;
            var curp = ppcURP ? ppcURP.GetComponent<Camera>() : null;
            cam = c2d ? c2d : curp;
        }

        if (cam)
        {
            camOrigPos = cam.transform.position;
            camOrigSize = cam.orthographicSize;
        }

        int curPPU = GetCurrentPPU();
        if (curPPU > 0) ppcOrigPPU = curPPU;
    }

    /*═════════════ LIFECYCLE ═════════════*/
    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        int startBalls = GetStartingBalls();
        BallsLeft = startBalls;

        if (SaveManager.I != null)
        {
            // (opsional) sinkronkan nilai di save agar tidak menyimpan 0/1 ke depan
            SaveManager.I.Data.ballsLeft = startBalls;
            // SaveManager.I.SaveToDisk();  // kalau mau benar-benar commit
        }
        ResolveCameraAndPPC();

        if (feedbackImage)
        {
            NormalizeCoinRect();
            feedbackImage.preserveAspect = true;
            feedbackImage.raycastTarget = false;
            feedbackImage.gameObject.SetActive(false);
        }
    }

    void OnEnable() => ResolveCameraAndPPC();

    IEnumerator Start()
    {
        yield return null;
        StartLevel();
    }

    /*════════════ LEVEL INIT ═════════════*/
    void StartLevel()
    {
        loseCommitted = false;
        shotsTaken = freeBallsEarned = 0; firstTry = true;
        SyncInventoryFromSave();
        DespawnExtraBucket();

        var pegs = FindObjectsOfType<PegController>();
        totalPegCount = pegs.Length;
        orangeTotal = pegs.Count(p => p.Type == PegType.Orange);
        orangeCleared = 0;
        orangeRemaining = orangeTotal;
        clearedPegCount = 0;

        feverStarted = false;
        lastOrangeMode = false;
        cinematicPlaying = false;
        nextCinematicAllowedTime = 0f;
        _pendingKillZoneBonus = 0;
        _killZoneHappenedThisTurn = false;

        ScoreManager.ResetLevelScores();
        StyleShotManager.InitLevel(totalPegCount);
        StyleShotManager.ResetStylePointPool();

        // --- Mark & save posisi level sekarang secara defensif ---
        if (SaveManager.I != null)
        {
            int idx = LevelManager.Instance ? LevelManager.Instance.CurrentIndex : SaveManager.I.Data.levelIndex;
            SaveManager.I.Data.levelIndex = Mathf.Clamp(idx, 0,
                (LevelManager.Instance && LevelManager.Instance.levelScenes != null)
                    ? LevelManager.Instance.levelScenes.Length - 1
                    : SaveManager.TOTAL_LEVELS - 1);

            int lps = (LevelManager.Instance && LevelManager.Instance.levelsPerStage > 0)
                        ? LevelManager.Instance.levelsPerStage : 5;
            SaveManager.I.Data.stageIndex = SaveManager.I.Data.levelIndex / Mathf.Max(1, lps);
            SaveManager.I.SaveToDisk();
        }

        ShowSavedToastNow();

        State = GameState.Idle;

        Time.timeScale = 1f;
        Time.fixedDeltaTime = .02f;

        // Tentukan BGM berdasarkan stage
        string stageMusicKey = null;
        if (LevelManager.Instance != null)
            stageMusicKey = LevelManager.Instance.GetCurrentStageBgmKey();

        // Kalau tidak ada, fallback ke default inspector
        if (string.IsNullOrEmpty(stageMusicKey))
            stageMusicKey = musicGameplayKey;

        if (!string.IsNullOrEmpty(stageMusicKey))
            AudioManager.I?.PlayMusicIfChanged(stageMusicKey, true);

        ElementSystem.SetNext(ElementType.Neutral);
        CardEffects.I?.ApplyPickedEffectsFromSave();

        ApplyPowerUpExtraGreens();
        ApplyMinusOrangeThenRefreshCounts();

        if (totalPegCount > 5)
        {
            int idx = LevelManager.Instance ? LevelManager.Instance.CurrentIndex : 0;
            if (idx >= 7) SelectNewRainbowPeg();
        }

        // Pastikan banner tersembunyi di awal
        feverBanner?.HideImmediate();
        PegIntroPop.ResetFlags();
        StartCoroutine(RunInitialRandomizersThenIntro());
    }
    IEnumerator RunInitialRandomizersThenIntro()
    {
        yield return null;
        // Pastikan guard PegRandomizer bersih di awal level
        PegRandomizer.ResetRunGuards();

        // 1) Jalankan PegRandomizer (swap posisi dulu)
        var pegRand = FindObjectOfType<PegRandomizer>(true);
        if (pegRand != null)
        {
            bool done = false;
            System.Action cb = () => done = true;
            PegRandomizer.OnRandomizeDone += cb;
            pegRand.TryRandomize();

            float until = Time.realtimeSinceStartup + 1f; // timeout aman
            while (!done && Time.realtimeSinceStartup < until) yield return null;
            PegRandomizer.OnRandomizeDone -= cb;
        }

        // 2) Lalu ElementPairRandomizer (pilih 2 elemen & apply)
        var elemRand = FindObjectOfType<ElementPairRandomizer>(true);
        if (elemRand != null) elemRand.RandomizeNow();

        // 3) Beri 1 frame agar visual settle
        yield return null;

        // 4) Baru mainkan intro pop
        if (playPegIntro && introPop)
            yield return StartCoroutine(introPop.Play());
    }

    IEnumerator RunInitialRandomizers()
    {
        // Tunggu intro benar-benar selesai
        while (PegIntroPop.IsIntroPlaying) yield return null;
        yield return null; // beri 1 frame agar semua peg aktif

        // Reset guard PegRandomizer (penting saat Retry dalam 1 sesi)
        PegRandomizer.ResetRunGuards();

        // 1) Swap posisi peg (orange/green vs blue) dulu
        var pegRand = FindObjectOfType<PegRandomizer>(true);
        if (pegRand != null)
        {
            bool done = false;
            System.Action cb = () => done = true;
            PegRandomizer.OnRandomizeDone += cb;
            pegRand.TryRandomize();

            // tunggu selesai (fallback timeout 1 detik realtime)
            float until = Time.realtimeSinceStartup + 1f;
            while (!done && Time.realtimeSinceStartup < until) yield return null;
            PegRandomizer.OnRandomizeDone -= cb;
        }

        // 2) Baru pilih 2 elemen & terapkan
        var elemRand = FindObjectOfType<ElementPairRandomizer>(true);
        if (elemRand != null) elemRand.RandomizeNow();
    }

    /*════════════ BALL MANAGEMENT ════════════*/
    public void UseBall()
    {
        if (!HasBalls) return;
        BallsLeft--; shotsTaken++;
        OnBallUsed?.Invoke();
        OnBallsLeftChanged?.Invoke(BallsLeft);
        State = GameState.BallInPlay;
        StyleShotManager.StartShot();
        TrySpawnExtraBucketForThisShot();
    }

    public int GainBall(int amt = 1)
    {
        const int MAX_BALLS = 20;              // CAP Ball-O-Tron
        if (amt <= 0) return 0;

        int before = BallsLeft;
        int space = Mathf.Max(0, MAX_BALLS - before);
        int granted = Mathf.Min(amt, space);

        if (granted > 0)
        {
            BallsLeft += granted;
            freeBallsEarned += granted;
            OnBallGained?.Invoke();
            OnBallsLeftChanged?.Invoke(BallsLeft);
        }
#if UNITY_EDITOR
        if (granted < amt) Debug.Log($"[GM] Ball-O-Tron penuh ({before}/{MAX_BALLS}) → {amt - granted} ditolak");
#endif
        return granted;
    }

    /*════════════ PEG HIT ════════════*/
    public void RegisterHitPeg(PegController peg, BallController ball)
    {
        bool firstHit = !hitPegs.Contains(peg);

        if (firstHit && peg.Type == PegType.Orange && CardEffects.I != null)
        {
            if (CardEffects.I.TryConsumeFreePower())
            {
                CharacterPowerManager.Instance?.TryActivatePower();
                // (opsional) SFX/feedback kecil:
                try { AudioManager.I.Play("PowerActivate", peg.transform.position); } catch { }
            }
        }

        if (firstHit && ball != null && peg.Type == PegType.Blue && CardEffects.I != null)
        {
            if (CardEffects.I.TryConsumeTinySplit())
            {
                DoTinySplit(ball);
            }
        }

        if (firstHit && ball != null && peg.IsHard && CardEffects.I != null && CardEffects.I.TryConsumeStoneBreakerFor(peg))
        {
            peg.ClearNow();                       // force clear
            RegisterPegCleared(peg.Type == PegType.Orange, true);
            return;                               // selesai untuk hit ini
        }
        if (firstHit) hitPegs.Add(peg);

        if (firstHit && scorePopupPrefab)
        {
            int basePts = peg.Type == PegType.Orange ? 100 :
                          peg.Type == PegType.Rainbow ? 500 : 100;
            int amount = InFever ? 10000 : basePts * ScoreManager.Multiplier;
            Instantiate(scorePopupPrefab, peg.transform.position, Quaternion.identity, popupParent).Show(amount);
        }

        if (firstHit && peg.Type == PegType.Orange && !peg.IsHard)
        {
            orangeRemaining = Mathf.Max(0, orangeRemaining - 1);
            feverMeter?.AddOrangeHitInstant();

            // NEW: orange HARD → pada transisi 2x→1x, naikkan meter langsung sekali
            if (firstHit && peg.Type == PegType.Orange && peg.IsHard && peg.HitsRemaining == 1)
            {
                // pakai overload dgn ID agar tidak dobel kalau tembakan berikutnya kena lagi
                feverMeter?.AddOrangeHitInstant(peg.GetInstanceID());
            }

            if (orangeRemaining == 1)
            {
                var lastPeg = FindObjectsOfType<PegController>()
                              .FirstOrDefault(p => p.Type == PegType.Orange &&
                                                   p != peg &&
                                                   p.State != PegController.PegState.Cleared);
                if (lastPeg)
                {
                    if (lastPeg.IsHard && lastPeg.HitsRemaining >= 2)
                    { lastOrangeMode = false; lastOrangePeg = null; }
                    else
                    { EnterLastOrangeMode(lastPeg); lastOrangeReadyTime = Time.time + cinematicDelay; }
                }
            }

            if (orangeRemaining == 0 && !feverStarted && State != GameState.EndLevelSuccess)
                TriggerFever(ball);
        }

        if (!InFever && firstHit && peg.Type == PegType.Green && !peg.IsHard)
            CharacterPowerManager.Instance?.TryActivatePower();

        int hitCount = peg.Type == PegType.Rainbow ? 5 : 1;
        for (int i = 0; i < hitCount; i++) freeBallMeter?.RegisterPegHit();

        ScoreManager.AddPegHit(peg.Type, orangeCleared, orangeTotal);

        if (!feverStarted && peg.Type == PegType.Orange && peg.IsHard)
        {
            var remainingOranges = FindObjectsOfType<PegController>()
                .Where(p => p.Type == PegType.Orange && p.State != PegController.PegState.Cleared)
                .ToList();

            if (remainingOranges.Count == 1 && remainingOranges[0] == peg && peg.HitsRemaining == 1)
            { EnterLastOrangeMode(peg); lastOrangeReadyTime = Time.time + cinematicDelay; }
        }

        if (peg == currentRainbowPeg && peg.State == PegController.PegState.Cleared)
            currentRainbowPeg = null;

        StyleShotManager.OnPegHit(peg.Type, peg.transform.position);
    }

    public void RegisterPegCleared(bool isOrange, bool wasHard)
    {
        clearedPegCount++;

        if (isOrange)
        {
            orangeCleared++;
            orangeRemaining = Mathf.Max(0, orangeTotal - orangeCleared);

            if (wasHard)
            {
                // no-op: sudah dikredit saat transisi 1x→0x
            }

            if (orangeRemaining == 1 && !feverStarted)
            {
                var lastPeg = FindObjectsOfType<PegController>()
                              .FirstOrDefault(p => p.Type == PegType.Orange &&
                                                   p.State != PegController.PegState.Cleared);
                if (lastPeg)
                {
                    if (lastPeg.IsHard && lastPeg.HitsRemaining >= 2)
                    { lastOrangeMode = false; lastOrangePeg = null; }
                    else
                    { EnterLastOrangeMode(lastPeg); lastOrangeReadyTime = Time.time + cinematicDelay; }
                }
            }

            if (orangeRemaining == 0 && !feverStarted && State != GameState.EndLevelSuccess)
                TriggerFever(null);
        }
    }

    public void NotifyBallEnd(BallEndReason reason)
    {
        _lastBallEndReason = reason;
    }

    public void NotifyKillZoneThisTurn()
    {
        _killZoneHappenedThisTurn = true;
    }

    /*════════════ LAST-ORANGE CINEMATIC ════════════*/
    void EnterLastOrangeMode(PegController peg)
    {
        lastOrangeMode = true;
        lastOrangePeg = peg.transform;
    }

    public void CheckLastOrangeCinematic(BallController ball)
    {
        if (cinematicPlaying && !feverStarted)
        {
            if (!lastOrangePeg) { EndLastOrangeCinematic(); return; }
            bool exited = Vector2.Distance(ball.transform.position, lastOrangePeg.position) > exitCinematicDist;
            bool holdMet = Time.unscaledTime - cinematicStartRT >= minCinematicHold;
            if (exited && holdMet) { EndLastOrangeCinematic(); return; }
        }

        if (!lastOrangeMode || cinematicPlaying || feverStarted) return;
        if (!lastOrangePeg) { lastOrangeMode = false; return; }
        if (Time.time < lastOrangeReadyTime) return;
        if (Time.time < nextCinematicAllowedTime) return;

        Vector2 pos = ball.transform.position;
        Vector2 vel = ball.Velocity;
        float speed = vel.magnitude;

        Vector2 toPeg = (Vector2)lastOrangePeg.position - pos;
        float distToPeg = toPeg.magnitude;
        float castDist = Mathf.Min(speed * lookAheadTime, distToPeg + 0.05f);

        bool willHit = false;
        if (speed > 0.01f && castDist > 0.01f)
        {
            var hit = Physics2D.CircleCast(pos, ball.Radius, vel.normalized, castDist, pegLayerMask);
            if (hit && hit.transform == lastOrangePeg)
            {
                float tHit = hit.distance / Mathf.Max(0.001f, speed);
                if (tHit <= triggerMaxTimeAhead || hit.distance <= willHitDistanceGate)
                    willHit = true;
            }
        }

        bool nearFacing = false;
        if (!willHit && distToPeg <= cinematicTriggerDist && speed > 0.0001f)
        {
            float dirDot = Vector2.Dot(vel.normalized, toPeg.normalized);
            bool facing = dirDot >= directionDotThreshold;

            // ⬇️ NEW: hanya sah jika ada line-of-sight ke last-orange
            if (facing)
                nearFacing = HasLineOfSightToLastOrange(pos, ball.Radius);
        }

        if (willHit || nearFacing) PlayLastOrangeCinematic(ball);
    }

    void PlayLastOrangeCinematic(BallController ball)
    {
        ResolveCameraAndPPC();

        cinematicPlaying = true;
        lastOrangeMode = false;
        cinematicStartRT = Time.unscaledTime;
        nextCinematicAllowedTime = Time.time + rearmCooldown;

        DOTween.Kill("TimeFX");
        DOTween.To(() => Time.timeScale,
                   x => { Time.timeScale = x; Time.fixedDeltaTime = .02f * x; },
                   cinematicSlowFactor, .10f)
               .SetId("TimeFX")
               .SetUpdate(true);

        if (cameraZoom && cam && lastOrangePeg)
        {
            camOrigSize = cam.orthographicSize;
            camOrigPos = cam.transform.position;

            // Posisi target sebelum clamp
            Vector3 targetPos = new Vector3(
                lastOrangePeg.position.x + zoomOffset.x,
                lastOrangePeg.position.y + zoomOffset.y,
                camOrigPos.z
            );

            // ====== NEW: Clamp target agar tidak keluar area playfield ======
            if (confineCamera)
            {
                // Hitung half-height viewport SETELAH zoom
                float targetHalfH;
                if (cameraZoomUsePPU && HasAnyPPC())
                {
                    if (ppcOrigPPU <= 0) ppcOrigPPU = GetCurrentPPU();
                    int fromPPU = Mathf.Max(1, ppcOrigPPU);
                    int toPPU = Mathf.Max(1, zoomPPU);
                    float scale = (float)toPPU / fromPPU;        // >1 berarti zoom-in
                    targetHalfH = camOrigSize / Mathf.Max(0.0001f, scale);
                }
                else
                {
                    targetHalfH = zoomOrthoSizeFallback;         // target ortho size setelah tween
                }
                float targetHalfW = targetHalfH * cam.aspect;

                // Ambil bounds dunia + padding
                Rect r = GetWorldBoundsRect();
                r.xMin += boundsPadding; r.yMin += boundsPadding;
                r.xMax -= boundsPadding; r.yMax -= boundsPadding;

                // Jika viewport lebih besar dari bounds, center saja
                if (r.width < targetHalfW * 2f || r.height < targetHalfH * 2f)
                {
                    targetPos = new Vector3(r.center.x, r.center.y, targetPos.z);
                }
                else
                {
                    targetPos = new Vector3(
                        Mathf.Clamp(targetPos.x, r.xMin + targetHalfW, r.xMax - targetHalfW),
                        Mathf.Clamp(targetPos.y, r.yMin + targetHalfH, r.yMax - targetHalfH),
                        targetPos.z
                    );
                }
            }
            // ====== END NEW ======

            DOTween.Kill("CamPan");
            cam.transform.DOMove(targetPos, zoomDuration).SetUpdate(true).SetId("CamPan");

            if (cameraZoomUsePPU && HasAnyPPC())
            {
                if (ppuTween != null) StopCoroutine(ppuTween);
                if (ppcOrigPPU <= 0) ppcOrigPPU = GetCurrentPPU();
                zoomedByPPU = true;
                ppuTween = StartCoroutine(TweenPPU(GetCurrentPPU(), Mathf.Max(1, zoomPPU), zoomDuration));
            }
            else
            {
                DOTween.Kill("CamZoomOrtho");
                DOTween.To(() => cam.orthographicSize, s => cam.orthographicSize = s,
                           zoomOrthoSizeFallback, zoomDuration)
                       .SetUpdate(true).SetId("CamZoomOrtho");
            }
        }

        AudioManager.I.Play("LastOrangeSting", transform.position);
    }

    void ShowSavedToastNow()
    {
        if (!showSavedOnLevelStart || !savedToast) return;

        var rt = savedToast.GetComponent<RectTransform>();
        if (rt) rt.anchoredPosition = savedToastPos;

        savedToast.gameObject.SetActive(true);
        savedToast.DOKill();
        savedToast.alpha = 0f;

        savedToast.DOFade(1f, Mathf.Max(0.0001f, savedFadeIn))
            .SetUpdate(true)
            .OnComplete(() =>
            {
                DOVirtual.DelayedCall(Mathf.Max(0f, savedHold), () =>
                {
                    savedToast.DOFade(0f, Mathf.Max(0.0001f, savedFadeOut))
                              .SetUpdate(true)
                              .OnComplete(() => savedToast.gameObject.SetActive(false));
                }, ignoreTimeScale: true);
            });
    }
    void SyncInventoryFromSave()
    {
        if (SaveManager.I == null || CardInventory.I == null) return;

        var inv = CardInventory.I;
        var data = SaveManager.I.Data;

        inv.ClearAll(); // kosongkan runtime

        // rebuild owned
        if (data.ownedCards != null)
        {
            foreach (var id in data.ownedCards)
            {
                var cd = CardLibrary.GetById(id);
                if (cd) inv.AddCard(cd);
            }
        }

        // energy limit
        inv.SetEnergyLimit(Mathf.Max(1, data.energyLimit));

        // rebuild picked
        if (data.pickedForNext != null)
        {
            foreach (var id in data.pickedForNext)
            {
                var cd = CardLibrary.GetById(id);
                if (cd) inv.TryPick(cd, out _);
            }
        }
    }

    // Tambahkan method ini
    public void RegisterIndirectPegHit(PegController peg)
    {
        if (peg == null) return;
        // Hindari double-entries
        var already = hitPegs.Contains(peg);
        if (!already) hitPegs.Add(peg);

        // Catatan:
        //  - Tidak ada popup skor, tidak konsumsi StoneBreaker, dsb.
        //  - Tujuan hanya agar peg ikut diproses OnEndTurnCleanup()
    }
    void EndLastOrangeCinematic()
    {
        if (!cinematicPlaying) return;
        cinematicPlaying = false;

        DOTween.Kill("TimeFX");
        DOTween.To(() => Time.timeScale,
                   x => { Time.timeScale = x; Time.fixedDeltaTime = .02f * x; },
                   1f, .15f)
               .SetId("TimeFX")
               .SetUpdate(true);

        if (cam)
        {
            DOTween.Kill("CamPan");
            cam.transform.DOMove(camOrigPos, 0.20f).SetUpdate(true).SetId("CamPan");

            if (cameraZoomUsePPU && HasAnyPPC() && zoomedByPPU)
            {
                if (ppuTween != null) StopCoroutine(ppuTween);
                ppuTween = StartCoroutine(TweenPPU(GetCurrentPPU(), ppcOrigPPU, 0.20f));
                zoomedByPPU = false;
            }
            else
            {
                DOTween.Kill("CamZoomOrtho");
                DOTween.To(() => cam.orthographicSize, s => cam.orthographicSize = s,
                           camOrigSize, 0.20f)
                       .SetUpdate(true).SetId("CamZoomOrtho");
            }
        }

        if (!feverStarted && orangeRemaining == 1 && lastOrangePeg)
        {
            var peg = lastOrangePeg.GetComponent<PegController>();
            if (peg && peg.State != PegController.PegState.Cleared)
            {
                lastOrangeMode = true;
                lastOrangeReadyTime = Time.time + cinematicDelay;
            }
        }
    }

    IEnumerator TweenPPU(int from, int to, float dur)
    {
        dur = Mathf.Max(0.0001f, dur);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            int cur = Mathf.RoundToInt(Mathf.Lerp(from, to, k));
            SetCurrentPPU(cur);
            yield return null;
        }
        SetCurrentPPU(to);
        ppuTween = null;
    }

    /*════════════ FEVER ════════════*/
    // EXTREME = tidak ada peg non-orange yang tersisa (yang orange boleh sedang clear).
    bool BoardHasNoNonOrangeUnclearedPeg()
    {
        foreach (var peg in FindObjectsOfType<PegController>())
        {
            if (peg.Type != PegType.Orange && peg.State != PegController.PegState.Cleared)
                return false;
        }
        return true;
    }

    void TriggerFever(BallController triggerBall)
    {
        EndLastOrangeCinematic();
        if (feverStarted) return;

        feverStarted = true;
        State = GameState.FeverFinalTurn;

        // Slow-mo masuk fever
        DOTween.Kill("FeverTimeFX");
        DOTween.To(() => Time.timeScale,
                   x => { Time.timeScale = x; Time.fixedDeltaTime = .02f * x; },
                   0.3f, 0.25f)
               .SetId("FeverTimeFX")
               .SetUpdate(true)
               .OnComplete(() =>
               {
                   DOTween.To(() => Time.timeScale,
                              x => { Time.timeScale = x; Time.fixedDeltaTime = .02f * x; },
                              0.7f, 1.0f)
                         .SetId("FeverTimeFX")
                         .SetUpdate(true);
               });

        // === Penentu mode: sesuai definisi proyek
        // Fever biasa: semua orange habis.
        // Extreme: SEMUA peg sudah habis (yang tersisa hanya orange yang sedang fade pun dianggap extreme).
        bool isExtreme = BoardHasNoNonOrangeUnclearedPeg();

        // SFX sesuai mode
        try
        {
            var key = isExtreme ? "ExtremeFever" : "Fever";
            AudioManager.I.Play(key, transform.position);
        }
        catch { }

        // Tentukan bola pemicu
        if (triggerBall == null)
        {
            triggerBall = FindObjectsOfType<BallController>()
                .OrderByDescending(b => b.transform.position.y)
                .FirstOrDefault();
        }

        // (opsional) ganti fireball ke bola normal saat fever
        if (replaceFireballOnFever)
            triggerBall = ReplaceAllFireballsForFever(triggerBall);

        // Hapus bola lain, beri gravity fever pada bola trigger
        foreach (var b in FindObjectsOfType<BallController>())
        {
            if (triggerBall != null && b == triggerBall)
                b.ApplyFeverGravity(feverGravityFactor * 1.5f);
            else
                Destroy(b.gameObject);
        }

        ballsLeftWhenFeverBegan = BallsLeft;
        CharacterPowerManager.Instance?.OnFeverStart();

        // Bucket: 50k semua jika extreme
        bucket.EnterFeverMode(isExtreme);

        triggerBall?.NotifyFeverStarted();

        // Banner: masuk & auto-hide 3 detik
        feverBanner?.Play(isExtreme, 3f);

        if (!string.IsNullOrEmpty(musicFeverKey))
            AudioManager.I?.PlayMusic(musicFeverKey, false);

        Launcher.Instance?.LockInput();
    }

    /*════════════ END TURN ════════════*/
    public void NotifyBallEnded()
    {
        if (replacingFireballGuard) return;

        if (cinematicPlaying && !feverStarted)
            EndLastOrangeCinematic();

        StartCoroutine(EndTurnSequenceWrapper());
    }


    public void ApplyKillZoneBonusNow()
    {
        int amt = _pendingKillZoneBonus;
        if (amt <= 0 && CardEffects.I && CardEffects.I.killZoneBonusScore > 0)
            amt = CardEffects.I.killZoneBonusScore;

        if (amt <= 0) return;

        ScoreManager.AddDirectBonus(amt);   // ✅ langsung ke total, seperti free ball

        _pendingKillZoneBonus = 0;
        _killZoneHappenedThisTurn = true;
    }

    IEnumerator EndTurnSequence()
    {
        foreach (var peg in hitPegs.ToArray())
        {
            if (!peg || peg.State == PegController.PegState.Cleared) continue;
            peg.OnEndTurnCleanup();
            AudioManager.I.Play("Pegpop", peg.transform.position);
            yield return new WaitForSeconds(peg.FadeDuration);
        }

        int clearedThisShot = hitPegs.Count;
        Vector3 popupCenter = cam ? cam.transform.position : Vector3.zero;
        StyleShotManager.EndShot(popupCenter, clearedThisShot);

        if (ScoreManager.ShotPoints > 0 && ScoreUI.Instance)
        {
            IsScoring = true;
            yield return StartCoroutine(ScoreUI.Instance.PlayLastShotPopup());
        }
        if (_pendingKillZoneBonus > 0)
        {
            ScoreManager.AddDirectBonus(_pendingKillZoneBonus);
            _pendingKillZoneBonus = 0;
        }

        ScoreManager.EndShot();
        hitPegs.Clear();

        if (cinematicPlaying && !feverStarted)
            EndLastOrangeCinematic();

        if (State == GameState.FeverFinalTurn && BallController.ActiveBalls == 0)
        {
            IsScoring = false;
            yield return StartCoroutine(ClearRemainingPegsThenFinish());
            yield break;
        }

        if (State == GameState.FeverFinalTurn) { IsScoring = false; yield break; }

        CharacterPowerManager.Instance?.OnTurnEnded();

        // ===== Savior: 3x miss dianggap tertangkap bucket =====
        bool diedByKillZone = (_lastBallEndReason == BallEndReason.KillZone) || _killZoneHappenedThisTurn;
        if (diedByKillZone && CardEffects.I != null && CardEffects.I.TryConsumeSavior())
        {
            if (bucket)
            {
                StyleShotManager.OnBucketCatch(bucket.transform.position);
                AudioManager.I.Play("BucketIn", bucket.transform.position);
            }

            GainBall(1);
            ScoreManager.AddFreeBallBonus();

            _lastBallEndReason = BallEndReason.Bucket;   // tandai “terselamatkan”
            _killZoneHappenedThisTurn = false;           // reset flag
        }


        DespawnExtraBucket();
        bucket.ResetBucket();
        freeBallMeter?.ResetMeter();

        // LOSE CHECK
        if (BallsLeft <= 0 && orangeRemaining > 0)
        {
            bool savedByGambit = (CardEffects.I != null) && CardEffects.I.TryConsumeFinalGambit();
            if (savedByGambit)
            {
                // Beri +3 bola & sedikit feedback
                GainBall(3);
                try { AudioManager.I.Play("ExtraLife", transform.position); } catch { }
                Debug.Log("[GM] Final Gambit triggered → +3 balls");

                // jangan Lose; biarkan lanjut ke Idle
            }
            else
            {
                Debug.Log("[GM] LOSE: ballsLeft=0 & orangeRemaining=" + orangeRemaining);
                TriggerLose();
                yield break;
            }
        }

        IsScoring = false;
        State = GameState.Idle;
        _lastBallEndReason = BallEndReason.None;
    }

    /*════════════ LEVEL END ════════════*/
    void EndLevelSuccess()
    {
        if (State == GameState.EndLevelSuccess) return;
        State = GameState.EndLevelSuccess;

        RestoreTimeScale();

        var stats = new LevelStats
        {
            totalScore = ScoreManager.TotalScore,
            levelScore = ScoreManager.LevelScore,
            winOnFirstTry = firstTry,
            shotsTaken = shotsTaken,
            freeBalls = freeBallsEarned,
            percentCleared = (float)clearedPegCount / totalPegCount,
            shotPoints = ScoreManager.ShotPoints,
            feverPoints = ScoreManager.FeverPoints
        };
        onLevelSuccess?.Invoke(stats);

        // SAVE progres WIN
        try
        {
            int nextLevel = LevelManager.Instance ? LevelManager.Instance.CurrentIndex + 1 : 0;
            int totalScore = ScoreManager.TotalScore;
            int ballsLeftNow = BallsLeft;
            SaveManager.I?.OnLevelCompleted(nextLevel, totalScore, ballsLeftNow);
        }
        catch (System.Exception e) { Debug.LogWarning("Save on success failed: " + e.Message); }

        Time.timeScale = .5f;
    }

    public void NotifyHardOrangeBroken(PegController peg)
    {
        if (!peg || peg.Type != PegType.Orange) return;
        int id = peg.GetInstanceID();
        if (_creditedHardOrange.Contains(id)) return;   // sudah pernah
        _creditedHardOrange.Add(id);
        feverMeter?.AddOrangeHitInstant();              // kredit sekali, langsung
    }

    void DoTinySplit(BallController source)
    {
        if (!source || !normalBallPrefab) return;

        var rb = source.GetComponent<Rigidbody2D>();
        if (!rb) return;

        // Kecepatan dasar—kalau terlalu pelan, pakai initial speed mengarah ke bawah
        Vector2 v = rb.velocity;
        if (v.sqrMagnitude < 0.05f) v = Vector2.down * source.InitialSpeed;

        float speed = v.magnitude;
        float angle = 12f * Mathf.Deg2Rad;   // sudut pecah (feel bebas diganti 10–18 deg)
        float newSpeed = speed * 0.95f;      // sedikit dikurangi supaya stabil

        // Arah kiri/kanan simetris terhadap v
        Vector2 dir = v.normalized;
        Vector2 dirL = new Vector2(
            dir.x * Mathf.Cos(angle) - dir.y * Mathf.Sin(angle),
            dir.x * Mathf.Sin(angle) + dir.y * Mathf.Cos(angle)
        );
        Vector2 dirR = new Vector2(
            dir.x * Mathf.Cos(-angle) - dir.y * Mathf.Sin(-angle),
            dir.x * Mathf.Sin(-angle) + dir.y * Mathf.Cos(-angle)
        );

        // 1) Ubah bola sumber ke salah satu arah
        rb.velocity = dirL * newSpeed;

        // 2) Spawn bola baru ke arah satunya
        var pos = source.transform.position;
        var newBall = Instantiate(normalBallPrefab, pos, Quaternion.identity);
        var newRB = newBall.GetComponent<Rigidbody2D>();
        if (newRB) newRB.velocity = dirR * newSpeed;

        // SFX/Style optional
        try { AudioManager.I.Play("Split", pos); } catch { }
        StyleShotManager.OnTopWallBounce(); // pakai satu hook style agar terasa ada event (opsional)
    }

    // Tambahkan di dalam class GameManager (bagian Utilities private ok)
    bool IsRoundedPeg(PegController p)
    {
        // Rounded peg lazimnya CircleCollider2D
        var col = p ? p.GetComponent<Collider2D>() : null;
        return col is CircleCollider2D;
    }

    void ApplyPowerUpExtraGreens()
    {
        int add = (CardEffects.I != null) ? CardEffects.I.TakeExtraGreenCount() : 0;
        Debug.Log($"[GM] PowerUp: will add {add} green pegs.");
        if (add <= 0) return;

        // 🔒 Kandidat dibatasi: hanya Blue, non-hard, belum cleared, dan ROUNDED (CircleCollider2D)
        var candidates = FindObjectsOfType<PegController>()
            .Where(p => p.Type == PegType.Blue &&
                        !p.IsHard &&
                        p.State != PegController.PegState.Cleared &&
                        IsRoundedPeg(p))
            .ToList();

        Debug.Log($"[GM] PowerUp: blue rounded candidates = {candidates.Count}");

        add = Mathf.Min(add, candidates.Count);
        for (int i = 0; i < add; i++)
        {
            int idx = UnityEngine.Random.Range(0, candidates.Count);
            var peg = candidates[idx];
            candidates.RemoveAt(idx);

            Debug.Log($"[GM] PowerUp: converting {peg.name} to GREEN (rounded-only)");
            peg.ForceSetGreenWithSkin();   // sudah menyalin skin hijau yang ada di scene
            try { AudioManager.I.Play("PowerUpSpawn", peg.transform.position); } catch { }
        }
    }

    void ApplyMinusOrangeThenRefreshCounts()
    {
        int rm = (CardEffects.I != null) ? CardEffects.I.TakeMinusOrangeCount() : 0;
        if (rm <= 0) return;

        // ✅ candidates: only ORANGE pegs that are *rounded* and not cleared
        var oranges = FindObjectsOfType<PegController>()
            .Where(p => p.Type == PegType.Orange
                     && p.State != PegController.PegState.Cleared
                     && IsRoundedPeg(p)                             // CircleCollider2D check
                     && HasFamily(p, PegFamily.Rounded))            // explicit family tag check
            .ToList();

        if (oranges.Count == 0) return;

        rm = Mathf.Min(rm, oranges.Count);
        for (int i = 0; i < rm; i++)
        {
            int idx = UnityEngine.Random.Range(0, oranges.Count);
            var peg = oranges[idx];
            oranges.RemoveAt(idx);

            // Turn into normal blue with the correct blue skin
            peg.ForceSetBlueWithSkin();
            try { AudioManager.I.Play("PegDowngrade", peg.transform.position); } catch { }
        }

        // Recount oranges after conversion
        orangeTotal = FindObjectsOfType<PegController>().Count(p => p.Type == PegType.Orange);
        orangeCleared = 0;
        orangeRemaining = orangeTotal;
    }

    // helper: explicit family guard (next to IsRoundedPeg)
    bool HasFamily(PegController p, PegFamily fam)
    {
        var tag = p ? p.GetComponent<PegFamilyTag>() : null;
        return tag && tag.family == fam;
    }

    void TrySpawnExtraBucketForThisShot()
    {
        extraBucketSpawnedThisShot = false;
        if (feverStarted) return;                    // Saat fever, bucket normal diganti holes
        if (!bucket) return;
        if (CardEffects.I == null) return;
        if (!CardEffects.I.ConsumeDoubleBucketForThisShot()) return;

        // Duplikasi bucket & posisikan mirror di sumbu X biar tak tumpuk
        Vector3 p = bucket.transform.position;
        Vector3 mirrored = new Vector3(-p.x, p.y, p.z);

        extraBucket = Instantiate(bucket, mirrored, bucket.transform.rotation);
        extraBucket.name = bucket.name + " (Extra)";
        extraBucket.ResetBucket();                   // pastikan status internal bersih
        extraBucketSpawnedThisShot = true;
#if UNITY_EDITOR
        Debug.Log("[GM] DoubleBucket → spawn bucket kedua untuk tembakan ini");
#endif
    }

    void DespawnExtraBucket()
    {
        if (extraBucket)
        {
            Destroy(extraBucket.gameObject);
            extraBucket = null;
        }
        extraBucketSpawnedThisShot = false;
    }


    void TriggerLose()
    {
        if (loseCommitted) return;
        loseCommitted = true;

        RestoreTimeScale();

        // SAVE kalah
        if (LevelManager.Instance != null)
            SaveManager.I?.OnLevelFailed(LevelManager.Instance.CurrentIndex);

        // UI lives
        LivesUI.Instance?.PlayLoseLife(SaveManager.I != null ? SaveManager.I.Data.lives : 0);

        var stats = new LevelStats
        {
            totalScore = ScoreManager.TotalScore,
            levelScore = ScoreManager.LevelScore,
            winOnFirstTry = false,
            shotsTaken = shotsTaken,
            freeBalls = freeBallsEarned,
            percentCleared = totalPegCount == 0 ? 0f : (float)clearedPegCount / totalPegCount,
            shotPoints = ScoreManager.ShotPoints,
            feverPoints = ScoreManager.FeverPoints
        };

        if (endPanel != null) endPanel.ShowLose(stats);
        else Debug.LogWarning("[GM] EndLevelPanel belum di-assign.");
        Launcher.Instance?.LockInput();
    }

    /*════════════ RAINBOW PEG HELPER ════════════*/
    void SelectNewRainbowPeg()
    {
        // 🔒 Gate Rainbow sampai Level 2-3 (index >= 7)
        int idx = LevelManager.Instance ? LevelManager.Instance.CurrentIndex : 0;
        if (idx < 7) return;

        currentRainbowPeg?.RevertToBlue();
        currentRainbowPeg = null;

        var bluePegs = FindObjectsOfType<PegController>()
                       .Where(p => p.Type == PegType.Blue &&
                                   !p.IsHard &&
                                   p.State == PegController.PegState.Idle)
                       .ToList();

        if (bluePegs.Count == 0) return;

        currentRainbowPeg = bluePegs[UnityEngine.Random.Range(0, bluePegs.Count)];
        currentRainbowPeg.SetAsRainbow();
    }


    /*════════════ UTILITIES ════════════*/
    public bool CanShoot() =>
        State == GameState.Idle && HasBalls && !IsFlipping && !IsScoring;

    public void QueueKillZoneBonus(int amount)
    {
        if (amount <= 0) return;
        _pendingKillZoneBonus += amount;
#if UNITY_EDITOR
        Debug.Log($"[GM] QueueKillZoneBonus += {amount} → pending={_pendingKillZoneBonus}");
#endif
    }
    public void RestoreTimeScale()
    { Time.timeScale = 1f; Time.fixedDeltaTime = .02f; }

    public void FlipCoinFeedback()
    {
        if (!feedbackImage) return;

        NormalizeCoinRect();

        float p = 0.5f + (CardEffects.I ? CardEffects.I.coinFreeBonus : 0f);
        p = Mathf.Clamp01(p);
        bool isFree = UnityEngine.Random.value < p;
        IsFlipping = true;

        if (isFree && coinFreeSprite) feedbackImage.sprite = coinFreeSprite;
        else if (!isFree && coinNoSprite) feedbackImage.sprite = coinNoSprite;

        feedbackImage.gameObject.SetActive(true);

        if (isFree)
        {
            GainBall(1);
            ScoreManager.AddFreeBallBonus();
            PlayCoinSfx(sfxFreeKey);
        }
        else
        {
            PlayCoinSfx(sfxNoKey);

            bool lastBallUsed = (BallsLeft <= 0);
            bool stillHasOrange = (orangeRemaining > 0);
            if (lastBallUsed && stillHasOrange)
            {
                float totalFeedbackDur = Mathf.Max(0.0001f, fadeInDuration)
                                         + Mathf.Max(0f, holdDuration)
                                         + Mathf.Max(0.0001f, fadeOutDuration)
                                         + 0.05f;
                RequestEndTurnDelay(totalFeedbackDur);
            }
        }

        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        feedbackRoutine = StartCoroutine(ShowFeedbackRoutine());
    }

    void HideFeedback()
    {
        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
        }
        if (feedbackImage) feedbackImage.gameObject.SetActive(false);
        IsFlipping = false;
    }
    public void ShowProgressSavedToast()
    {
        ShowSavedToastNow();   // pakai toast yang sudah ada
    }
    IEnumerator ShowFeedbackRoutine()
    {
        var rt = feedbackImage ? feedbackImage.rectTransform : null;
        if (feedbackImage == null || rt == null)
        {
            HideFeedback();
            yield break;
        }

        Color c = feedbackImage.color; c.a = 0f; feedbackImage.color = c;

        Vector3 startScale = Vector3.one * appearScale;
        Vector3 endScale = Vector3.one;
        rt.localScale = startScale;

        IsFlipping = true;

        float t = 0f;
        float fin = Mathf.Max(0.0001f, fadeInDuration);
        while (t < fin)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fin);
            rt.localScale = Vector3.Lerp(startScale, endScale, k);
            c.a = k;
            feedbackImage.color = c;
            yield return null;
        }

        float h = Mathf.Max(0f, holdDuration);
        while (h > 0f)
        {
            h -= Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;
        float fout = Mathf.Max(0.0001f, fadeOutDuration);
        Vector3 outStart = endScale;
        Vector3 outEnd = endScale * 1.02f;
        float a0 = 1f;
        while (t < fout)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fout);
            rt.localScale = Vector3.Lerp(outStart, outEnd, k);
            c.a = Mathf.Lerp(a0, 0f, k);
            feedbackImage.color = c;
            yield return null;
        }

        feedbackImage.gameObject.SetActive(false);
        IsFlipping = false;
        feedbackRoutine = null;
    }
    Rect GetWorldBoundsRect()
    {
        if (cameraBoundsCollider)
        {
            var b = cameraBoundsCollider.bounds;
            return new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
        }
        return fallbackBounds;
    }
    // Cek apakah jalur dari bola ke last-orange TIDAK terhalang peg lain
    bool HasLineOfSightToLastOrange(Vector2 from, float ballRadius)
    {
        if (!lastOrangePeg) return false;

        Vector2 to = (Vector2)lastOrangePeg.position - from;
        float dist = to.magnitude;

        // Sedikit kurangi jarak agar tidak "nabrak" diri sendiri di start
        float castDist = Mathf.Max(0f, dist - ballRadius * 0.5f);
        if (castDist <= 0.0001f) return true; // practically on top of it

        // Pakai CircleCast supaya konsisten dengan ukuran bola
        var hit = Physics2D.CircleCast(
            from,
            Mathf.Max(0.01f, ballRadius * 0.95f),
            to.normalized,
            castDist,
            pegLayerMask
        );

        // True hanya bila tidak ada halangan ATAU yang pertama terkena memang last-orange
        return !hit || hit.transform == lastOrangePeg;
    }

    Vector3 ClampToBounds(Vector3 desired, float halfW, float halfH)
    {
        var r = GetWorldBoundsRect();

        // padding aman
        r.xMin += boundsPadding; r.yMin += boundsPadding;
        r.xMax -= boundsPadding; r.yMax -= boundsPadding;

        // kalau rect lebih kecil dari viewport (jarang, tapi amanin)
        if (r.width < halfW * 2f || r.height < halfH * 2f)
            return new Vector3(r.center.x, r.center.y, desired.z);

        float cx = Mathf.Clamp(desired.x, r.xMin + halfW, r.xMax - halfW);
        float cy = Mathf.Clamp(desired.y, r.yMin + halfH, r.yMax - halfH);
        return new Vector3(cx, cy, desired.z);
    }

    // half-height viewport SETELAH zoom (PPU or Ortho)
    float GetZoomedHalfHeight()
    {
        // Bila zoom via PPU (PixelPerfect)
        if (cameraZoomUsePPU && HasAnyPPC())
        {
            int from = (ppcOrigPPU > 0) ? ppcOrigPPU : Mathf.Max(1, GetCurrentPPU());
            int to = Mathf.Max(1, zoomPPU);
            float scale = (float)to / Mathf.Max(1, from); // >1 artinya zoom-in
            return camOrigSize / Mathf.Max(0.0001f, scale);
        }
        // Fallback orthographic zoom
        return zoomOrthoSizeFallback;
    }

    void NormalizeCoinRect()
    {
        if (!feedbackImage) return;
        var rt = feedbackImage.rectTransform;

        if (enforceCenterAnchor)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(.5f, .5f);
        }

        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, coinResultSize.x);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, coinResultSize.y);
        rt.anchoredPosition = coinResultAnchoredPos;

        var lp = rt.localPosition;
        rt.localPosition = new Vector3(lp.x, lp.y, 0f);
        rt.localScale = Vector3.one;
    }

    void PlayCoinSfx(string key)
    {
        if (string.IsNullOrEmpty(key) || AudioManager.I == null) return;

        if (sfxUseUI) AudioManager.I.PlayUI(key);
        else
        {
            Vector3 pos = sfxAnchor ? sfxAnchor.position : transform.position;
            AudioManager.I.Play(key, pos);
        }
    }

    bool BoardHasNoUnclearedPeg()
    {
        foreach (var peg in FindObjectsOfType<PegController>())
            if (peg.State != PegController.PegState.Cleared) return false;
        return true;
    }

    IEnumerator ConsumeLeftoverBalls()
    {
        var ui = BallOTronUI.Instance;
        for (int i = 0; i < ballsLeftWhenFeverBegan; i++)
        { ui.ConsumeOneBall(); yield return new WaitForSeconds(.10f); }

        BallsLeft = 0; OnBallsLeftChanged?.Invoke(0);
    }

    IEnumerator ClearRemainingPegsThenFinish()
    {
        var remain = FindObjectsOfType<PegController>()
                     .Where(p => p.State != PegController.PegState.Cleared)
                     .OrderBy(p => p.transform.position.y);

        foreach (var peg in remain)
        {
            peg.ClearNow();
            AudioManager.I.Play("Pegpop", peg.transform.position);
            yield return new WaitForSeconds(.05f);
        }

        ScoreManager.AddFeverBallBonus(ballsLeftWhenFeverBegan, feverBallBonus);
        yield return StartCoroutine(ConsumeLeftoverBalls());

        feverScorePopup.Show(ScoreManager.FeverPoints,
                             ballsLeftWhenFeverBegan, feverBallBonus);
        AudioManager.I.Play("FeverFanfare", transform.position);

        yield return new WaitForSeconds(feverHold);
        EndLevelSuccess();
    }

    public void RequestEndTurnDelay(float seconds)
    {
        float until = Time.unscaledTime + Mathf.Max(0f, seconds);
        endTurnDelayUntilRT = Mathf.Max(endTurnDelayUntilRT, until);
    }

    IEnumerator EndTurnSequenceWrapper()
    {
        if (endTurnDelayUntilRT > 0f)
        {
            while (Time.unscaledTime < endTurnDelayUntilRT)
                yield return null;
            endTurnDelayUntilRT = 0f;
        }
        yield return StartCoroutine(EndTurnSequence());
    }

    public void Style_ElementalFinale(Vector3 where) =>
        StyleShotManager.TriggerElementalFinale(where);

    /*──────────── Debug Zoom Pulse ────────────*/
    IEnumerator DebugZoomPulse()
    {
        ResolveCameraAndPPC();

        Debug.Log("[GM] DEBUG ZOOM start (press Z)");
        float dur = 0.25f;

        Vector3 pos0 = cam.transform.position;
        float ortho0 = cam.orthographicSize;
        int ppu0 = GetCurrentPPU();

        Vector3 pos1 = pos0;
        float ortho1 = zoomOrthoSizeFallback;
        int ppu1 = Mathf.Max(1, zoomPPU);

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / dur);

            cam.transform.position = Vector3.Lerp(pos0, pos1, k);

            if (cameraZoomUsePPU && HasAnyPPC())
            {
                int v = Mathf.RoundToInt(Mathf.Lerp(ppu0, ppu1, k));
                SetCurrentPPU(v);
            }
            else
            {
                cam.orthographicSize = Mathf.Lerp(ortho0, ortho1, k);
            }
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.25f);

        t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / dur);

            cam.transform.position = Vector3.Lerp(pos1, pos0, k);

            if (cameraZoomUsePPU && HasAnyPPC())
            {
                int v = Mathf.RoundToInt(Mathf.Lerp(ppu1, ppu0, k));
                SetCurrentPPU(v);
            }
            else
            {
                cam.orthographicSize = Mathf.Lerp(ortho1, ortho0, k);
            }
            yield return null;
        }
        Debug.Log("[GM] DEBUG ZOOM end");
    }

    /*════════════ INTERNAL: Replace Fireball Helpers ════════════*/

    bool replacingFireballGuard = false;

    IEnumerator ClearReplacingGuardNextFrame()
    {
        yield return null; // tunggu 1 frame supaya OnDestroy fireball selesai
        replacingFireballGuard = false;
    }

    BallController ReplaceAllFireballsForFever(BallController currentTrigger)
    {
        var fireballs = FindObjectsOfType<AposdaFireball>();
        if (fireballs == null || fireballs.Length == 0) return currentTrigger;

        if (!normalBallPrefab)
        {
            Debug.LogWarning("[GM] normalBallPrefab belum diassign di Inspector!");
            return currentTrigger;
        }

        Dictionary<BallController, BallController> remap = new();
        replacingFireballGuard = true;

        foreach (var f in fireballs)
        {
            if (!f) continue;

            var rb = f.GetComponent<Rigidbody2D>();
            var bc = f.GetComponent<BallController>();

            Vector3 pos = f.transform.position;
            Vector2 vel = rb ? rb.velocity : Vector2.zero;

            var newBall = Instantiate(normalBallPrefab, pos, Quaternion.identity);
            var newRB = newBall.GetComponent<Rigidbody2D>();
            if (newRB) newRB.velocity = vel;

            if (smoothReplaceFireball)
            {
                FadeAllSprites(newBall.gameObject, 0f);
                FadeAllSprites(newBall.gameObject, 1f, replaceFadeDuration);
                FadeAllSprites(f.gameObject, 0f, replaceFadeDuration);
                Destroy(f.gameObject, replaceFadeDuration);
            }
            else
            {
                Destroy(f.gameObject);
            }

            if (bc) remap[bc] = newBall;
        }

        StartCoroutine(ClearReplacingGuardNextFrame());

        if (currentTrigger && remap.TryGetValue(currentTrigger, out var newTrigger))
            return newTrigger;

        var chosen = FindObjectsOfType<BallController>()
                     .OrderByDescending(b => b.transform.position.y)
                     .FirstOrDefault();
        return chosen ? chosen : currentTrigger;
    }

    void FadeAllSprites(GameObject go, float targetAlpha, float dur = 0f)
    {
        var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in srs)
        {
            if (!sr) continue;
            var c = sr.color;
            if (dur <= 0f)
            { c.a = targetAlpha; sr.color = c; }
            else
            {
                DOTween.Kill(sr.GetInstanceID());
                sr.DOFade(targetAlpha, dur).SetId(sr.GetInstanceID()).SetUpdate(true);
            }
        }
    }
}
