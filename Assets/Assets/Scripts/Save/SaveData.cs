using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    /* ───────── Profile / meta ───────── */
    public string playerName = "";
    public DateTime createdAt = DateTime.UtcNow;
    public DateTime lastPlayedAt = DateTime.UtcNow;
    public int sessionsPlayed = 0;

    // Legacy (dipertahankan demi kompatibilitas)
    public int selectedCharacterIndex = 0;

    // Karakter aktif berbasis KEY (nama asset CharacterData)
    // Contoh: "NesoNesaNesda", "Tu Ka La", "Aposda", "Porky"
    public string chosenCharacterId = "";

    /* ───────── Settings / flags ───────── */
    public bool fastForwardUnlocked = false;
    public bool fastForwardForceLocked = false;

    /* ───────── Progress ─────────
       - levelIndex: index level saat ini (0..24)
       - stageIndex: 0..4 (tiap 5 level = 1 stage)
       - lives: nyawa sisa di perjalanan
       - totalScore: skor kumulatif jalan
    */
    public int levelIndex = 0;
    public int stageIndex = 0;
    public int lives = 3;
    public int ballsLeft = 10;
    public int totalScore = 0;
    public int[] levelScores;

    /* ───────── Inventory / Cards ───────── */
    public List<string> ownedCards = new();
    public List<string> droppedBuffer = new();
    public List<string> pickedForNext = new();
    public int energyLimit = 10;

    /* ───────── Character Unlocks ───────── */
    public bool nesoNesaNesdaUnlocked = true;
    public bool tuKaLaUnlocked = true;
    public bool aposdaUnlocked = false;  // unlock setelah semua reaction terjadi
    public bool porkyUnlocked = false;   // unlock setelah clear sekali

    // Legacy fallback; dibiarkan untuk kompatibilitas lama
    public string currentCharacter = "NesoNesaNesda";

    /* ───────── Achievement / counters ───────── */
    public HashSetString elementReactionsSeen = new();
    public bool gameClearedOnce = false;

    /* ───────── Leaderboard (riwayat lokal) ───────── */
    public List<LeaderboardEntry> leaderboard = new();
    public List<string> activeCardsThisLevel = new List<string>();
}

[Serializable]
public class LeaderboardEntry
{
    public string playerName;
    public int finalScore;
    public string runResult; // "Win" / "Lose"
    public DateTime when;

    public LeaderboardEntry(string name, int score, string result)
    {
        playerName = name;
        finalScore = score;
        runResult = result;
        when = DateTime.UtcNow;
    }
}


/* JsonUtility tidak support HashSet; wrapper kecil */
[Serializable]
public class HashSetString
{
    public List<string> data = new();

    public bool Add(string s)
    {
        if (data.Contains(s)) return false;
        data.Add(s); return true;
    }

    public bool Contains(string s) => data.Contains(s);
    public int Count => data.Count;
}
