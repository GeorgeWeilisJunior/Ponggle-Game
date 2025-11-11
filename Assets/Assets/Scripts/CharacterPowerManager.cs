using System;
using System.Collections.Generic;
using UnityEngine;

/// Power manager pusat untuk mengatur karakter & state power di scene gameplay.
/// KEY karakter = nama asset CharacterData (yang kamu simpan dari CharacterSelect: chosen.name).
[DefaultExecutionOrder(-100)]
public class CharacterPowerManager : MonoBehaviour
{
    public static CharacterPowerManager Instance { get; private set; }

    [Header("Active Character (ScriptableObject)")]
    [SerializeField] private CharacterData currentCharacter;

    [Header("Database Karakter (isi semua CharacterData)")]
    [Tooltip("Drag semua ScriptableObject CharacterData ke sini: Neso/Nesa/Nesda, Tu Ka La, Aposda, Porky.")]
    [SerializeField] private List<CharacterData> allCharacters = new List<CharacterData>();

    [Header("Behaviour")]
    [Tooltip("Baca key karakter dari SaveManager dan apply saat Start.")]
    [SerializeField] private bool readFromSaveOnStart = true;
    [SerializeField] private bool logDebug = false;

    // ======== Power Settings (punyamu) ========
    [Header("Tu Ka La Settings")]
    [SerializeField] private BucketWallPower bucketWall; // boleh kosong; akan dicari otomatis
    [SerializeField, Min(1)] private int tuKaLaTurns = 2;

    [Header("Aposda Settings")]
    [SerializeField] private string aposdaPowerName = "Fireball";

    [Header("Porky Settings")]
    [SerializeField] private string porkyPowerName = "More Balls, More Money";
    [SerializeField, Min(1)] private int porkyBalls = 3;

    // ======== State/UI Events ========
    public string CurrentPowerName => currentCharacter ? currentCharacter.powerName : string.Empty;
    public bool IsPowerReady { get; private set; }
    public bool nextShotTripleBall { get; private set; }
    public bool nextShotFireball { get; private set; }
    public bool IsTuKaLaActive => (tuKaLaTurnsLeft > 0) || tuKaLaSkipFirstEndTurn;

    int tuKaLaTurnsLeft = 0;
    bool tuKaLaSkipFirstEndTurn = false;

