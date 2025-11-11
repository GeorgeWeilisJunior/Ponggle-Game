using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[DefaultExecutionOrder(-900)]
public class SaveManager : MonoBehaviour
{
    public static SaveManager I { get; private set; }

    public SaveData Data { get; private set; } = new SaveData();

    public bool HasSave => _cachedHasSave ?? File.Exists(SavePath);
    public bool RequestedContinue { get; set; }
    public void ConsumeContinueRequest() => RequestedContinue = false;
    const int MAX_LIVES = 3;
    string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");
    bool? _cachedHasSave;

    [Header("Leaderboard")]
    [Tooltip("Key papan skor untuk run Adventure 25 level.")]
    [SerializeField] string leaderboardKey = "Adventure_All25";
    public const int TOTAL_LEVELS = 25; // index 0..24

    public string GetLeaderboardKey()
    {
        return string.IsNullOrWhiteSpace(leaderboardKey) ? "Adventure_All25" : leaderboardKey;
    }

    [Serializable]
    class PendingLb
    {
        public string key;
        public string name;
        public int score;
    }

    const string PENDING_LB_PPKEY = "PENDING_LEADERBOARD_JSON";
    PendingLb _pendingLb; // cache runtime

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        TryLoadFromDisk();                 // kalau belum ada file, Data tetap default

        // Hanya update meta + save JIKA memang sudah ada save sebelumnya
        if (HasSave)
        {
            Data.sessionsPlayed++;
            Data.lastPlayedAt = DateTime.UtcNow;
            SaveToDisk();
        }

