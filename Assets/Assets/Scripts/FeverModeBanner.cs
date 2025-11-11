using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class FeverModeBanner : MonoBehaviour
{
    [Header("Roots")]
    public RectTransform feverRoot;    // child: Fever, Mode
    public RectTransform extremeRoot;  // child: Extreme, Fever, Mode

    [Header("Auto-mapping by child name")]
    public bool autoMapByName = true;
    public string nameExtreme = "Extreme";
    public string nameFever = "Fever";
    public string nameMode = "Mode";

    [Header("Visual")]
    public CanvasGroup canvasGroup;            // parent canvas (mis. FeverDramatic)
    public float enterOffsetX = 900f;
    public float slideDuration = 0.55f;
    public float wordStagger = 0.12f;
    public float holdDuration = 1.2f;       // dipakai saat autoHide
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public bool unscaledTime = true;
    public bool autoHide = false;
    public float hideFadeDuration = 0.3f;

    struct WordSet
    {
        public RectTransform extreme, fever, mode;
        public Vector2 extremeT, feverT, modeT;
        public bool HasExtreme => extreme;
        public bool HasFever => fever;
        public bool HasMode => mode;
    }

    WordSet feverSet, extremeSet;
    Coroutine playCo;
    bool isExtreme;

    void Reset()
    {
        canvasGroup = GetComponentInChildren<CanvasGroup>(true);
    }

    void Awake()
    {
        if (!canvasGroup) canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        MapAll();
        HideImmediate();
    }

    void OnEnable() { HideImmediate(); }
    void OnDisable() { if (playCo != null) StopCoroutine(playCo); playCo = null; }

    void OnValidate() { if (autoMapByName) MapAll(); }

    // ===== PUBLIC API =====
    public void Play(bool extreme)
    {
        isExtreme = (extreme && extremeRoot != null); // fallback kalau extremeRoot belum di-assign
        if (playCo != null) StopCoroutine(playCo);
        playCo = StartCoroutine(PlayRoutine());
    }

    // overload untuk auto-hide dalam N detik (setelah semua kata masuk)
    public void Play(bool extreme, float autoHideAfterSeconds)
    {
        autoHide = true;
        holdDuration = Mathf.Max(0f, autoHideAfterSeconds);
        Play(extreme);
    }

    public void HideImmediate()
    {
        if (canvasGroup)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        if (feverRoot) feverRoot.gameObject.SetActive(false);
        if (extremeRoot) extremeRoot.gameObject.SetActive(false);

        ParkOffscreen(ref feverSet);
        ParkOffscreen(ref extremeSet);
    }

    // ===== INTERNAL =====
    void MapAll()
    {
        feverSet = BuildSetFromRoot(feverRoot);
        extremeSet = BuildSetFromRoot(extremeRoot);
    }

    WordSet BuildSetFromRoot(RectTransform root)
    {
        WordSet s = new WordSet();
        if (!root) return s;

        RectTransform FindByName(string n)
        {
            var arr = root.GetComponentsInChildren<RectTransform>(true);
            foreach (var r in arr)
                if (r != null && r.gameObject.name == n) return r;
            return null;
        }

        s.extreme = autoMapByName ? FindByName(nameExtreme) : null;
        s.fever = autoMapByName ? FindByName(nameFever) : null;
        s.mode = autoMapByName ? FindByName(nameMode) : null;

        if (s.extreme) s.extremeT = s.extreme.anchoredPosition;
        if (s.fever) s.feverT = s.fever.anchoredPosition;
        if (s.mode) s.modeT = s.mode.anchoredPosition;

        return s;
    }

    void ParkOffscreen(ref WordSet s)
    {
        if (s.fever) s.fever.anchoredPosition = s.feverT + Vector2.left * enterOffsetX;
        if (s.mode) s.mode.anchoredPosition = s.modeT + Vector2.left * enterOffsetX;
        if (s.extreme) s.extreme.anchoredPosition = s.extremeT + Vector2.left * enterOffsetX;
    }

    IEnumerator PlayRoutine()
    {
        // aktifkan hanya satu root
        if (feverRoot) feverRoot.gameObject.SetActive(!isExtreme);
        if (extremeRoot) extremeRoot.gameObject.SetActive(isExtreme);

        WordSet set = isExtreme ? extremeSet : feverSet;

        // reset posisi start (di kiri)
        ParkOffscreen(ref set);

        if (canvasGroup)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (isExtreme)
        {
            if (set.HasExtreme) yield return SlideIn(set.extreme, set.extremeT, slideDuration);
            yield return Wait(wordStagger);
            if (set.HasFever) yield return SlideIn(set.fever, set.feverT, slideDuration);
            yield return Wait(wordStagger);
            if (set.HasMode) yield return SlideIn(set.mode, set.modeT, slideDuration);
        }
        else
        {
            if (set.HasFever) yield return SlideIn(set.fever, set.feverT, slideDuration);
            yield return Wait(wordStagger);
            if (set.HasMode) yield return SlideIn(set.mode, set.modeT, slideDuration);
        }

        if (autoHide)
        {
            yield return Wait(holdDuration);

            // default: fade out
            float t = 0f;
            float dur = Mathf.Max(0.0001f, hideFadeDuration);
            while (t < dur)
            {
                t += Delta();
                float k = Mathf.Clamp01(t / dur);
                if (canvasGroup) canvasGroup.alpha = 1f - k;
                yield return null;
            }
            HideImmediate();
        }

        playCo = null;
    }

    IEnumerator SlideIn(RectTransform word, Vector2 target, float duration)
    {
        if (!word) yield break;
        Vector2 start = word.anchoredPosition;
        float t = 0f;
        while (t < duration)
        {
            t += Delta();
            float u = Mathf.Clamp01(t / duration);
            float k = ease != null ? ease.Evaluate(u) : u;
            word.anchoredPosition = Vector2.LerpUnclamped(start, target, k);
            yield return null;
        }
        word.anchoredPosition = target;
    }

    float Delta() => unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    IEnumerator Wait(float s)
    {
        if (s <= 0f) yield break;
        if (unscaledTime) yield return new WaitForSecondsRealtime(s);
        else yield return new WaitForSeconds(s);
    }
}