    /// Dipakai HUD untuk update teks/label power.
    public event Action<string> OnPowerChanged;
    /// Dipakai portrait/ikon untuk ganti tampilan saat karakter berubah.
    public event Action<CharacterData> OnCharacterChanged;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!bucketWall) bucketWall = FindObjectOfType<BucketWallPower>();
        if (bucketWall) bucketWall.SetActive(false);
    }

    void Start()
    {
        if (readFromSaveOnStart) ApplyFromSave();
        else ApplyCurrentCharacter();

        OnPowerChanged?.Invoke(CurrentPowerName);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // APPLY / SET CHARACTER
    // ─────────────────────────────────────────────────────────────────────────────

    /// Ambil key dari save, cari CharacterData di database, lalu apply.
    public void ApplyFromSave()
    {
        string key = SaveManager.I?.GetChosenCharacterKey();
        CharacterData picked = null;

        if (!string.IsNullOrEmpty(key))
            picked = allCharacters.Find(c => c &&
                   string.Equals(c.name, key, StringComparison.OrdinalIgnoreCase));

        if (picked == null)
        {
            int idx = SaveManager.I?.GetSelectedCharacterIndex() ?? -1;
            if (idx >= 0 && idx < allCharacters.Count) picked = allCharacters[idx];
        }

        if (picked != null)
        {
            SetCharacter(picked, alsoSave: false);
            Debug.Log($"[CharacterPower] Loaded from Save => {picked.name}");
        }
        else
        {
            // terakhir, pakai yang di Inspector biar jelas
            ApplyCurrentCharacter();
            Debug.LogWarning("[CharacterPower] Key/index not found. Using currentCharacter from Inspector.");
        }
    }


    /// Set karakter aktif (opsional: menulis balik ke save).
    public void SetCharacter(CharacterData ch, bool alsoSave = false)
    {
        if (ch == null) return;
        currentCharacter = ch;

        if (alsoSave && SaveManager.I != null)
            SaveManager.I.SetChosenCharacterKey(currentCharacter.name);

        ResetPowerState();

        if (logDebug) Debug.Log($"[CPM] Applied Character: {currentCharacter.name}");
        OnCharacterChanged?.Invoke(currentCharacter);
        OnPowerChanged?.Invoke(CurrentPowerName);
    }

    /// Set karakter berdasarkan KEY (nama asset CharacterData).
    public void SetCharacterByKey(string key, bool alsoSave = false)
    {
        if (string.IsNullOrEmpty(key)) return;
        var found = allCharacters.Find(c => c && string.Equals(c.name, key, StringComparison.Ordinal));
        if (found != null) SetCharacter(found, alsoSave);
        else Debug.LogWarning($"[CPM] SetCharacterByKey: '{key}' tidak ditemukan di database.");
    }

    /// Pakai currentCharacter dari Inspector (tanpa baca save).
    public void ApplyCurrentCharacter()
    {
        ResetPowerState();
        OnCharacterChanged?.Invoke(currentCharacter);
        OnPowerChanged?.Invoke(CurrentPowerName);
        if (logDebug && currentCharacter) Debug.Log($"[CPM] ApplyCurrentCharacter: {currentCharacter.name}");
    }

    void ResetPowerState()
    {
        IsPowerReady = false;
        nextShotTripleBall = false;
        nextShotFireball = false;
        tuKaLaTurnsLeft = 0;
        tuKaLaSkipFirstEndTurn = false;
        if (bucketWall) bucketWall.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // RUNTIME EVENTS (dipanggil oleh gameplay)
    // ─────────────────────────────────────────────────────────────────────────────

    /// Dipanggil saat GREEN peg terpicu (mengaktifkan efek power).
    public void TryActivatePower()
    {
        if (GameManager.Instance && GameManager.Instance.InFever) return;
        if (!currentCharacter) return;

        if (currentCharacter.powerEffectPrefab)
            Instantiate(currentCharacter.powerEffectPrefab, Camera.main.transform.position, Quaternion.identity);

        if (!string.IsNullOrEmpty(currentCharacter.powerSfxKey))
            AudioManager.I.Play(currentCharacter.powerSfxKey, Camera.main.transform.position);
        else if (currentCharacter.fallbackClip)
            PlayOneShot(currentCharacter.fallbackClip);

        string cname = (currentCharacter.characterName ?? "").ToLowerInvariant();
        string pname = (currentCharacter.powerName ?? "").ToLowerInvariant();

        // Triple Shot (Neso/Nesa/Nesda)
        if (cname.Contains("neso") || cname.Contains("nesa") || cname.Contains("nesda"))
        {
            nextShotTripleBall = true;
            nextShotFireball = false;
            IsPowerReady = true;
            OnPowerChanged?.Invoke(CurrentPowerName);
            return;
        }

        // Tu Ka La (Bucket Walls N turns, dihitung setelah turn yang mengaktifkan)
        if (cname.Contains("tu ka la") || cname.Contains("tukala") || pname.Contains("pyramid"))
        {
            if (bucketWall) bucketWall.SetActive(true);
            tuKaLaTurnsLeft = tuKaLaTurns;
            tuKaLaSkipFirstEndTurn = true;
            IsPowerReady = true;
            OnPowerChanged?.Invoke($"{CurrentPowerName} ({tuKaLaTurnsLeft})");
            return;
        }

        // Aposda (Fireball)
        if (cname.Contains("aposda") || pname.Contains("fireball"))
        {
            nextShotFireball = true;
            nextShotTripleBall = false;
            IsPowerReady = true;
            OnPowerChanged?.Invoke(string.IsNullOrEmpty(CurrentPowerName) ? aposdaPowerName : CurrentPowerName);
            return;
        }

        // Porky (langsung +N bola, tidak menunggu tembakan)
        if (cname.Contains("porky") || pname.Contains("more balls") || pname.Contains("more money") || pname.Contains("richer"))
        {
            GameManager.Instance?.GainBall(porkyBalls);

            // +5 Energy Limit
            var inv = CardInventory.I;
            if (inv != null)
            {
                inv.SetEnergyLimit(inv.EnergyLimit + 5);
                Debug.Log($"[Porky Power] Energy Limit +5 => {inv.EnergyLimit}");

                // Persist ke save kalau ada SaveManager
                if (SaveManager.I != null)
                {
                    SaveManager.I.Data.energyLimit = inv.EnergyLimit;
                    SaveManager.I.SaveToDisk();
                }
            }

            IsPowerReady = false;
            OnPowerChanged?.Invoke(string.IsNullOrEmpty(CurrentPowerName) ? porkyPowerName : CurrentPowerName);
            return;
        }


        // fallback generic
        IsPowerReady = true;
        OnPowerChanged?.Invoke(CurrentPowerName);
    }

    /// Panggil dari Launcher saat peluru ditembak.
    public void OnBallShot()
    {
        if (nextShotTripleBall || nextShotFireball)
        {
            nextShotTripleBall = false;
            nextShotFireball = false;
            IsPowerReady = false;
            OnPowerChanged?.Invoke(CurrentPowerName);
        }
    }

    /// Panggil dari GameManager saat turn berakhir.
    public void OnTurnEnded()
    {
        if (!IsTuKaLaActive) return;

        if (tuKaLaSkipFirstEndTurn)
        {
            tuKaLaSkipFirstEndTurn = false;
            OnPowerChanged?.Invoke($"{CurrentPowerName} ({tuKaLaTurnsLeft})");
            return;
        }

        tuKaLaTurnsLeft--;
        if (tuKaLaTurnsLeft <= 0)
        {
            if (bucketWall) bucketWall.SetActive(false);
            IsPowerReady = false;
            OnPowerChanged?.Invoke(CurrentPowerName);
        }
        else
        {
            OnPowerChanged?.Invoke($"{CurrentPowerName} ({tuKaLaTurnsLeft})");
        }
    }

    /// Reset semua state ketika masuk Fever.
    public void OnFeverStart()
    {
        ResetPowerState();
        OnPowerChanged?.Invoke(CurrentPowerName);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // UTIL
    // ─────────────────────────────────────────────────────────────────────────────

    void PlayOneShot(AudioClip clip)
    {
        if (!clip) return;
        var go = new GameObject("OneShotAudio");
        var src = go.AddComponent<AudioSource>();
        src.clip = clip; src.spatialBlend = 0f;
        src.Play(); Destroy(go, clip.length);
    }

    public CharacterData GetCurrentCharacter() => currentCharacter;
    public IReadOnlyList<CharacterData> GetAllCharacters() => allCharacters;

    [ContextMenu("Debug/Apply From Save")]
    void _CtxApplyFromSave() => ApplyFromSave();

    [ContextMenu("Debug/Log Current")]
    void _CtxLogCurrent()
    {
        Debug.Log($"[CPM] Current={(currentCharacter ? currentCharacter.name : "(null)")}, " +
                  $"TripleReady={nextShotTripleBall}, FireballReady={nextShotFireball}, " +
                  $"TuKaLaActive={IsTuKaLaActive}({tuKaLaTurnsLeft})");
    }
}
