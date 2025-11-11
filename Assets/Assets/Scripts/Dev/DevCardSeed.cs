using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// DevCardSeed
/// Mengisi Save dengan semua kartu (atau subset) untuk testing.
/// Aktif HANYA bila define symbol DEV_CARDS diaktifkan
/// atau sedang dijalankan di Unity Editor.
/// </summary>
[DefaultExecutionOrder(-800)]
public class DevCardSeed : MonoBehaviour
{
#if !DEV_CARDS
    void Awake()
    {
        // Jika bukan build DEV_CARDS dan bukan di Editor, matikan diri sendiri
        if (!Application.isEditor)
        {
            Destroy(this);
            return;
        }
    }
#else
    [Header("Owned Cards Seed")]
    [Tooltip("Kalau kosong, ambil SEMUA CardData di Resources/Cards/")]
    public string[] ownedCardIds = new string[0];

    [Header("Picked for Next (uji efek)")]
    public string[] pickedForNextIds = new string[0];
    public int energyLimit = 10;

    [Header("Optional")]
    [Tooltip("Nama scene gameplay untuk loncat setelah Next")]
    public string gameplaySceneName = "1-1";
    public bool autoJumpToGameplayOnNext = false;

    void Awake()
    {
        // Pastikan SaveManager ada
        if (SaveManager.I == null)
        {
            var go = new GameObject("SaveManager (Debug)");
            go.AddComponent<SaveManager>();
        }

        // 1) Seed Owned
        EnsureOwned();

        // 2) Seed Picked (opsional)
        if (pickedForNextIds != null && pickedForNextIds.Length > 0)
        {
            var list = pickedForNextIds.Distinct().ToList();
            SaveManager.I.SetPickedForNextLevel(list, Mathf.Max(energyLimit, 1));
        }

        // 3) (Opsional) Hook tombol Next agar loncat ke gameplay saat ditekan
        if (autoJumpToGameplayOnNext)
        {
            var ui = FindObjectOfType<CardManagementUI>();
            if (ui != null)
                ui.gameObject.AddComponent<DevNextHook>().Init(gameplaySceneName);
        }
    }

    void EnsureOwned()
    {
        List<string> ids = new();
        if (ownedCardIds != null && ownedCardIds.Length > 0)
        {
            ids.AddRange(ownedCardIds);
        }
        else
        {
            var all = Resources.LoadAll<CardData>("Cards");
            foreach (var cd in all)
                if (cd && !string.IsNullOrEmpty(cd.id))
                    ids.Add(cd.id);
        }
        ids = ids.Distinct().ToList();

        foreach (var id in ids)
            SaveManager.I.AddDropToBuffer(id);
        SaveManager.I.ClaimDropsToInventory();
    }

    class DevNextHook : MonoBehaviour
    {
        string _scene;
        public void Init(string sceneName) => _scene = sceneName;
        void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
        void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }
        void OnSceneLoaded(Scene s, LoadSceneMode m) { }
        public void JumpNow()
        {
            if (!string.IsNullOrEmpty(_scene))
                SceneManager.LoadScene(_scene);
        }
    }
#endif
}