        TryPushTotalScoreToHUD();
        TryFlushPendingLeaderboard();
    }


    void SavePendingToPlayerPrefs(PendingLb p)
    {
        try
        {
            var json = JsonUtility.ToJson(p);
            PlayerPrefs.SetString(PENDING_LB_PPKEY, json);
            PlayerPrefs.Save();
        }
        catch { /* ignore */ }
    }

    PendingLb LoadPendingFromPlayerPrefs()
    {
        try
        {
            if (!PlayerPrefs.HasKey(PENDING_LB_PPKEY)) return null;
            var json = PlayerPrefs.GetString(PENDING_LB_PPKEY, "");
            if (string.IsNullOrEmpty(json)) return null;
            return JsonUtility.FromJson<PendingLb>(json);
        }
        catch { return null; }
    }

    void ClearPendingInPlayerPrefs()
    {
        try
        {
            if (PlayerPrefs.HasKey(PENDING_LB_PPKEY))
            {
                PlayerPrefs.DeleteKey(PENDING_LB_PPKEY);
                PlayerPrefs.Save();
            }
        }
        catch { /* ignore */ }
    }

    /* ========================= PUBLIC API ========================= */

    // Dipanggil dari Name Entry (setelah user confirm nama)
    public void NewGame(string playerName)
    {
        // 1) Simpan flag "global/permanent" dari data lama
        //    (kalau belum ada save, Data sudah objek default dari Awake/TryLoad)
        bool keepAposda = Data.aposdaUnlocked;
        bool keepPorky = Data.porkyUnlocked;
        bool keepClearedOnce = Data.gameClearedOnce;

        // (opsional: kalau mau Neso/TuKaLa selalu true, sebenarnya default SaveData sudah true)
        bool keepNesoTu = true; // Data.nesoNesaNesdaUnlocked && Data.tuKaLaUnlocked;

        // 2) Buat save baru (reset progress run)
        Data = new SaveData();
        Data.levelScores = new int[TOTAL_LEVELS];
        Data.ownedCards = new List<string>();
        Data.droppedBuffer = new List<string>();
        Data.pickedForNext = new List<string>();
        Data.elementReactionsSeen = new HashSetString(); // sesuaikan tipe kamu
        Data.energyLimit = 10;
        Data.lives = 3;
        Data.levelIndex = 0;
        Data.stageIndex = 0;
        Data.totalScore = 0;

        // 3) Restore GLOBAL FLAGS supaya bertahan lintas run
        Data.aposdaUnlocked = keepAposda;
        Data.porkyUnlocked = keepPorky;
        Data.gameClearedOnce = keepClearedOnce;
        Data.nesoNesaNesdaUnlocked = keepNesoTu;
        Data.tuKaLaUnlocked = keepNesoTu;

        // 4) Inisialisasi profil & progress awal run
        Data.playerName = playerName ?? "";
        Data.createdAt = DateTime.UtcNow;
        Data.lastPlayedAt = Data.createdAt;
        Data.sessionsPlayed = 1;

        Data.levelScores = new int[TOTAL_LEVELS];
        Data.totalScore = 0;
        Data.levelIndex = 0;
        Data.stageIndex = 0;
        Data.lives = 3;
        Data.totalScore = 0;
        Data.energyLimit = 10; // atau 15 jika Easy Mode, sesuaikan logikamu

        // Default karakter (kalau belum dipilih)
        if (string.IsNullOrEmpty(Data.chosenCharacterId))
            Data.chosenCharacterId = "NesoNesaNesda";

        _cachedHasSave = true;
        SaveToDisk();
        TryPushTotalScoreToHUD();
    }


    // Dipanggil dari Main Menu → Load/Continue
    public void ContinueGame()
    {
        if (!HasSave) return;
        TryLoadFromDisk();
        RequestedContinue = true;           // ← tandai bahwa user klik Load/Continue
        TryPushTotalScoreToHUD();
        Debug.Log($"[Save] Continue requested. levelIndex={Data.levelIndex}");
    }


    // Kalau punya panel Name Entry terpisah
    public void SetPlayerName(string name)
    {
        Data.playerName = name ?? "";
        SaveToDisk();
    }

    /* ===== Karakter (API baru + alias lama) ===== */

    // === Nama baru yang dipakai CharacterPowerManager ===
    public void SetChosenCharacterKey(string key)
    {
        // normalisasi: kalau kosong, pakai default triple-shot
        Data.chosenCharacterId = string.IsNullOrEmpty(key) ? "NesoNesaNesda" : key;

        // sinkron legacy agar sistem lama masih bekerja
        Data.currentCharacter = Data.chosenCharacterId;

        SaveToDisk();
    }

    public string GetChosenCharacterKey()
    {
        // prioritas ke field baru; fallback ke legacy
        if (!string.IsNullOrEmpty(Data.chosenCharacterId)) return Data.chosenCharacterId;
        return string.IsNullOrEmpty(Data.currentCharacter) ? "NesoNesaNesda" : Data.currentCharacter;
    }

    // === Alias lama (tetap ada agar tidak merusak referensi lain) ===
    public void SetChosenCharacter(string key) => SetChosenCharacterKey(key);
    public string GetChosenCharacter() => GetChosenCharacterKey();

    public void DeleteSave()
    {
        try
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
            _cachedHasSave = false;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DeleteSave failed: {e}");
        }
    }

    /* ---------- Dipanggil oleh gameplay ---------- */

    // Menang 1 level
    public void OnLevelCompleted(int nextLevelIndex, int totalScore, int ballsLeft)
    {
        // ballsLeft tetap disimpan
        Data.ballsLeft = ballsLeft;

        // Pastikan array ada & index level valid
        if (Data.levelScores == null || Data.levelScores.Length != TOTAL_LEVELS)
            Data.levelScores = new int[TOTAL_LEVELS];

        int curIdx = Mathf.Clamp(Data.levelIndex, 0, TOTAL_LEVELS - 1);

        // Ambil skor level SAJA dari ScoreManager
        int levelOnlyScore = ScoreManager.LevelScore;

        // Simpan skor terbaik untuk level ini di run saat ini
        Data.levelScores[curIdx] = Mathf.Max(Data.levelScores[curIdx], levelOnlyScore);

        // Hitung total run dari array
        int runTotal = 0;
        for (int i = 0; i < TOTAL_LEVELS; i++) runTotal += Data.levelScores[i];
        Data.totalScore = runTotal; // untuk keperluan riwayat/submit

        // === REWARD MENANG: +1 life (di-clamp ke MAX_LIVES) ===
        GainLife(1);

        // Deteksi tamat (last level)
        bool isAtLastLevelNow = Data.levelIndex >= (TOTAL_LEVELS - 1);
        bool finishedAll = (nextLevelIndex >= TOTAL_LEVELS) || isAtLastLevelNow;

        if (finishedAll)
        {
            SubmitLeaderboardWin();              // ← akan kirim Data.totalScore (runTotal)
            AddRunHistory("Win", Data.totalScore);

            // Unlocks / flags seperti sebelumnya…
            Data.gameClearedOnce = true;
            Data.porkyUnlocked = true;

            // Reset run baru
            Data.levelIndex = 0;
            Data.stageIndex = 0;
            Data.lives = 3;                      // tetap reset ke 3 setelah tamat
            Data.totalScore = 0;
            Data.levelScores = new int[TOTAL_LEVELS];  // kosongkan skor per-level untuk run berikutnya
        }
        else
        {
            // Lanjut ke level berikutnya
            Data.levelIndex = Mathf.Clamp(nextLevelIndex, 0, 999);
            Data.stageIndex = Data.levelIndex / 5;

            if (!Data.fastForwardUnlocked && nextLevelIndex >= 5)
                Data.fastForwardUnlocked = true;
        }

        Data.lastPlayedAt = DateTime.UtcNow;
        ClearActiveCardsThisLevel(false);
        SaveToDisk();
        // (JANGAN dorong totalScore ke HUD; biarkan HUD tetap per-level)
    }


    // Kalah 1 level
    public void OnLevelFailed(int keepLevelIndexIfRetry)
    {
        Data.lives = Mathf.Max(0, Data.lives - 1);

        Data.levelIndex = keepLevelIndexIfRetry;
        Data.stageIndex = Data.levelIndex / 5;

        if (Data.lives <= 0)
        {
            ClearActiveCardsThisLevel(false);
            // Game Over untuk stage ini → kembali ke awal stage
            int stageStart = Data.stageIndex * 5;
            Data.levelIndex = stageStart;
            Data.stageIndex = Data.levelIndex / 5;
            Data.lives = 3;

            // Desainmu: reset skor perjalanan
            Data.totalScore = 0;
            Data.levelScores = new int[TOTAL_LEVELS];  // ⬅️ reset juga
            AddRunHistory("Lose", 0);

            AddRunHistory("Lose", 0);
        }

        Data.lastPlayedAt = DateTime.UtcNow;
        SaveToDisk();
        TryPushTotalScoreToHUD();
    }

    public List<string> ConsumePickedForNext()
    {
        if (Data.pickedForNext == null)
            Data.pickedForNext = new List<string>();
        var list = new List<string>(Data.pickedForNext);
        Data.pickedForNext.Clear();
        SaveToDisk();
        return list;
    }

    /* ========================= CARDS ========================= */

    public void AddDropToBuffer(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return;
        if (!Data.droppedBuffer.Contains(cardId))
            Data.droppedBuffer.Add(cardId);
        SaveToDisk();
    }

    public void GainLife(int amount = 1)
    {
        if (Data == null) return;
        int before = Data.lives;
        Data.lives = Mathf.Clamp(Data.lives + amount, 0, MAX_LIVES);
        if (Data.lives != before)
        {
            // animasi kecil pop-in sudah diatur di LivesUI.SetLives(..., immediate:false)
            try { LivesUI.Instance?.RefreshFromSave(immediate: false); } catch { /* scene mungkin belum ada UI */ }
            SaveToDisk();
        }
    }

    public void ClaimDropsToInventory()
    {
        if (Data.droppedBuffer != null && Data.droppedBuffer.Count > 0)
        {
            if (Data.ownedCards == null) Data.ownedCards = new System.Collections.Generic.List<string>();
            foreach (var c in Data.droppedBuffer)
            {
                if (string.IsNullOrEmpty(c)) continue;
                Data.ownedCards.Add(c); // <-- boleh duplikat
            }
            Data.droppedBuffer.Clear();
            SaveToDisk();
        }
    }

    public void SetPickedForNextLevel(List<string> picked, int totalEnergyLimit)
    {
        Data.pickedForNext = picked != null ? new List<string>(picked) : new List<string>();
        if (totalEnergyLimit > 0) Data.energyLimit = totalEnergyLimit;
        SaveToDisk();
    }

    public IReadOnlyList<string> GetActiveCardsThisLevel()
    {
        if (Data.activeCardsThisLevel == null) Data.activeCardsThisLevel = new List<string>();
        return Data.activeCardsThisLevel;
    }

    public void SetActiveCardsThisLevel(List<string> ids)
    {
        Data.activeCardsThisLevel = (ids != null) ? new List<string>(ids) : new List<string>();
        SaveToDisk();
    }

    public void ClearActiveCardsThisLevel(bool save = true)
    {
        if (Data.activeCardsThisLevel == null || Data.activeCardsThisLevel.Count == 0) return;
        Data.activeCardsThisLevel.Clear();
        if (save) SaveToDisk();
    }

    public void ClearPickedForNext()
    {
        Data.pickedForNext.Clear();
        SaveToDisk();
    }
    public void TryFlushPendingLeaderboard()
    {
        try
        {
            if (_pendingLb == null) _pendingLb = LoadPendingFromPlayerPrefs();
            if (_pendingLb == null) return;
            if (LocalLeaderboardManager.I == null) return;

#if UNITY_EDITOR
            Debug.Log($"[Save] FLUSH pending LB key='{_pendingLb.key}' name='{_pendingLb.name}' score={_pendingLb.score}");
#endif
            LocalLeaderboardManager.I.Submit(_pendingLb.key, _pendingLb.name, _pendingLb.score);

            _pendingLb = null;
            ClearPendingInPlayerPrefs();
#if UNITY_EDITOR
            Debug.Log("[Save] Pending leaderboard flushed.");
#endif
        }
        catch (Exception e)
        {
            Debug.LogWarning($"TryFlushPendingLeaderboard failed: {e}");
        }
    }


    /* ========================= INTERNALS ========================= */

    void SubmitLeaderboardWin()
    {
        try
        {
            string key = GetLeaderboardKey();
            string name = Data.playerName;
            int score = Data.totalScore;

            if (LocalLeaderboardManager.I != null)
            {
#if UNITY_EDITOR
                Debug.Log($"[Save] Submit LB NOW key='{key}' name='{name}' score={score}");
#endif
                LocalLeaderboardManager.I.Submit(key, name, score);
                return;
            }

            // Manager belum ada → simpan pending (akan di-flush di Main Menu)
            _pendingLb = new PendingLb { key = key, name = name, score = score };
            SavePendingToPlayerPrefs(_pendingLb);
#if UNITY_EDITOR
            Debug.Log($"[Save] Pending LB STORED key='{key}' name='{name}' score={score}");
#endif
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SubmitLeaderboardWin failed: {e}");
        }
    }



    void AddRunHistory(string result, int score)
    {
        try
        {
            Data.leaderboard.Add(new LeaderboardEntry(Data.playerName, score, result));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"AddRunHistory failed: {e}");
        }
    }

    void TryPushTotalScoreToHUD()
    {
        try
        {
            ScoreManager.SetTotalScore(Mathf.Max(0, Data.totalScore));
        }
        catch { /* scene belum punya ScoreManager; skip */ }
    }

    // Reset data di MEMORI ke default.
    // Jika persistToDisk = true → langsung SaveToDisk() (bikin file baru).
    // Jika persistToDisk = false → JANGAN bikin file; tandai HasSave = false.
    public void ResetInMemoryToDefaults(bool persistToDisk)
    {
        Data = new SaveData();

        // siapkan array skor per level (25 level = TOTAL_LEVELS)
        Data.levelScores = new int[TOTAL_LEVELS];
        Data.ownedCards = new List<string>();
        Data.droppedBuffer = new List<string>();
        Data.pickedForNext = new List<string>();
        Data.elementReactionsSeen = new HashSetString(); // kalau kamu pakai ini
        Data.energyLimit = 10;
        Data.lives = 3;
        Data.levelIndex = 0;
        Data.stageIndex = 0;
        Data.totalScore = 0;

        // pastikan semua unlock balik ke false (benar-benar fresh)
        Data.nesoNesaNesdaUnlocked = false;
        Data.tuKaLaUnlocked = false;
        Data.aposdaUnlocked = false;
        Data.porkyUnlocked = false;
        Data.gameClearedOnce = false;

        // kartu & limit default
        Data.ownedCards = new System.Collections.Generic.List<string>();
        Data.energyLimit = 10;

        if (persistToDisk)
        {
            SaveToDisk();                // ← bikin file save baru (fresh)
        }
        else
        {
            // Jangan bikin file; tandai bahwa tidak ada save
            _cachedHasSave = false;
        }

#if UNITY_EDITOR
        Debug.Log("[Save] ResetInMemoryToDefaults persist=" + persistToDisk);
#endif
    }

    // Reset data di MEMORI ke default lalu langsung simpan ke disk.
    // Gunakan setelah DeleteSave() / DeleteAll PlayerPrefs agar state konsisten.
    public void ResetInMemoryToDefaultsAndSave()
    {
        ResetInMemoryToDefaults(true);
    }


    void TryLoadFromDisk()
    {
        try
        {
            if (!File.Exists(SavePath)) { _cachedHasSave = false; return; }
            var json = File.ReadAllText(SavePath, Encoding.UTF8);
            var loaded = JsonUtility.FromJson<SaveData>(json);
            if (loaded != null) Data = loaded;
            Data.levelScores ??= new int[TOTAL_LEVELS];
            Data.ownedCards ??= new List<string>();
            Data.droppedBuffer ??= new List<string>();
            Data.pickedForNext ??= new List<string>();
            Data.elementReactionsSeen ??= new HashSetString(); // kalau dipakai

            // Backward-compat: isi chosenCharacterId bila kosong
            if (string.IsNullOrEmpty(Data.chosenCharacterId))
            {
                if (!string.IsNullOrEmpty(Data.currentCharacter))
                    Data.chosenCharacterId = Data.currentCharacter;
                else
                    Data.chosenCharacterId = "NesoNesaNesda";
            }

            _cachedHasSave = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Load failed: {e}");
            _cachedHasSave = false;
        }
    }

    public void SaveToDisk()
    {
        try
        {
            Data.lastPlayedAt = DateTime.UtcNow;
            var json = JsonUtility.ToJson(Data, false);
            File.WriteAllText(SavePath, json, Encoding.UTF8);
            _cachedHasSave = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Save failed: {e}");
        }
    }

    // Legacy helper: tetap ada agar tidak memutus referensi lama
    public void SetSelectedCharacterIndex(int idx)
    {
        Data.selectedCharacterIndex = Mathf.Max(0, idx);
        SaveToDisk();
    }
    public void RegisterGameClearOnce()
    {
        if (Data == null) return;
        Data.gameClearedOnce = true;
        Data.porkyUnlocked = true;  // Porky kebuka setelah tamat 1x
        SaveToDisk();
#if UNITY_EDITOR
        Debug.Log("[Save] Game cleared once → Porky unlocked");
#endif
    }
    // Catat bahwa sebuah ELEMENT REACTION terjadi (pakai ID/string bebas).
    public void RegisterElementReaction(string reactionId)
    {
        if (string.IsNullOrEmpty(reactionId) || Data == null) return;

        // simpan unik (HashSetString pakai List di belakang, tapi .Add() sudah tahan duplikat)
        bool added = Data.elementReactionsSeen.Add(reactionId);

        // Kalau baru (belum pernah), cek apakah sudah memenuhi syarat unlock Aposda
        if (added)
        {
            SaveToDisk(); // commit dulu progress counter

            // misal syaratnya 6 jenis reaction unik
            const int REQUIRED = 6;
            if (!Data.aposdaUnlocked && Data.elementReactionsSeen.Count >= REQUIRED)
            {
                Data.aposdaUnlocked = true;
                SaveToDisk();
#if UNITY_EDITOR
                Debug.Log($"[Save] Aposda unlocked! ReactionsSeen={Data.elementReactionsSeen.Count}");
#endif
            }
        }
    }

    public int GetSelectedCharacterIndex() => Mathf.Max(0, Data.selectedCharacterIndex);
}
