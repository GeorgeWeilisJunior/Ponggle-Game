using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ElementTutorialUI : MonoBehaviour
{
    [Header("Audio (SFX keys)")]
    [SerializeField] private string sfxOpenKey = "UI_Open";
    [SerializeField] private string sfxBackKey = "UI_Back";

    [Header("Overlay Music (optional)")]
    [SerializeField] private string overlayMusicKey = "Music_ElementTutorial";
    [SerializeField] private bool pauseGameplayMusic = true;

    [Header("Behaviour")]
    [SerializeField] private bool closeOnEscape = true;
    [Tooltip("Jika true, setiap kali overlay dibuka semua entry (Steam, Firestorm, dst) direset tertutup.")]
    [SerializeField] private bool resetEntriesClosedOnOpen = true;

    [Header("Layout")]
    [Tooltip("Container list entry: .../RightPane/Scroll View/Viewport/Content")]
    [SerializeField] private RectTransform listContent;
    [SerializeField] private Button btnBack;   // drag kalau mau manual


    Button backBtn;
    float prevTimeScale = 1f;
    bool inputLocked;

    void Awake()
    {
        // Ambil back button kalau belum di-assign
        if (!btnBack) btnBack = GetComponentInChildren<Button>(true);

        // Pastikan klik Back memanggil Close() -> akan memutar sfxBackKey
        if (btnBack)
        {
            btnBack.onClick.RemoveListener(Close);
            btnBack.onClick.AddListener(Close);
        }
    }

    void OnEnable()
    {
        // pastikan overlay di atas UI lain
        transform.SetAsLastSibling();

        // pause gameplay (simpan timescale sebelumnya bila > 0)
        prevTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
        Time.timeScale = 0f;

        // lock input gameplay (aman jika Launcher.Instance null)
        Launcher.Instance?.LockInput();
        inputLocked = true;

        // SFX open
        if (!string.IsNullOrEmpty(sfxOpenKey))
            AudioManager.I?.PlayUI(sfxOpenKey, ignorePause: true);

        // overlay music
        if (pauseGameplayMusic)
        {
            AudioManager.I?.PauseMusic();
            if (!string.IsNullOrEmpty(overlayMusicKey))
                AudioManager.I?.PlayOverlayMusic(overlayMusicKey, loop: true);
        }

        // reset semua entry ke tertutup
        if (resetEntriesClosedOnOpen)
            ResetAllEntriesClosed();

        // rebuild layout setelah aktif (biar layout shrink/expand rapi)
        StartCoroutine(RebuildAfterOpen());

        // fokuskan ke tombol back (kalau ada) agar navigasi gamepad/keyboard enak
        if (backBtn) EventSystem.current?.SetSelectedGameObject(backBtn.gameObject);
    }

    void Update()
    {
        if (!closeOnEscape) return;
        if (Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    void OnDisable()
    {
        // stop overlay music & kembalikan BGM
        if (pauseGameplayMusic)
        {
            AudioManager.I?.StopOverlayMusic();
            AudioManager.I?.ResumeMusic();
        }

        // unlock input gameplay
        if (inputLocked)
        {
            Launcher.Instance?.UnlockInput();
            inputLocked = false;
        }

        // restore timescale
        Time.timeScale = prevTimeScale <= 0f ? 1f : prevTimeScale;
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);
    public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);

    public void Close()
    {
        if (!string.IsNullOrEmpty(sfxBackKey))
            AudioManager.I?.PlayUI(sfxBackKey, ignorePause: true);

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Paksa semua entry di panel kanan menjadi tertutup (instan, tanpa animasi).
    /// Panggil saat overlay dibuka atau dari tombol "Close All".
    /// </summary>
    public void CloseAllEntries()
    {
        ResetAllEntriesClosed();
        // rebuild supaya tinggi list langsung mengecil
        if (listContent) LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// Tutup instan semua ReactionEntryUI di bawah listContent/overlay.
    /// </summary>
    void ResetAllEntriesClosed()
    {
        RectTransform root = listContent ? listContent : (RectTransform)transform;
        var entries = root.GetComponentsInChildren<ReactionEntryUI>(true);
        foreach (var e in entries)
            e.ForceSetOpenInstant(false);
    }

    IEnumerator RebuildAfterOpen()
    {
        // beri waktu 1–2 frame agar layout components siap
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        if (listContent)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
            Canvas.ForceUpdateCanvases();
        }
    }
}
