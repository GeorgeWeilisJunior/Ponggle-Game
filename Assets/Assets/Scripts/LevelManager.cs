using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Level manager terpusat:
/// - Menyimpan urutan scene level
/// - Menyediakan API Next / Restart / Load by Index / Back to Menu
/// - Utility Stage (tiap N level = 1 stage)
/// - Opsional: mengatur FastForward (FF) lock/unlock berbasis stage
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Scenes")]
    [Tooltip("Urutan scene level (tanpa Main Menu). Pastikan semua scene sudah di Build Settings.")]
    public string[] levelScenes;

    [Tooltip("Nama scene untuk Main Menu.")]
    public string mainMenuScene = "Main Menu";

    [Header("Flow Scenes (Optional)")]
    [Tooltip("Nama scene untuk Card Management. Biarkan kosong kalau mau skip.")]
    public string cardManagementScene = "CardManagement";

    [Header("Stage Settings")]
    [Tooltip("Berapa level per stage. 5 artinya Level 1–5 = Stage 1, 6–10 = Stage 2, dst.")]
    public int levelsPerStage = 5;

    [Tooltip("Mulai stage ke berapa FF di-unlock otomatis (contoh: 2 => mulai Level 6).")]
    public int unlockFromStage = 2;

    [Header("Stage BGM (per 5 level)")]
    [Tooltip("Nama key musik untuk setiap stage. Index 0=Stage1, 1=Stage2, dst.")]
    public string[] stageBgmKeys = new string[5]
    {
        "Music_Gameplay_Stage1",
        "Music_Gameplay_Stage2",
        "Music_Gameplay_Stage3",
        "Music_Gameplay_Stage4",
        "Music_Gameplay_Stage5"
    };

    // Index level saat ini (0-based): 0=Level1, 1=Level2, dst
    int currentIndex = 0;
    public int CurrentIndex => currentIndex;
    bool _flowLocked;

    /* ═══════════ LIFECYCLE ═══════════ */
    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Sinkron index saat Play langsung dari scene level di Editor
        string active = SceneManager.GetActiveScene().name;
        int idx = Array.IndexOf(levelScenes, active);
        if (idx >= 0) currentIndex = idx;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /* ═══════════ PUBLIC API ═══════════ */

    /// <summary>Muat level berdasarkan index (0-based). Aman untuk dipanggil dari Main Menu (New/Load).</summary>
    public void LoadLevelIndex(int index)
    {
        if (!HasLevelsConfigured())
            return;

        currentIndex = Mathf.Clamp(index, 0, levelScenes.Length - 1);
        if (SaveManager.I != null)
        {
            SaveManager.I.Data.levelIndex = currentIndex;
            SaveManager.I.Data.stageIndex = currentIndex / Mathf.Max(1, levelsPerStage);
            SaveManager.I.SaveToDisk();
        }
        Time.timeScale = 1f;
        SceneTransition.LoadScene(levelScenes[currentIndex]);
    }

    public void SetFlowLocked(bool on)
    {
        _flowLocked = on;
        Debug.Log("[LevelManager] FlowLocked = " + on);
    }


    /// <summary>Muat scene berikutnya; jika habis, kembali ke Main Menu.</summary>
    public void LoadNext()
    {
        if (!HasLevelsConfigured()) return;
        if (_flowLocked) { Debug.LogWarning("[LevelManager] LoadNext diblok oleh FlowLock"); return; }

        currentIndex++;
        if (SaveManager.I != null)
        {
            SaveManager.I.Data.levelIndex = currentIndex;
            SaveManager.I.Data.stageIndex = currentIndex / Mathf.Max(1, levelsPerStage);
            SaveManager.I.SaveToDisk();
        }
        if (currentIndex >= levelScenes.Length) { BackToMenu(); return; }
        Time.timeScale = 1f;
        SceneTransition.LoadScene(levelScenes[currentIndex]);

    }

    /// <summary>Reload level aktif.</summary>
    public void Restart()
    {
        if (!HasLevelsConfigured())
            return;

        Time.timeScale = 1f;

        // Opsional informasi ke GameManager (jika ada method ini)
        if (GameManager.Instance != null)
            GameManager.Instance.SendMessage("SetFirstTryFalse", SendMessageOptions.DontRequireReceiver);

        string active = SceneManager.GetActiveScene().name;
        // pastikan currentIndex sinkron
        int idx = Array.IndexOf(levelScenes, active);
        if (idx >= 0) currentIndex = idx;
        if (SaveManager.I != null)
        {
            SaveManager.I.Data.levelIndex = currentIndex;
            SaveManager.I.Data.stageIndex = currentIndex / Mathf.Max(1, levelsPerStage);
            SaveManager.I.SaveToDisk();
        }

        SceneTransition.LoadScene(active);
    }

    /// <summary>Kembali ke Main Menu.</summary>
    public void BackToMenu()
    {
        Time.timeScale = 1f;

        if (string.IsNullOrEmpty(mainMenuScene))
        {
            Debug.LogWarning("[LevelManager] mainMenuScene kosong. Default ke 'MainMenu'.");
            mainMenuScene = "MainMenu";
        }

        SceneTransition.LoadScene(mainMenuScene);
    }

    /// <summary>
    /// Setelah menang & selesai Card Drop, panggil ini agar ke Card Management dulu.
    /// Kalau nama scenenya kosong, fallback ke LoadNext().
    /// </summary>
    public void GoToCardManagementThenNext()
    {
        if (_flowLocked) { Debug.LogWarning("[LevelManager] GoToCardManagement diblok oleh FlowLock"); return; }

        if (!string.IsNullOrEmpty(cardManagementScene))
        {
            Time.timeScale = 1f;
            SceneTransition.LoadScene(cardManagementScene);
        }
        else LoadNext();
    }

    /// <summary>Stage 1-based dari index level saat ini (1 = Stage 1).</summary>
    public int GetCurrentStage() => GetStageFromLevelIndex(currentIndex);

    /// <summary>Stage 1-based dari index level 0-based tertentu.</summary>
    public int GetStageFromLevelIndex(int levelIndex0Based)
    {
        int lps = Mathf.Max(1, levelsPerStage);
        return (levelIndex0Based / lps) + 1; // 0..4 -> 1, 5..9 -> 2, dst
    }

    /// <summary>Index level awal untuk sebuah stage (1-based StageNumber).</summary>
    public int GetStageStartIndex(int stage1Based)
    {
        int lps = Mathf.Max(1, levelsPerStage);
        int s = Mathf.Max(1, stage1Based) - 1;
        return Mathf.Clamp(s * lps, 0, Mathf.Max(0, (levelScenes?.Length ?? 1) - 1));
    }

    /// <summary>Load level pertama dari stage tertentu (1-based).</summary>
    public void LoadStageStart(int stage1Based)
    {
        int start = GetStageStartIndex(stage1Based);
        LoadLevelIndex(start);
    }

    /* ═══════════ INTERNAL ═══════════ */

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        int idx = Array.IndexOf(levelScenes, scene.name);
        if (idx >= 0) currentIndex = idx;

        var ff = FastForwardController.Instance;
        if (ff != null)
        {
            int currentStage = GetStageFromLevelIndex(currentIndex);
            int minStageToUnlock = Mathf.Max(1, unlockFromStage);

            bool unlockedByStage = currentStage >= minStageToUnlock;
            bool unlockedBySave = (SaveManager.I != null && SaveManager.I.Data.fastForwardUnlocked);
            bool forceLocked = (SaveManager.I != null && SaveManager.I.Data.fastForwardForceLocked);

            if (forceLocked) ff.LockFromUI();
            else if (unlockedByStage || unlockedBySave) ff.UnlockFromUI();
            else ff.LockFromUI();
        }
        // === (A) Jika user klik Load Game dari menu → lompat ke level tersimpan
        if (SaveManager.I != null && SaveManager.I.RequestedContinue)
        {
            int target = Mathf.Clamp(SaveManager.I.Data.levelIndex, 0, levelScenes.Length - 1);

            if (currentIndex != target || scene.name != levelScenes[target])
            {
                // TUNDA load agar tidak bentrok dengan OnSceneLoaded frame ini
                StartCoroutine(DeferredJump(target));
                Debug.Log($"[LM] Defer jump from {scene.name} -> {levelScenes[target]} (idx {target})");
                return;
            }

            // sudah di scene target → reset flag
            SaveManager.I.ConsumeContinueRequest();
        }

        // === (B) Mark level hanya jika scene ini memang LEVEL (idx >= 0)
        if (SaveManager.I != null && !SaveManager.I.RequestedContinue && idx >= 0)
        {
            SaveManager.I.Data.levelIndex = currentIndex;
            SaveManager.I.Data.stageIndex = currentIndex / Mathf.Max(1, levelsPerStage);
            SaveManager.I.SaveToDisk();
            Debug.Log($"[LM] Mark current level idx={currentIndex} ({scene.name})");
        }

        if (idx >= 0) // hanya untuk level, bukan menu/card management
        {
            AudioManager.I?.StopOverlayMusic();

            var key = GetCurrentStageBgmKey();
            if (!string.IsNullOrEmpty(key))
                AudioManager.I?.PlayMusicIfChanged(key, true);
            else
                Debug.LogWarning("[LM] Stage BGM key kosong/tidak ada untuk stage ini.");
        }
    }


    bool HasLevelsConfigured()
    {
        if (levelScenes == null || levelScenes.Length == 0)
        {
            Debug.LogError("[LevelManager] levelScenes kosong. Isi di Inspector dan pastikan terdaftar di Build Settings.");
            return false;
        }
        return true;
    }

    public string GetCurrentStageBgmKey()
    {
        int stage = GetCurrentStage();
        if (stage <= 0 || stageBgmKeys == null || stageBgmKeys.Length == 0)
            return null;

        int index = Mathf.Clamp(stage - 1, 0, stageBgmKeys.Length - 1);
        return stageBgmKeys[index];
    }

    System.Collections.IEnumerator DeferredJump(int target)
    {
        yield return null; // tunda 1 frame biar OnSceneLoaded selesai dulu
        LoadLevelIndex(target); // ini benar-benar memuat scene target
    }
}
