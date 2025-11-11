using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using DG.Tweening;

/// <summary>
/// Pengendali utama Main Menu.
/// </summary>
public class MenuManager : MonoBehaviour
{
    // ====== PANELS ======
    [Header("Panels (drag di Inspector)")]
    [SerializeField] private GameObject panelMain;        // Kolom tombol utama
    [SerializeField] private GameObject panelAdventure;   // Dialog Adventure
    [SerializeField] private GameObject panelSettings;
    [SerializeField] private GameObject panelAbout;
    [SerializeField] private GameObject panelCredits;
    [SerializeField] private GameObject panelExit;
    [SerializeField] public NameEntryUI panelName;       // Input 3 huruf

    // ====== AUDIO ======
    [Header("Audio")]
    [SerializeField] private string uiClickSfxKey = "Plop";          // key SFX klik di AudioManager
    [SerializeField] private string menuMusicKey = "Music_Inventory"; // key BGM menu (ganti kalau perlu)
    [SerializeField] private bool playMenuMusicOnAwake = true;

    [Header("Global Dim (sibling under Canvas)")]
    [SerializeField] private GameObject dim;              // Image fullscreen
    private CanvasGroup dimGroup;

    [Header("Dialog behavior")]
    [SerializeField] private bool hideMainBehindDialog = false;
    [SerializeField, Range(0f, 1f)] private float mainFadeWhenDim = 0.35f;

    // ====== ADVENTURE UI ======
    [Header("Adventure UI")]
    [Tooltip("Opsional, jika pakai teks judul")]
    [SerializeField] private TMP_Text titleAdventure;

    [Tooltip("GameObject judul sprite untuk 'No Save Found'")]
    [SerializeField] private GameObject goNoSaveTitle;
    [Tooltip("GameObject judul sprite untuk 'Save Found'")]
    [SerializeField] private GameObject goSaveTitle;

    [Tooltip("Tombol Load/Continue (akan di-disable kalau tak ada save)")]
    [SerializeField] private Button btnContinue;
    [Tooltip("CanvasGroup tombol Load untuk atur alpha & raycast")]
    [SerializeField] private CanvasGroup btnContinueGroup;

    [Header("Load Scene")]
    [SerializeField] private string firstLevelScene = "Test";
    [SerializeField] private string characterSelectScene = "CharacterSelection";

    // ====== Inline Overwrite Warning (bukan panel) ======
    [Header("Adventure > Inline Overwrite Warning")]
    [SerializeField] private GameObject areYouSureText;           // drag GO teks “Are you sure?”
    [SerializeField] private RectTransform areYouSureShakeTarget; // opsional; kalau kosong, ambil dari areYouSureText
    [SerializeField] private bool requireDoubleNewGame = true;    // klik 2x untuk konfirmasi
    [SerializeField, Min(0.5f)] private float armResetSeconds = 3f;
    [SerializeField] private bool useDotweenShake = true;
    [SerializeField] private float shakeDuration = 0.25f;
    [SerializeField] private float shakeStrength = 12f;

    // ====== Runtime ======
    private CanvasGroup mainCanvasGroup;
    private GameObject currentDialog;
    bool newGameArmed = false;
    Coroutine armResetCo;

