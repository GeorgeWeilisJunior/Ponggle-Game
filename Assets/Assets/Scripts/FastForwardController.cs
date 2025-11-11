using System;
using UnityEngine;

public class FastForwardController : MonoBehaviour
{
    public static FastForwardController Instance { get; private set; }

    [Header("Persisted Unlock")]
    [SerializeField] string prefsUnlockedKey = "FF_Unlocked";
    [Tooltip("Paksa locked saat start (contoh: Stage 1). Menimpa PlayerPrefs lama.")]
    [SerializeField] bool forceLockedOnStart = true;
    [SerializeField] bool unlockedByDefaultInEditor = false; // untuk tes cepat di Editor

    [Header("Speed")]
    [SerializeField, Range(1f, 4f)] float ffScale = 1.8f;

    [Header("Inputs")]
    [Tooltip("Tahan klik kanan untuk FF sementara.")]
    [SerializeField] bool allowRightMouseHold = true;

    [Tooltip("Tekan/toggle Space untuk FF (ON/OFF).")]
    [SerializeField] bool allowSpacebarToggle = true;

    [Header("SFX (opsional)")]
    [SerializeField] string sfxToggleOnKey = "";
    [SerializeField] string sfxToggleOffKey = "";

    public bool IsUnlocked { get; private set; }
    public bool IsActive { get; private set; }

    public event Action<bool> OnUnlockedChanged;
    public event Action<bool> OnActiveChanged;

    float origFixedDT = .02f;
    bool latchedToggleOn = false;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        origFixedDT = Time.fixedDeltaTime;
    }

    void Start()
    {
        // Tentukan IsUnlocked awal
#if UNITY_EDITOR
        if (unlockedByDefaultInEditor && !forceLockedOnStart)
        {
            IsUnlocked = true;
            PlayerPrefs.SetInt(prefsUnlockedKey, 1);
            PlayerPrefs.Save();
        }
        else
        {
            IsUnlocked = PlayerPrefs.GetInt(prefsUnlockedKey, 0) == 1;
        }
#else
        IsUnlocked = PlayerPrefs.GetInt(prefsUnlockedKey, 0) == 1;
#endif
        // Paksa locked jika diminta (Stage 1)
        if (forceLockedOnStart)
        {
            IsUnlocked = false;
            PlayerPrefs.SetInt(prefsUnlockedKey, 0);
            PlayerPrefs.Save();
        }

        SetActive(false, playSfx: false, raiseEvent: false);
        OnUnlockedChanged?.Invoke(IsUnlocked);
    }

    void OnDisable()
    {
        if (IsActive) SetActive(false);
    }

    void Update()
    {
        if (ShouldForceOff())
        {
            if (IsActive) SetActive(false);
            return;
        }

        // ⬇︎ ganti cek lama:
        // if (!IsUnlocked)
        //   { if (IsActive || Time.timeScale != 1f) SetActive(false); return; }

        if (!CanFastForward())        // ← gunakan helper baru
        {
            if (IsActive || Time.timeScale != 1f) SetActive(false);
            return;
        }

        // INPUT: toggle Space
        if (allowSpacebarToggle && Input.GetKeyDown(KeyCode.Space))
        {
            latchedToggleOn = !latchedToggleOn;
            SetActive(latchedToggleOn);
        }

        // INPUT: hold Right Mouse
        if (allowRightMouseHold)
        {
            if (Input.GetMouseButton(1)) SetActive(true);
            else if (!latchedToggleOn) SetActive(false);
        }
    }


    bool ShouldForceOff()
    {
        var gm = GameManager.Instance;
        if (gm == null) return true;
        if (gm.IsFlipping) return true;
        if (gm.State == GameState.FeverFinalTurn) return true;
        if (gm.State == GameState.EndLevelSuccess) return true;
        if (!gm.HasBalls) return true;
        return false;
    }

    // ==== API untuk Stage Manager ====
    public void UnlockFromUI()
    {
        if (IsUnlocked) return;
        IsUnlocked = true;
        PlayerPrefs.SetInt(prefsUnlockedKey, 1);
        PlayerPrefs.Save();
        OnUnlockedChanged?.Invoke(IsUnlocked);
    }

    public void LockFromUI()
    {
        if (!IsUnlocked) return;
        IsUnlocked = false;
        PlayerPrefs.SetInt(prefsUnlockedKey, 0);
        PlayerPrefs.Save();
        latchedToggleOn = false;
        SetActive(false);
        OnUnlockedChanged?.Invoke(IsUnlocked);
    }

    // ==== Inti: hanya controller yang boleh ubah timescale ====
    void SetActive(bool on, bool playSfx = true, bool raiseEvent = true)
    {
        if (IsActive == on) return;
        IsActive = on;

        if (IsActive)
        {
            Time.timeScale = ffScale;
            Time.fixedDeltaTime = origFixedDT * ffScale;
            if (playSfx && !string.IsNullOrEmpty(sfxToggleOnKey)) AudioManager.I.PlayUI(sfxToggleOnKey);
        }
        else
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = origFixedDT;
            if (playSfx && !string.IsNullOrEmpty(sfxToggleOffKey)) AudioManager.I.PlayUI(sfxToggleOffKey);
        }

        if (raiseEvent) OnActiveChanged?.Invoke(IsActive);
    }

    // ==== Helper global untuk validasi ====
    public bool CanFastForward()
    {
        // Integrasi langsung dengan SaveManager bila ada
        if (SaveManager.I != null)
        {
            var data = SaveManager.I.Data;
            if (data.fastForwardForceLocked) return false;
            if (!data.fastForwardUnlocked) return false;
        }

        return IsUnlocked && !forceLockedOnStart;
    }
}
