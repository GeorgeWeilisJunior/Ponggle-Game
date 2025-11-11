using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

[DisallowMultipleComponent]
public class EndLevelPanel : MonoBehaviour
{
    public enum EndState { Win, Lose }
    enum Stage { Summary, AfterCardDrop }
    Stage stage = Stage.Summary;
    EndState endState = EndState.Win;

    bool subscribed;
    bool hasPendingStats;
    LevelStats pendingStats;

    [Header("Canvas / Visual Root (drag: PanelRoot)")]
    [SerializeField] GameObject canvasGO;

    [Header("Header")]
    [SerializeField] TMP_Text titleText;      // optional (pakai kalau judulnya TMP)
    [SerializeField] Image headerBG;
    [SerializeField] Color winColor = new Color(0.19f, 0.45f, 0.98f);
    [SerializeField] Color loseColor = new Color(0.75f, 0.22f, 0.30f);

    [Header("Header (Title Images)")]
    [SerializeField] GameObject titleWinGO;
    [SerializeField] GameObject titleLoseGO;

    [Header("Stat Texts")]
    [SerializeField] TMP_Text totalTxt;
    [SerializeField] TMP_Text levelTxt;
    [SerializeField] TMP_Text winTxt;
    [SerializeField] TMP_Text shotsTxt;
    [SerializeField] TMP_Text freeTxt;
    [SerializeField] TMP_Text pctTxt;
    [SerializeField] TMP_Text shotPtsTxt;
    [SerializeField] TMP_Text feverPtsTxt;

    [Header("Buttons")]
    [SerializeField] Button nextBtn;    // Open Reward / Next Level (non-final)
    [SerializeField] Button retryBtn;
    [SerializeField] Button backBtn;    // Main Menu (final only)

    [Header("Next Button (Label Images)")]
    [SerializeField] GameObject nextLabelOpenRewardGO;
    [SerializeField] GameObject nextLabelNextLevelGO;

    [Header("Card Drop Hook")]
    [SerializeField] CardDropPanel cardDropPanel;

    [Header("Visuals")]
    [SerializeField] Image darkenBG;
    [SerializeField] float popDuration = 0.4f;
    [SerializeField] int sortingOrder = 50;

    bool _openingDrop;

    // === NEW: guard supaya tidak double submit ===
    bool _submittedFinalWin = false;

    void Awake()
    {
        if (canvasGO) canvasGO.SetActive(false);
        var cv = GetComponent<Canvas>();
        if (cv) cv.sortingOrder = sortingOrder;

        // pastikan panel bisa menerima raycast
        if (!GetComponent<GraphicRaycaster>()) gameObject.AddComponent<GraphicRaycaster>();

        // ⬇️ tambahkan baris ini
        EnsureButtonRaycast(nextBtn);
        EnsureButtonRaycast(retryBtn);
        EnsureButtonRaycast(backBtn);

        if (backBtn) backBtn.gameObject.SetActive(false);
    }