    void Start()
    {
        if (playMenuMusicOnAwake)
            StartCoroutine(CoPlayMenuMusicWhenReady());
    }
    void Awake()
    {
        // Matikan semua dialog saat start
        SafeSetActive(panelAdventure, false);
        SafeSetActive(panelSettings, false);
        SafeSetActive(panelAbout, false);
        SafeSetActive(panelCredits, false);
        SafeSetActive(panelExit, false);
        if (panelName) panelName.HideImmediate();

        // Inline warning disembunyikan
        SafeSetActive(areYouSureText, false);
        newGameArmed = false;

        // Siapkan dim
        if (dim != null)
        {
            dimGroup = dim.GetComponent<CanvasGroup>();
            if (!dimGroup) dimGroup = dim.AddComponent<CanvasGroup>();
            dimGroup.alpha = 0f;
            dimGroup.interactable = false;
            dimGroup.blocksRaycasts = false;
            dim.SetActive(false);
        }

        // Main group
        if (panelMain != null)
        {
            mainCanvasGroup = panelMain.GetComponent<CanvasGroup>();
            if (!mainCanvasGroup) mainCanvasGroup = panelMain.AddComponent<CanvasGroup>();
            mainCanvasGroup.alpha = 1f;
            mainCanvasGroup.interactable = true;
            mainCanvasGroup.blocksRaycasts = true;
        }

        RefreshAdventureUI();

        // link balik supaya NameEntry bisa panggil StartNewGameAfterName
        if (panelName) panelName.menu = this;

        // Putar BGM menu saat scene dibuka
        if (playMenuMusicOnAwake && AudioManager.I != null)
        {
            AudioManager.I.PlayMusic(menuMusicKey);
        }

        if (SaveManager.I != null)
            SaveManager.I.TryFlushPendingLeaderboard();
    }

    // ===== Helpers =====
    private void SafeSetActive(GameObject go, bool v)
    {
        if (go != null && go.activeSelf != v) go.SetActive(v);
    }

    IEnumerator FadeCanvas(CanvasGroup g, float a, float b, float t, System.Action done = null)
    {
        if (!g) { done?.Invoke(); yield break; }
        float time = 0f;
        g.alpha = a;
        while (time < t)
        {
            time += Time.unscaledDeltaTime;
            g.alpha = Mathf.Lerp(a, b, time / t);
            yield return null;
        }
        g.alpha = b;
        done?.Invoke();
    }

    // ==== Dim control ====
    public void ShowDim()
    {
        if (!dimGroup) { SafeSetActive(dim, true); return; }
        dim.SetActive(true);
        dimGroup.blocksRaycasts = true;
        dimGroup.interactable = true;
        StopCoroutine(nameof(FadeCanvas));
        StartCoroutine(FadeCanvas(dimGroup, dimGroup.alpha, 0.6f, 0.12f));
        if (!hideMainBehindDialog && mainCanvasGroup) mainCanvasGroup.alpha = Mathf.Clamp01(mainFadeWhenDim);
        if (hideMainBehindDialog) SafeSetActive(panelMain, false);
    }

    public void HideDim()
    {
        if (!dimGroup) { SafeSetActive(dim, false); return; }
        dimGroup.blocksRaycasts = false;
        dimGroup.interactable = false;
        StopCoroutine(nameof(FadeCanvas));
        StartCoroutine(FadeCanvas(dimGroup, dimGroup.alpha, 0f, 0.12f, () => dim.SetActive(false)));
        if (!hideMainBehindDialog && mainCanvasGroup) mainCanvasGroup.alpha = 1f;
        if (hideMainBehindDialog) SafeSetActive(panelMain, true);
    }

    // ===== Dialog generic =====
    private void ShowDialog(GameObject dialog)
    {
        if (dialog == null) return;

        if (currentDialog != null && currentDialog != dialog)
        {
            var pfPrev = currentDialog.GetComponent<PanelFader>();
            if (pfPrev) pfPrev.Hide();
            else currentDialog.SetActive(false);
        }

        currentDialog = dialog;

        var pf = dialog.GetComponent<PanelFader>();
        if (pf) pf.Show();
        else dialog.SetActive(true);

        ShowDim();
    }

    public void HideDialog()
    {
        PlayClick();

        if (currentDialog != null)
        {
            var go = currentDialog;
            var pf = go.GetComponent<PanelFader>();
            currentDialog = null;

            if (pf != null && go.activeInHierarchy)
            {
                pf.Hide(() => { HideDim(); });
            }
            else
            {
                HideDim();
            }
        }
        else
        {
            HideDim();
        }
    }

    // ===== Adventure =====
    public void OnClickAdventure()
    {
        PlayClick();
        RefreshAdventureUI();
        DisarmNewGameWarning();   // pastikan warning hilang ketika panel dibuka
        ShowDialog(panelAdventure);
    }

