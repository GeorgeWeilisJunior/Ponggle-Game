using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Linq;

public class PauseManager : MonoBehaviour
{
    public static PauseManager I { get; private set; }

    [Header("UI Roots")]
    [SerializeField] GameObject pauseCanvas;         // root PauseCanvas (dipakai utk aktif/nonaktif awal)
    [SerializeField] TMP_Text titleText;             // Pause title
    [SerializeField] TMP_Text dialogueText;          // Tips text
    [SerializeField] Button btnContinue;             // Pause → Continue
    [SerializeField] Button btnSettings;             // Pause → Settings
    [SerializeField] Button btnMainMenu;             // Pause → Main Menu

    [Header("Panels")]
    [SerializeField] PanelFader pausePanel;          // drag: PauseCanvas/Panel_Pause (punya CanvasGroup)
    [SerializeField] PanelFader settingsPanel;       // drag: PauseCanvas/Panel_Setting (punya CanvasGroup)
    [SerializeField] SettingsPanel settings;         // komponen SettingsPanel pada Panel_Setting

    [Header("Options")]
    [SerializeField] KeyCode toggleKey = KeyCode.Escape;
    [SerializeField] bool pauseAudio = true;
    [SerializeField] string sfxOpenKey = "";         // play dgn ignorePause:true
    [SerializeField] string sfxCloseKey = "";
    [SerializeField] string sfxSettingsOpen = "UI_SettingOpen";   // <— BARU
    [SerializeField] string sfxSettingsClose = "UI_SettingClose"; // <— BARU
    [SerializeField] string mainMenuScene = "Main Menu";

    [SerializeField] GameObject pauseCanvasRoot;     // drag: Canvas/PauseCanvas (atau root panel pause kamu)
    [SerializeField] CanvasGroup pauseCanvasGroup;   // (opsional) add CanvasGroup di PauseCanvas & drag ke sini

    [Header("Tips per Karakter")]
    [SerializeField] List<CharacterTips> tipsDb = new List<CharacterTips>();
    [SerializeField, TextArea(2, 6)]
    string[] defaultTips = new string[]
    {
        "Gunakan dinding bucket untuk bantu arahin bola ke lubang.",
        "Bidik peg hijau sejak awal untuk mengaktifkan power.",
        "Pantulkan dari dinding untuk sudut yang lebih landai."
    };

    [Header("Typewriter")]
    [SerializeField] bool useTypewriter = true;
    [SerializeField, Range(5f, 120f)] float charsPerSecond = 40f;
    [SerializeField, Range(0f, 0.5f)] float punctuationDelay = 0.12f;
    [SerializeField] bool allowSkipTyping = true;

    // di atas / dekat titleText
    [SerializeField] TMP_Text levelBadge;
    [SerializeField, Range(0f, 0.5f)] float inputUnfreezeDelay = 0.12f; // delay buka raycast

    public bool IsPaused { get; private set; }
    float prevTimeScale = 1f;

    // runtime
    Coroutine typingCo;
    string lastTipShown = null;
    bool isTyping = false;

    [System.Serializable]
    public class CharacterTips
    {
        public string name = "Character";
        public string[] characterKeys;
        [TextArea(2, 6)] public string[] tips;
    }

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;