    void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onLevelSuccess += Show;
            subscribed = true;
        }
        else
        {
            Debug.LogWarning("[EndLevelPanel] GameManager.Instance null di Start().");
        }

        if (nextBtn) { nextBtn.onClick.RemoveAllListeners(); nextBtn.onClick.AddListener(OnClickNext); }
        if (retryBtn) { retryBtn.onClick.RemoveAllListeners(); retryBtn.onClick.AddListener(OnClickRetry); }
        if (backBtn)
        {
            backBtn.onClick.RemoveAllListeners();
            backBtn.onClick.AddListener(() => {
                if (backBtn) backBtn.interactable = false;  // <— guard anti double-click
                StartCoroutine(BackToMenuFlow());
            });
        }
    }

    void OnDestroy()
    {
        if (subscribed && GameManager.Instance != null)
            GameManager.Instance.onLevelSuccess -= Show;
    }

    // ===== Entry: WIN =====
    void Show(LevelStats s)
    {
        hasPendingStats = true;
        pendingStats = s;
        stage = Stage.Summary;
        endState = EndState.Win;

        DoShow(pendingStats);
        SetHeaderVisuals();

        bool finalLevel = IsFinalLevel();   // deteksi gabungan (LevelManager lalu SaveManager)

        if (nextBtn) nextBtn.gameObject.SetActive(!finalLevel); // non-final → Open Card
        if (retryBtn) retryBtn.gameObject.SetActive(true);
        if (backBtn) backBtn.gameObject.SetActive(finalLevel);  // final only

        if (!finalLevel)
            SetNextButtonLabel(isOpenReward: true);              // label “Open Card”
        if (LevelManager.Instance) LevelManager.Instance.SetFlowLocked(true);
    }

    // ===== Entry: LOSE =====
    public void ShowLose(LevelStats s)
    {
        hasPendingStats = true;
        pendingStats = s;
        stage = Stage.Summary;
        endState = EndState.Lose;

        DoShow(pendingStats);
        SetHeaderVisuals();

        if (nextBtn) nextBtn.gameObject.SetActive(false);
        if (retryBtn) retryBtn.gameObject.SetActive(true);
        if (backBtn) backBtn.gameObject.SetActive(false);      // jangan tampilkan saat kalah
    }

    // ===== Setelah CardDrop selesai (WIN) =====
    public void ShowPending()
    {
        if (!hasPendingStats) return;
        stage = Stage.AfterCardDrop;
        DoShow(pendingStats);
        endState = EndState.Win;
        SetHeaderVisuals();

        bool finalLevel = IsFinalLevel();
        if (nextBtn) nextBtn.gameObject.SetActive(!finalLevel);
        if (retryBtn) retryBtn.gameObject.SetActive(true);
        if (backBtn) backBtn.gameObject.SetActive(finalLevel);
        if (!finalLevel) SetNextButtonLabel(isOpenReward: false);

        GameManager.Instance?.ShowProgressSavedToast();
        // Bersihkan flag cheat agar run berikutnya normal
        if (PlayerPrefs.HasKey("DEV_FORCE_CARD_DROP"))
        {
            PlayerPrefs.DeleteKey("DEV_FORCE_CARD_DROP");
            PlayerPrefs.Save();
        }
    }

    // ===== Buttons =====
    void OnClickNext()
    {
        if (endState == EndState.Lose) return;
        if (IsFinalLevel()) return;

        if (stage == Stage.Summary)
        {
            if (_openingDrop) return;
            _openingDrop = true;

            if (canvasGO) canvasGO.SetActive(false);
            Resume();

            bool isDevForce = PlayerPrefs.GetInt("DEV_FORCE_CARD_DROP", 0) == 1;
            StartCoroutine(ShowCardDropNextFrame(isDevForce));
            return;
        }

        // === stage == AfterCardDrop ===
        Resume();
        // Lepas rem → izinkan pindah scene ke Card Management/Next
        if (LevelManager.Instance) LevelManager.Instance.SetFlowLocked(false);
        LevelManager.Instance.GoToCardManagementThenNext();
    }

    void OnClickRetry()
    {
        Resume(); // Time.timeScale = 1
        if (canvasGO) canvasGO.SetActive(false);
        AudioManager.I?.StopOverlayMusic();

        var lm = LevelManager.Instance;
        if (lm) lm.SetFlowLocked(false);

        // === WIN: replay level yang barusan dimenangkan ===
        if (endState == EndState.Win)
        {
            // sinkronkan save agar tidak maju ke next
            if (SaveManager.I != null && lm != null)
            {
                SaveManager.I.Data.levelIndex = lm.CurrentIndex;
                SaveManager.I.Data.stageIndex = lm.CurrentIndex / Mathf.Max(1, lm.levelsPerStage);
                SaveManager.I.SaveToDisk();
            }
            lm?.Restart();
            return;
        }

        // === LOSE: tetap hormati punishment (awal stage jika nyawa habis) ===
        int? targetFromSave = null;
        if (SaveManager.I != null && lm != null && lm.levelScenes != null && lm.levelScenes.Length > 0)
        {
            int idxSave = Mathf.Clamp(SaveManager.I.Data.levelIndex, 0, lm.levelScenes.Length - 1);
            if (idxSave != lm.CurrentIndex)
                targetFromSave = idxSave;   // contoh: sudah di-reset ke awal stage
        }

        if (targetFromSave.HasValue)
            lm.LoadLevelIndex(targetFromSave.Value);  // → awal stage (punishment)
        else
            lm.Restart();                              // → retry level yang sama
    }



    IEnumerator BackToMenuFlow()
    {
        // === NEW: submit leaderboard kalau ini final & menang ===
        SubmitFinalWinToLeaderboardIfNeeded();

        Resume();
        if (canvasGO) canvasGO.SetActive(false);
        yield return null;

        if (LevelManager.Instance) LevelManager.Instance.SetFlowLocked(false);
        LevelManager.Instance?.BackToMenu();
    }
    void EnsureButtonRaycast(Button b)
    {
        if (!b) return;

        // kalau Button tidak punya targetGraphic, tambahkan Image transparan tipis
        if (b.targetGraphic == null)
        {
            var img = b.GetComponent<Image>();
            if (!img) img = b.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.001f);  // hampir tak terlihat
            img.raycastTarget = true;
            b.targetGraphic = img;
        }
    }
    // ===== NEW: helper untuk submit leaderboard saat final win =====
    void SubmitFinalWinToLeaderboardIfNeeded()
    {
        if (_submittedFinalWin) return;
        if (endState != EndState.Win) return;

        bool isFinal = IsFinalLevel();
        if (!isFinal) return;

        if (SaveManager.I != null)
        {
            // FIX: LevelStats adalah struct → pakai flag hasPendingStats, bukan cek null
            int totalScore = hasPendingStats ? pendingStats.totalScore : ScoreManager.TotalScore;
            int ballsLeft = GameManager.Instance ? GameManager.Instance.BallsLeft : 0;

            // Pakai nextLevelIndex = TOTAL_LEVELS agar pasti dianggap tamat
            SaveManager.I.OnLevelCompleted(SaveManager.TOTAL_LEVELS, totalScore, ballsLeft);
#if UNITY_EDITOR
            Debug.Log($"[EndLevelPanel] Submitted final win → total={totalScore} ballsLeft={ballsLeft}");
#endif
            _submittedFinalWin = true;
        }
        else
        {
            Debug.LogWarning("[EndLevelPanel] SaveManager.I null saat submit final win.");
        }
    }


    // ===== Render / Visual =====
    void DoShow(LevelStats s)
    {
        if (totalTxt) totalTxt.text = s.totalScore.ToString("N0");
        if (levelTxt) levelTxt.text = s.levelScore.ToString("N0");
        if (winTxt) winTxt.text = s.winOnFirstTry ? "YES" : "NO";
        if (shotsTxt) shotsTxt.text = s.shotsTaken.ToString();
        if (freeTxt) freeTxt.text = s.freeBalls.ToString();
        if (pctTxt) pctTxt.text = (s.percentCleared * 100f).ToString("0") + "%";
        if (shotPtsTxt) shotPtsTxt.text = s.shotPoints.ToString("N0");
        if (feverPtsTxt) feverPtsTxt.text = s.feverPoints.ToString("N0");

        if (canvasGO) canvasGO.SetActive(true);

        transform.DOKill();
        if (darkenBG) darkenBG.DOKill();

        transform.localScale = Vector3.zero;
        if (darkenBG)
        {
            var c = darkenBG.color; c.a = 0f; darkenBG.color = c;
        }

        transform.DOScale(1f, popDuration).SetEase(Ease.OutBack).SetUpdate(true);
        if (darkenBG) darkenBG.DOFade(1f, popDuration).SetUpdate(true);

        if (AudioManager.I)
            AudioManager.I.Play(endState == EndState.Win ? "TaDa" : "Lose", Camera.main.transform.position);

        Time.timeScale = 0f;
    }

    void SetHeaderVisuals()
    {
        if (titleText) titleText.text = (endState == EndState.Win) ? "LEVEL COMPLETE!" : "YOU LOSE";
        if (headerBG) headerBG.color = (endState == EndState.Win) ? winColor : loseColor;
        if (titleWinGO) titleWinGO.SetActive(endState == EndState.Win);
        if (titleLoseGO) titleLoseGO.SetActive(endState == EndState.Lose);
    }

    /// <summary> Cari CardDropPanel meski canvanya inaktif. Sekaligus cache ke field. </summary>
    CardDropPanel EnsureCardDropPanel()
    {
        if (cardDropPanel) return cardDropPanel;

        var all = Resources.FindObjectsOfTypeAll<CardDropPanel>();
        if (all != null && all.Length > 0)
        {
            cardDropPanel = all[0];
            return cardDropPanel;
        }

        var roots = gameObject.scene.GetRootGameObjects();
        foreach (var r in roots)
        {
            var t = r.transform.Find("CardDropCanvas");
            if (t)
            {
                cardDropPanel = t.GetComponentInChildren<CardDropPanel>(true);
                if (cardDropPanel) return cardDropPanel;
            }
        }

        Debug.LogWarning("[EndLevelPanel] CardDropPanel tidak ditemukan di scene.");
        return null;
    }

    void ActivateRoot(GameObject go)
    {
        var root = go.transform;
        while (root.parent) root = root.parent;
        if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
    }

    void SetNextButtonLabel(bool isOpenReward)
    {
        if (nextLabelOpenRewardGO) nextLabelOpenRewardGO.SetActive(isOpenReward);
        if (nextLabelNextLevelGO) nextLabelNextLevelGO.SetActive(!isOpenReward);
    }

    // ===== Helpers =====
    bool IsFinalLevel()
    {
        // 1) Kalau LevelManager ada & terkonfigurasi, ini sumber kebenaran
        var lm = LevelManager.Instance;
        if (lm != null && lm.levelScenes != null && lm.levelScenes.Length > 0)
        {
            return lm.CurrentIndex >= lm.levelScenes.Length - 1;
        }

        // 2) Fallback: hanya jika LM tidak ada / tidak terkonfigurasi
        if (SaveManager.I != null && SaveManager.I.Data != null)
        {
            int idx = Mathf.Clamp(SaveManager.I.Data.levelIndex, 0, SaveManager.TOTAL_LEVELS - 1);
            return idx >= (SaveManager.TOTAL_LEVELS - 1);
        }

        return false;
    }


    IEnumerator ShowCardDropNextFrame(bool isDevForce = false)
    {
        yield return null;
        var blocker = CreateTempRaycastBlocker(0.20f);

        var panel = EnsureCardDropPanel();
        if (panel)
        {
            ActivateRoot(panel.gameObject);
            panel.BeginFromWin();

            // Cheat: biarkan berhenti di CardDrop (tanpa auto lanjut)
            if (isDevForce) yield break;
        }
        else
        {
            Debug.LogWarning("[EndLevelPanel] CardDropPanel tidak ditemukan. Fallback ke CardManagement.");
            if (LevelManager.Instance) LevelManager.Instance.SetFlowLocked(false);
            LevelManager.Instance.GoToCardManagementThenNext();
        }

        yield return new WaitForSecondsRealtime(0.20f);
        if (blocker) Destroy(blocker);
        _openingDrop = false;
    }

    GameObject CreateTempRaycastBlocker(float seconds)
    {
        var go = new GameObject("__DropClickBlocker");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60000;
        go.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var imgGO = new GameObject("Blocker");
        imgGO.transform.SetParent(go.transform, false);
        var img = imgGO.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0, 0, 0, 0.001f);
        img.raycastTarget = true;

        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        Destroy(go, seconds + 1f);
        return go;
    }

    void Resume() => Time.timeScale = 1f;
}