    private void RefreshAdventureUI()
    {
        bool hasSave = (SaveManager.I != null) && SaveManager.I.HasSave;

        // 1) Judul berbasis sprite (jika di-assign)
        SafeSetActive(goNoSaveTitle, !hasSave);
        SafeSetActive(goSaveTitle, hasSave);

        // 2) Fallback teks (kalau mau tetap pakai TMP_Text)
        if (titleAdventure) titleAdventure.text = hasSave ? "Save Found" : "No Save Found";

        // 3) State tombol Load/Continue
        if (btnContinue) btnContinue.interactable = hasSave;
        if (btnContinueGroup)
        {
            btnContinueGroup.alpha = hasSave ? 1f : 0.5f;
            btnContinueGroup.interactable = hasSave;
            btnContinueGroup.blocksRaycasts = hasSave;
        }
    }

    /// <summary>
    /// Klik "New Game":
    /// - Jika TIDAK ada save -> langsung buka Name Entry.
    /// - Jika ADA save       -> klik 1x: tampil teks "Are you sure?" (shake). Klik 2x (sebelum timeout): buka Name Entry.
    /// </summary>
    public void NewGame()
    {
        PlayClick();

        bool hasSave = (SaveManager.I != null) && SaveManager.I.HasSave;

        if (hasSave && requireDoubleNewGame)
        {
            if (!newGameArmed)
            {
                ArmNewGameWarning();   // tampilkan teks + shake, tetap di Adventure
                return;
            }
            // sudah armed (klik kedua) -> lanjut
            DisarmNewGameWarning();
        }
        else
        {
            // tidak butuh double confirm -> pastikan teks tersembunyi
            DisarmNewGameWarning();
        }

        // Buka Name Entry
        OpenNameEntryFromAdventure();
    }

    /// <summary>Dipanggil NameEntryUI saat Confirm (ini yang benar2 overwrite save).</summary>
    public void StartNewGameAfterName(string sceneOverride = "", bool playSfx = true)
    {
        if (playSfx) PlayClick();

        // stop musik menu biar tidak tumpang tindih
        if (AudioManager.I) AudioManager.I.StopMusic();

        string scene = string.IsNullOrEmpty(sceneOverride) ? characterSelectScene : sceneOverride;
        PlayerPrefs.Save();
        if (!string.IsNullOrEmpty(scene))
            SceneTransition.LoadScene(scene);
        else
            Debug.LogWarning("[Menu] firstLevelScene belum diisi.");
    }

    public void ContinueGame()
    {
        if (SaveManager.I == null || !SaveManager.I.HasSave)   // ← tanpa ()
        {
            Debug.LogWarning("[Menu] Continue: no save, fallback to NewGame");
            NewGame();
            return;
        }

        // Ambil level target dari save, clamp aman
        int maxIdx = (LevelManager.Instance != null && LevelManager.Instance.levelScenes != null)
            ? LevelManager.Instance.levelScenes.Length - 1
            : 0;

        int target = Mathf.Clamp(SaveManager.I.Data.levelIndex, 0, Mathf.Max(0, maxIdx));
        Debug.Log($"[Menu] Continue → Load level index {target}");

        // Pastikan musik overlay/main dimatikan agar tidak overlap
        AudioManager.I?.StopOverlayMusic();
        AudioManager.I?.StopMusic();

        // Langsung load level target — TANPA lewat firstLevelScene
        LevelManager.Instance?.LoadLevelIndex(target);
    }



