using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.Events;

public class SettingsPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider music;
    [SerializeField] private Slider sfx;
    [SerializeField] private Slider brightness;
    [SerializeField] private Slider guideLength;      // 0..1
    [SerializeField] private Toggle disableEffects;
    [SerializeField] private Toggle easyMode;
    [SerializeField] private Button btnClose;

    [Header("Wiring")]
    [SerializeField] private Image brightnessOverlay; // overlay gelap utk “brightness”
    [SerializeField] private MenuManager menu;        // optional: hanya dipakai di Main Menu

    [Header("Audio (opsional)")]
    [SerializeField] private AudioMixer audioMixer;   // boleh kosong
    [SerializeField] private string musicParam = "MusicVol";
    [SerializeField] private string sfxParam = "SfxVol";

    [Header("Events")]
    public UnityEvent onRequestClose;                 // ← dipakai gameplay (PauseManager)

    // PlayerPrefs keys
    const string K_MUSIC = "set_music";
    const string K_SFX = "set_sfx";
    const string K_BR = "set_brightness";
    const string K_GUIDE = "set_guidelen";
    const string K_EFXOFF = "set_disablefx";
    const string K_EASY = "set_easymode";

    /*===================== Unity =====================*/

    void Awake()
    {
        if (btnClose)
        {
            btnClose.onClick.RemoveAllListeners();
            btnClose.onClick.AddListener(() =>
            {
                if (menu != null) { menu.HideDialog(); return; } // Main Menu
                onRequestClose?.Invoke();                        // Gameplay
            });
        }
    }

    void OnEnable()
    {
        Load();
        ApplyAll();
        Hook();
    }

    void OnDisable()
    {
        Unhook();
        Save();
    }

    /*=================== Hooking =====================*/

    void Hook()
    {
        if (music) music.onValueChanged.AddListener(OnMusic);
        if (sfx) sfx.onValueChanged.AddListener(OnSfx);
        if (brightness) brightness.onValueChanged.AddListener(OnBrightness);
        if (guideLength) guideLength.onValueChanged.AddListener(OnGuideLength);
        if (disableEffects) disableEffects.onValueChanged.AddListener(OnDisableFx);
        if (easyMode) easyMode.onValueChanged.AddListener(OnEasyMode);
    }

    void Unhook()
    {
        if (music) music.onValueChanged.RemoveAllListeners();
        if (sfx) sfx.onValueChanged.RemoveAllListeners();
        if (brightness) brightness.onValueChanged.RemoveAllListeners();
        if (guideLength) guideLength.onValueChanged.RemoveAllListeners();
        if (disableEffects) disableEffects.onValueChanged.RemoveAllListeners();
        if (easyMode) easyMode.onValueChanged.RemoveAllListeners();
    }

    /*================= Load / Save ===================*/

    void Load()
    {
        if (music) music.value = PlayerPrefs.GetFloat(K_MUSIC, 1f);
        if (sfx) sfx.value = PlayerPrefs.GetFloat(K_SFX, 1f);
        if (brightness) brightness.value = PlayerPrefs.GetFloat(K_BR, 1f);
        if (guideLength) guideLength.value = PlayerPrefs.GetFloat(K_GUIDE, 1f);
        if (disableEffects) disableEffects.isOn = PlayerPrefs.GetInt(K_EFXOFF, 0) == 1;
        if (easyMode) easyMode.isOn = PlayerPrefs.GetInt(K_EASY, 0) == 1;
    }

    void Save()
    {
        if (music) PlayerPrefs.SetFloat(K_MUSIC, music.value);
        if (sfx) PlayerPrefs.SetFloat(K_SFX, sfx.value);
        if (brightness) PlayerPrefs.SetFloat(K_BR, brightness.value);
        if (guideLength) PlayerPrefs.SetFloat(K_GUIDE, guideLength.value);
        if (disableEffects) PlayerPrefs.SetInt(K_EFXOFF, disableEffects.isOn ? 1 : 0);
        if (easyMode) PlayerPrefs.SetInt(K_EASY, easyMode.isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    void ApplyAll()
    {
        if (music) OnMusic(music.value);
        if (sfx) OnSfx(sfx.value);
        if (brightness) OnBrightness(brightness.value);
        if (guideLength) OnGuideLength(guideLength.value); // push ke AimGuide juga
        if (disableEffects) OnDisableFx(disableEffects.isOn);
        if (easyMode) OnEasyMode(easyMode.isOn); // supaya GlobalSettings ikut update
    }

    /*=================== Handlers ====================*/

    static float ToDb(float v) => Mathf.Log10(Mathf.Clamp(v, 0.0001f, 1f)) * 20f;

    void OnMusic(float v)
    {
        if (audioMixer) audioMixer.SetFloat(musicParam, ToDb(v));
        AudioManager.I?.SetMusicVolume01(v); // fallback kalau tak pakai mixer
    }

    void OnSfx(float v)
    {
        if (audioMixer) audioMixer.SetFloat(sfxParam, ToDb(v));
        AudioManager.I?.SetSfxVolume01(v);
    }

    void OnBrightness(float v)
    {
        if (!brightnessOverlay) return;
        var c = brightnessOverlay.color;
        // v=1 terang (alpha 0), v=0 gelap (alpha 0.6)
        c.a = Mathf.Lerp(0.6f, 0f, v);
        brightnessOverlay.color = c;
    }

    // === GUIDE LENGTH ===
    void OnGuideLength(float v)
    {
        v = Mathf.Clamp01(v);

        // simpan ke global & prefs
        GlobalSettings.GuideLength = v;
        PlayerPrefs.SetFloat(K_GUIDE, v);
        PlayerPrefs.Save();

        // dorong ke SEMUA AimGuide yang ada (kalau tidak ada, aman)
        var guides = FindObjectsOfType<AimGuide>(true); // true untuk termasuk yg inactive di scene
        for (int i = 0; i < guides.Length; i++)
        {
            // method ini ada di AimGuide.cs yang kamu upload
            guides[i].ApplyGuideLength01(v);
        }
    }

    void OnDisableFx(bool on) => GlobalSettings.DisableEffects = on;

    // simpan segera supaya scene berikutnya pakai nilai terbaru
    public void OnEasyMode(bool on)
    {
        GlobalSettings.EasyMode = on;
        PlayerPrefs.SetInt(K_EASY, on ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"[SettingsPanel] EasyMode={on}");
    }
}

/*====================================================*/

public static class GlobalSettings
{
    public static bool DisableEffects;
    public static bool EasyMode;
    public static float GuideLength = 1f; // 0..1 dipakai AimGuide
}
