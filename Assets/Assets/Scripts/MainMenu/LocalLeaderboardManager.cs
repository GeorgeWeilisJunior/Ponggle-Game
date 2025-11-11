using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public class LocalLeaderboardManager : MonoBehaviour
{
    // ---------- Singleton ----------
    public static LocalLeaderboardManager I { get; private set; }

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        filePath = Path.Combine(Application.persistentDataPath, FILE_NAME);
        Load(); // penting: baca dari disk saat start
    }

    void OnApplicationPause(bool pause)
    {
        if (pause) Save(); // jaga-jaga saat Editor stop/play
    }

    void OnApplicationQuit()
    {
        Save();
    }

    // ---------- Model ----------
    [Serializable] public class Entry { public string name = "YOU"; public int score; public long timestamp; }
    [Serializable] public class Board { public int keepTop = 50; public List<Entry> entries = new List<Entry>(); }

    // JsonUtility tidak bisa serialize Dictionary → bungkus jadi list saat save/load
    [Serializable] class BoardKV { public string key; public Board board; }
    [Serializable] class DBWrap { public List<BoardKV> boards = new List<BoardKV>(); }

    class DB { public Dictionary<string, Board> boards = new Dictionary<string, Board>(); }

    DB db = new DB();

    // ---------- File I/O ----------
    const string FILE_NAME = "leaderboard_local.json";
    string filePath;

    void Save()
    {
        try
        {
            var wrap = new DBWrap();
            foreach (var kv in db.boards)
                wrap.boards.Add(new BoardKV { key = kv.Key, board = kv.Value });

            var json = JsonUtility.ToJson(wrap, true);
            File.WriteAllText(filePath, json);
#if UNITY_EDITOR
            Debug.Log($"[LocalLB] Saved → {filePath}");
#endif
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LocalLB] Save failed: {e}");
        }
    }

    void Load()
    {
        try
        {
            if (!File.Exists(filePath))
            {
#if UNITY_EDITOR
                Debug.Log($"[LocalLB] No file yet. Will create on first save. Path: {filePath}");
#endif
                db.boards.Clear();
                return;
            }

            var json = File.ReadAllText(filePath);
            var wrap = JsonUtility.FromJson<DBWrap>(json);
            db.boards.Clear();
            if (wrap?.boards != null)
            {
                foreach (var kv in wrap.boards)
                {
                    if (string.IsNullOrEmpty(kv.key) || kv.board == null) continue;
                    db.boards[kv.key] = kv.board;
                }
            }
#if UNITY_EDITOR
            Debug.Log($"[LocalLB] Loaded {db.boards.Count} board(s) from {filePath}");
#endif
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LocalLB] Load failed: {e}");
            db.boards.Clear();
        }
    }

    // ---------- API ----------
    public event Action OnChanged;

    public void Submit(string boardKey, string playerName, int score)
    {
#if UNITY_EDITOR
        Debug.Log($"[LocalLB] Submit -> key='{boardKey}' name='{playerName}' score={score}");
#endif
        if (!db.boards.TryGetValue(boardKey, out var b))
        {
            b = new Board();
            db.boards[boardKey] = b;
        }

        if (string.IsNullOrWhiteSpace(playerName)) playerName = "YOU";
        playerName = playerName.Trim();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 1) Cegah duplikat klik ganda (nama & skor sama dalam 2 detik)
        const int DUP_WINDOW_SEC = 2;
        for (int i = 0; i < b.entries.Count; i++)
        {
            var ex = b.entries[i];
            if (ex.name == playerName && ex.score == score && (now - ex.timestamp) <= DUP_WINDOW_SEC)
            {
#if UNITY_EDITOR
                Debug.Log("[LocalLB] Skip: duplicate within short window.");
#endif
                ex.timestamp = now;
                Save();
                OnChanged?.Invoke();
                return;
            }
        }

        // 2) Replace jika nama sama & skor lebih baik; abaikan jika lebih jelek/sama
        for (int i = 0; i < b.entries.Count; i++)
        {
            var ex = b.entries[i];
            if (ex.name == playerName)
            {
                if (score > ex.score)
                {
                    ex.score = score;
                    ex.timestamp = now;
#if UNITY_EDITOR
                    Debug.Log("[LocalLB] Updated existing name with better score.");
#endif
                    b.entries.Sort((a, c) => c.score.CompareTo(a.score));
                    Save();
                    OnChanged?.Invoke();
                    return;
                }
                else
                {
#if UNITY_EDITOR
                    Debug.Log("[LocalLB] Ignored: not better than existing score.");
#endif
                    return;
                }
            }
        }

        // 3) Nama baru → tambah baris
        b.entries.Add(new Entry { name = playerName, score = score, timestamp = now });
        b.entries.Sort((a, c) => c.score.CompareTo(a.score));
        if (b.entries.Count > b.keepTop)
            b.entries.RemoveRange(b.keepTop, b.entries.Count - b.keepTop);

        Save();
        OnChanged?.Invoke();
    }

    public IReadOnlyList<Entry> GetTop(string boardKey, int maxCount = 12)
    {
        if (!db.boards.TryGetValue(boardKey, out var b) || b.entries == null)
            return Array.Empty<Entry>();
        int n = Mathf.Min(maxCount, b.entries.Count);
        return b.entries.GetRange(0, n);
    }

    public void ClearBoard(string boardKey)
    {
        if (!db.boards.TryGetValue(boardKey, out var b))
        {
            b = new Board();
            db.boards[boardKey] = b;
        }
        b.entries.Clear();
        Save();
        OnChanged?.Invoke();
    }

    public void ClearAll()
    {
        db.boards.Clear();
        Save();
        OnChanged?.Invoke();
    }
}
