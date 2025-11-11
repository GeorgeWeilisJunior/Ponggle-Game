using System.Collections;
using System.IO; // << untuk konversi buildIndex -> path -> nama scene
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    [Header("UI (drag dari _App/LoadingCanvas)")]
    [SerializeField] private CanvasGroup loadingCanvas;       // CanvasGroup di LoadingCanvas
    [SerializeField] private Slider progressSlider;           // opsional
    [SerializeField] private TextMeshProUGUI tipTMP;          // opsional (boleh kosong)
    [SerializeField] private Text legacyTip;                  // opsional (kalau pakai UI.Text)

    [Header("Fade & Timing")]
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private float minHoldSeconds = 0.35f;    // cegah “kedip” di loading cepat

    [Header("Tips (opsional)")]
    [TextArea]
    public string[] tips = new string[]
    {
        "Tip: Orange peg menaikkan Fever.",
        "Tip: Klik kanan untuk batalkan bidikan.",
        "Tip: Coba power Neso untuk triple-shot."
    };

    bool isBusy;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        HideInstant();
    }

    // ---------------- UI helpers ----------------
    void HideInstant()
    {
        if (!loadingCanvas) return;
        loadingCanvas.alpha = 0f;
        loadingCanvas.blocksRaycasts = false;
        loadingCanvas.interactable = false;
        if (progressSlider) progressSlider.value = 0f;
    }

    void ShowInstant()
    {
        if (!loadingCanvas) return;
        loadingCanvas.alpha = 1f;
        loadingCanvas.blocksRaycasts = true;
        loadingCanvas.interactable = true;
    }

    IEnumerator Fade(float from, float to, float dur)
    {
        if (!loadingCanvas) yield break;
        float t = 0f;
        loadingCanvas.alpha = from;
        loadingCanvas.blocksRaycasts = true;
        loadingCanvas.interactable = true;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            loadingCanvas.alpha = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }
        loadingCanvas.alpha = to;

        if (to <= 0f)
        {
            loadingCanvas.blocksRaycasts = false;
            loadingCanvas.interactable = false;
        }
    }

    void SetRandomTip()
    {
        if (tips == null || tips.Length == 0) return;
        string tip = tips[Random.Range(0, tips.Length)];
        if (tipTMP) tipTMP.text = tip;
        if (legacyTip) legacyTip.text = tip;
    }

    void SetProgress(float v)
    {
        if (progressSlider) progressSlider.value = Mathf.Clamp01(v);
    }

    // ================== PUBLIC API (string) ==================
    public static void LoadScene(string sceneName)
    {
        if (!Check()) return;
        if (!Instance.isBusy) Instance.StartCoroutine(Instance.LoadSceneRoutine(sceneName));
    }

    public static void LoadAdditive(string sceneName, bool unloadCurrent = false)
    {
        if (!Check()) return;
        if (!Instance.isBusy) Instance.StartCoroutine(Instance.LoadAdditiveRoutine(sceneName, unloadCurrent));
    }

    // ================== PUBLIC API (int / buildIndex) ==================
    public static void LoadScene(int buildIndex)
    {
        string path = SceneUtility.GetScenePathByBuildIndex(buildIndex);
        string name = Path.GetFileNameWithoutExtension(path);
        LoadScene(name);
    }

    public static void LoadAdditive(int buildIndex, bool unloadCurrent = false)
    {
        string path = SceneUtility.GetScenePathByBuildIndex(buildIndex);
        string name = Path.GetFileNameWithoutExtension(path);
        LoadAdditive(name, unloadCurrent);
    }

    // (opsional) helper next/prev berdasarkan index aktif
    public static void LoadNext()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        LoadScene(next);
    }
    public static void LoadPrevious()
    {
        int prev = SceneManager.GetActiveScene().buildIndex - 1;
        if (prev >= 0) LoadScene(prev);
    }

    // ================== internals ==================
    static bool Check()
    {
        if (Instance == null)
        {
            Debug.LogError("[SceneTransition] Tidak ada Instance. Pastikan _App aktif dulu.");
            return false;
        }
        return true;
    }

    IEnumerator LoadSceneRoutine(string sceneName)
    {
        isBusy = true;
        SetRandomTip();
        ShowInstant();
        SetProgress(0f);

        // Fade tutup
        yield return Fade(0f, 1f, fadeDuration);

        float hold = 0f;
        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        op.allowSceneActivation = false;

        // Unity progress 0..0.9 saat loading
        while (op.progress < 0.9f)
        {
            hold += Time.unscaledDeltaTime;
            SetProgress(op.progress / 0.9f);
            yield return null;
        }

        // cegah kedip (kalau load sangat cepat)
        while (hold < minHoldSeconds)
        {
            hold += Time.unscaledDeltaTime;
            yield return null;
        }

        // Masuk scene
        op.allowSceneActivation = true;
        yield return null; // tunggu 1 frame

        SetProgress(1f);

        // Fade buka
        yield return Fade(1f, 0f, fadeDuration);
        HideInstant();
        isBusy = false;
    }

    IEnumerator LoadAdditiveRoutine(string sceneName, bool unloadCurrent)
    {
        isBusy = true;
        SetRandomTip();
        ShowInstant();
        SetProgress(0f);

        yield return Fade(0f, 1f, fadeDuration);

        float hold = 0f;
        var current = SceneManager.GetActiveScene();

        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            hold += Time.unscaledDeltaTime;
            SetProgress(op.progress / 0.9f);
            yield return null;
        }

        while (hold < minHoldSeconds)
        {
            hold += Time.unscaledDeltaTime;
            yield return null;
        }

        op.allowSceneActivation = true;
        yield return null;

        var newScene = SceneManager.GetSceneByName(sceneName);
        if (newScene.IsValid()) SceneManager.SetActiveScene(newScene);

        if (unloadCurrent && current.IsValid())
            yield return SceneManager.UnloadSceneAsync(current);

        SetProgress(1f);
        yield return Fade(1f, 0f, fadeDuration);
        HideInstant();
        isBusy = false;
    }
}