        if (pauseCanvas) pauseCanvas.SetActive(false);
        if (pausePanel) pausePanel.gameObject.SetActive(false);
        if (settingsPanel) settingsPanel.gameObject.SetActive(false);
    }

    void Start()
    {
        if (btnContinue) btnContinue.onClick.AddListener(Resume);
        if (btnSettings) btnSettings.onClick.AddListener(OpenSettingsFromPause);
        if (btnMainMenu) btnMainMenu.onClick.AddListener(OnClickMainMenu);

        // Saat tombol Close (X) di Settings ditekan → kembali ke Pause
        if (settings)
        {
            settings.onRequestClose.RemoveAllListeners();
            settings.onRequestClose.AddListener(CloseSettingsToPause);
        }
    }

    void Update()
    {
        // Toggle
        if (Input.GetKeyDown(toggleKey))
        {
            if (IsPaused) Resume();
            else Pause();
        }

        // Skip typewriter
        if (IsPaused && allowSkipTyping && isTyping &&
            (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0)))
        {
            SkipTyping();
        }
    }

    public void TogglePause()
    {
        if (IsPaused) Resume();
        else Pause();
    }
    string ComposeLevelLabel()
    {
        var sm = SaveManager.I;
        int idx = (sm != null) ? Mathf.Clamp(sm.Data.levelIndex, 0, SaveManager.TOTAL_LEVELS - 1) : 0;
        int stage = (idx / 5) + 1;
        int within = (idx % 5) + 1;
        return $"{stage}-{within}";
    }
    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;

        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (!string.IsNullOrEmpty(sfxOpenKey))
            AudioManager.I?.PlayUI(sfxOpenKey, ignorePause: true);

        if (pauseAudio) AudioListener.pause = true;
        StartCoroutine(OpenPauseRoutine());

        if (pauseCanvas) pauseCanvas.SetActive(true);
        if (settingsPanel) settingsPanel.Hide();
        if (pausePanel) { pausePanel.gameObject.SetActive(true); pausePanel.Show(); }

        // ⬇️ set level badge
        if (levelBadge) levelBadge.text = ComposeLevelLabel();

        string tip = ComposeTipForCurrentCharacter();
        ShowDialogue(tip);
    }

    IEnumerator OpenPauseRoutine()
    {
        // Matikan interaksi dulu
        if (pauseCanvasGroup)
        {
            pauseCanvasGroup.interactable = false;
            pauseCanvasGroup.blocksRaycasts = false;
            pauseCanvasGroup.alpha = 1f; // pastikan terlihat bila pakai CanvasGroup
        }

        if (pauseCanvas) pauseCanvas.SetActive(true);
        if (settingsPanel) settingsPanel.Hide();

        // Tampilkan panel pause
        if (pausePanel)
        {
            pausePanel.gameObject.SetActive(true);
            pausePanel.Show(); // anim kamu sendiri OK
        }

        // Set level badge + tip sekarang supaya sudah siap.
        if (levelBadge) levelBadge.text = ComposeLevelLabel();
        string tip = ComposeTipForCurrentCharacter();
        ShowDialogue(tip);

        // Tunggu beberapa frame unscaled supaya layout/anim settle
        yield return null;
        yield return new WaitForSecondsRealtime(inputUnfreezeDelay);

        // Force “pointer exit” agar state hover bersih
        var es = EventSystem.current;
        if (es != null)
        {
            es.SetSelectedGameObject(null);
            var ev = new PointerEventData(es) { position = new Vector2(-9999, -9999) };
            if (btnContinue) ExecuteEvents.Execute(btnContinue.gameObject, ev, ExecuteEvents.pointerExitHandler);
            if (btnSettings) ExecuteEvents.Execute(btnSettings.gameObject, ev, ExecuteEvents.pointerExitHandler);
            if (btnMainMenu) ExecuteEvents.Execute(btnMainMenu.gameObject, ev, ExecuteEvents.pointerExitHandler);
        }

        // Baru buka interaksi
        if (pauseCanvasGroup)
        {
            pauseCanvasGroup.blocksRaycasts = true;
            pauseCanvasGroup.interactable = true;
        }
    }

    public void Resume()
    {
        if (!IsPaused) return;

        if (!string.IsNullOrEmpty(sfxCloseKey))
            AudioManager.I?.PlayUI(sfxCloseKey, ignorePause: true);

        StopTypingAndShowFull();

        if (pausePanel) pausePanel.Hide();
        if (settingsPanel) settingsPanel.Hide();

        if (pauseAudio) AudioListener.pause = false;
        Time.timeScale = (prevTimeScale <= 0f) ? 1f : prevTimeScale;

        if (pauseCanvas) pauseCanvas.SetActive(false);

        IsPaused = false;
    }

    /*================= Alur Pause ⇄ Settings =================*/

    // Klik "SETTING" di Pause → TUTUP panel Pause, game TETAP paused, buka Settings
    public void OpenSettingsFromPause()
    {
        // SFX
        if (!string.IsNullOrEmpty(sfxSettingsOpen))
            AudioManager.I?.PlayUI(sfxSettingsOpen, ignorePause: true);

        // Tetap pause
        IsPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        // Tutup panel pause, buka panel setting
        if (pausePanel) pausePanel.Hide();
        if (settingsPanel)
        {
            settingsPanel.gameObject.SetActive(true);
            settingsPanel.Show();
        }

        // Pastikan root canvas hidup
        if (pauseCanvas) pauseCanvas.SetActive(true);
    }

    // Klik Close (X) di Settings → tutup Settings, MASIH paused, balik ke panel Pause
    public void CloseSettingsToPause()
    {
        if (!string.IsNullOrEmpty(sfxSettingsClose))
            AudioManager.I?.PlayUI(sfxSettingsClose, ignorePause: true);

        // Tetap pause
        IsPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        // Tutup settings, buka pause
        if (settingsPanel) settingsPanel.Hide();
        if (pauseCanvas) pauseCanvas.SetActive(true);
        if (pausePanel)
        {
            pausePanel.gameObject.SetActive(true);
            pausePanel.Show();
        }
    }


    /*================= Main Menu =================*/
    void OnClickMainMenu()
    {
        BackToMainMenu();
    }

    void OnDestroy()
    {
        if (IsPaused)
        {
            AudioListener.pause = false;
            Time.timeScale = 1f;
        }
    }

    void HidePauseInstant()
    {
        // nonaktifkan interaksi & visual (aman terhadap 1-frame delay transisi)
        if (pauseCanvasGroup)
        {
            pauseCanvasGroup.alpha = 0f;
            pauseCanvasGroup.interactable = false;
            pauseCanvasGroup.blocksRaycasts = false;
        }
        if (pauseCanvasRoot) pauseCanvasRoot.SetActive(false);
    }

    void BackToMainMenu()
    {
        // pastikan game tidak pause saat pindah scene
        IsPaused = false;
        AudioListener.pause = false;
        Time.timeScale = 1f;

        HidePauseInstant();
        // simpan progress & settings
        try { SaveManager.I?.SaveToDisk(); } catch { /* ignore */ }
        try { PlayerPrefs.Save(); } catch { /* ignore */ }

        // hentikan musik gameplay biar tidak tumpang tindih
        AudioManager.I?.StopMusic();

        // pindah ke Main Menu
        if (!string.IsNullOrEmpty(mainMenuScene))
            SceneTransition.LoadScene(mainMenuScene);
        else
            SceneManager.LoadScene("Main Menu"); // fallback kalau kamu belum pakai SceneTransition
    }

    /*================= Tip & Typewriter (tetap sama) =================*/
    string ComposeTipForCurrentCharacter()
    {
        string charName = null;
        var cpm = CharacterPowerManager.Instance;
        var ch = cpm ? cpm.GetCurrentCharacter() : null;
        if (ch != null && !string.IsNullOrEmpty(ch.characterName))
            charName = ch.characterName.ToLowerInvariant();

        if (!string.IsNullOrEmpty(charName))
        {
            foreach (var set in tipsDb)
            {
                if (set == null || set.tips == null || set.tips.Length == 0) continue;
                if (set.characterKeys == null || set.characterKeys.Length == 0) continue;
                foreach (var key in set.characterKeys)
                {
                    if (string.IsNullOrEmpty(key)) continue;
                    if (charName.Contains(key.ToLowerInvariant()))
                        return PickRandomTip(set.tips);
                }
            }
        }
        return PickRandomTip(defaultTips);
    }

    string PickRandomTip(string[] pool)
    {
        if (pool == null || pool.Length == 0) return "Game paused.";
        if (pool.Length == 1) { lastTipShown = pool[0]; return pool[0]; }
        string pick; int guard = 0;
        do { pick = pool[Random.Range(0, pool.Length)]; guard++; }
        while (pick == lastTipShown && guard < 8);
        lastTipShown = pick;
        return pick;
    }

    void ShowDialogue(string text)
    {
        if (!dialogueText) return;
        StopTyping();
        dialogueText.text = text;

        if (!useTypewriter)
        {
            dialogueText.maxVisibleCharacters = text.Length;
            isTyping = false;
            return;
        }
        typingCo = StartCoroutine(TypeRoutine(text));
    }

    IEnumerator TypeRoutine(string fullText)
    {
        isTyping = true;
        dialogueText.maxVisibleCharacters = 0;

        int visible = 0;
        while (visible < fullText.Length)
        {
            visible++;
            dialogueText.maxVisibleCharacters = visible;

            float delay = 1f / Mathf.Max(1f, charsPerSecond);
            char c = fullText[Mathf.Clamp(visible - 1, 0, fullText.Length - 1)];
            if (c == '.' || c == ',' || c == '!' || c == '?' || c == ';' || c == ':')
                delay += punctuationDelay;

            float t = 0f;
            while (t < delay)
            {
                yield return null;
                t += Time.unscaledDeltaTime; // tetap jalan saat paused
                if (!isTyping) yield break;
            }
        }
        isTyping = false;
        typingCo = null;
    }

    void SkipTyping()
    {
        if (!dialogueText || !isTyping) return;
        dialogueText.maxVisibleCharacters = dialogueText.text.Length;
        StopTyping();
    }

    void StopTyping()
    {
        if (typingCo != null)
        {
            StopCoroutine(typingCo);
            typingCo = null;
        }
        isTyping = false;
    }

    void StopTypingAndShowFull()
    {
        if (!dialogueText) return;
        if (isTyping) SkipTyping();
        dialogueText.maxVisibleCharacters = dialogueText.text.Length;
    }

    // API kecil
    public void SetDialogue(string text, bool playTypewriter = false)
    {
        if (!dialogueText) return;
        useTypewriter = playTypewriter;
        ShowDialogue(text);
    }
}