    // ===== Name Entry flow =====
    private void OpenNameEntryFromAdventure()
    {
        if (panelName != null)
        {
            panelName.menu = this;
            ShowDim();

            if (panelAdventure != null && currentDialog == panelAdventure)
            {
                var pfAdv = panelAdventure.GetComponent<PanelFader>();
                System.Action openName = () =>
                {
                    currentDialog = panelName.gameObject;
                    panelName.Show();
                };

                if (pfAdv) pfAdv.Hide(openName);
                else
                {
                    panelAdventure.SetActive(false);
                    openName();
                }
            }
            else
            {
                currentDialog = panelName.gameObject;
                panelName.Show();
            }
            return;
        }

        // Fallback (kalau tidak pakai NameEntry)
        if (!string.IsNullOrEmpty(firstLevelScene))
            SceneTransition.LoadScene(firstLevelScene);
        else
            Debug.LogWarning("[Menu] firstLevelScene belum diisi.");
    }

    // ===== Inline "Are you sure?" helpers =====
    void ArmNewGameWarning()
    {
        newGameArmed = true;
        SafeSetActive(areYouSureText, true);
        TryShakeInlineWarning();

        if (armResetCo != null) StopCoroutine(armResetCo);
        armResetCo = StartCoroutine(AutoDisarmAfter(armResetSeconds));
    }

    void DisarmNewGameWarning()
    {
        newGameArmed = false;
        if (armResetCo != null) { StopCoroutine(armResetCo); armResetCo = null; }
        SafeSetActive(areYouSureText, false);
    }

    IEnumerator AutoDisarmAfter(float secs)
    {
        yield return new WaitForSecondsRealtime(secs);
        DisarmNewGameWarning();
    }

    void TryShakeInlineWarning()
    {
        var target = areYouSureShakeTarget ? areYouSureShakeTarget
                   : (areYouSureText ? areYouSureText.GetComponent<RectTransform>() : null);
        if (!target) return;

#if DOTWEEN || DOTWEEN_ENABLED
        if (useDotweenShake)
        {
            var start = target.anchoredPosition;
            DOTween.Kill(target);
            target.anchoredPosition = start;
            target.DOShakeAnchorPos(shakeDuration, shakeStrength, vibrato: 10, randomness: 90, snapping: false, fadeOut: true)
                  .OnComplete(() => target.anchoredPosition = start);
            return;
        }
#endif
        StartCoroutine(ShakeCoroutine(target, shakeDuration, shakeStrength));
    }

    IEnumerator ShakeCoroutine(RectTransform rt, float dur, float strength)
    {
        if (!rt) yield break;
        Vector2 start = rt.anchoredPosition;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.PerlinNoise(t * 25f, 0f) * 2f - 1f;
            rt.anchoredPosition = start + new Vector2(n * strength, 0f);
            yield return null;
        }
        rt.anchoredPosition = start;
    }

    void PlayClick()
    {
        if (AudioManager.I != null && !string.IsNullOrEmpty(uiClickSfxKey))
            AudioManager.I.PlayUI(uiClickSfxKey);
    }

    // ===== About / Settings / Credits / Exit =====
    public void OnClickAbout()
    {
        PlayClick();
        ShowDialog(panelAbout);
    }

    public void OnClickSettings()
    {
        PlayClick();
        ShowDialog(panelSettings);
    }

    public void OnClickCredits()
    {
        PlayClick();
        ShowDialog(panelCredits);
    }

    public void OnClickExit()
    {
        PlayClick();
        if (panelExit != null) ShowDialog(panelExit);
        else QuitGame();
    }

    System.Collections.IEnumerator CoPlayMenuMusicWhenReady()
    {
        // tunggu 1 frame supaya AudioManager.Awake() selesai
        yield return null;

        // kalau masih belum ada, tunggu sampai ada (maks 1 detik biar aman)
        float t = 0f;
        while (AudioManager.I == null && t < 1f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (AudioManager.I == null) yield break;

        // hanya putar kalau belum ada musik main menu yang jalan
        if (!AudioManager.I.IsMusicPlaying)
            AudioManager.I.PlayMusic(menuMusicKey); // key di Inspector: "Music_MainMenu"
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

#if UNITY_EDITOR
    private void OnValidate() { mainFadeWhenDim = Mathf.Clamp01(mainFadeWhenDim); }
#endif
}
