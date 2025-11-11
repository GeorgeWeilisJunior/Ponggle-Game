using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[DisallowMultipleComponent]
public class ReactionEntryUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Toggle headerToggle;      // Toggle pada header (Transition=None)
    [SerializeField] private GameObject details;       // Container details
    [SerializeField] private RectTransform layoutRoot; // Root untuk rebuild layout

    [Header("Arrow (pilih salah satu)")]
    [SerializeField] private Image arrowIcon;
    [SerializeField] private Sprite arrowDown;
    [SerializeField] private Sprite arrowUp;
    [SerializeField] private RectTransform arrowRect;  // auto = arrowIcon.rectTransform

    [SerializeField] private GameObject arrowUpGO;     // alternatif 2 GO
    [SerializeField] private GameObject arrowDownGO;

    [Header("Header Feedback (opsional)")]
    [SerializeField] private Graphic headerGraphic;
    [SerializeField] private RectTransform headerRect;
    [SerializeField] private Color pressTint = Color.white;
    [SerializeField, Min(0f)] private float tintDuration = 0.08f;
    [SerializeField, Min(0f)] private float popScale = 0.97f;
    [SerializeField, Min(0f)] private float popDuration = 0.10f;

    [Header("Behaviour")]
    [SerializeField] private bool startOpen = false;

    [Header("Details Fade")]
    [SerializeField, Min(0f)] private float fadeDuration = 0.18f;
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Arrow Rotate")]
    [SerializeField] private float arrowClosedZ = 0f;
    [SerializeField] private float arrowOpenedZ = 180f;
    [SerializeField, Min(0f)] private float arrowRotateDuration = 0.18f;
    [SerializeField] private AnimationCurve arrowRotateCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Audio (opsional)")]
    [SerializeField] private string sfxOpenKey = "MainMenuClick";  // bunyi saat buka
    [SerializeField] private string sfxCloseKey = "MainMenuClick";  // bunyi saat tutup

    public bool IsOpen { get; private set; }

    CanvasGroup detailsCG;
    Coroutine fadeCR, arrowCR, tintCR, popCR;
    Color headerNormal;

    void Reset()
    {
        if (!headerToggle) headerToggle = GetComponentInChildren<Toggle>(true);
        if (!layoutRoot) layoutRoot = transform as RectTransform;
        if (!arrowRect && arrowIcon) arrowRect = arrowIcon.rectTransform;
        if (!headerRect && headerGraphic) headerRect = headerGraphic.rectTransform;
    }

    void Awake()
    {
        if (details)
        {
            detailsCG = details.GetComponent<CanvasGroup>();
            if (!detailsCG) detailsCG = details.AddComponent<CanvasGroup>();
        }

        if (!arrowRect && arrowIcon) arrowRect = arrowIcon.rectTransform;
        if (headerGraphic) headerNormal = headerGraphic.color;
        if (!headerRect && headerGraphic) headerRect = headerGraphic.rectTransform;

        // sinkron awal
        if (headerToggle) headerToggle.SetIsOnWithoutNotify(startOpen);
        ApplyStateInstant(startOpen);

        if (headerToggle) headerToggle.onValueChanged.AddListener(OnHeaderToggled);
    }

    void OnDestroy()
    {
        if (headerToggle) headerToggle.onValueChanged.RemoveListener(OnHeaderToggled);
    }

    void OnHeaderToggled(bool on)
    {
        PulseHeader();

        // SFX — tetap bunyi saat pause
        if (on && !string.IsNullOrEmpty(sfxOpenKey))
            AudioManager.I?.PlayUI(sfxOpenKey, ignorePause: true);
        if (!on && !string.IsNullOrEmpty(sfxCloseKey))
            AudioManager.I?.PlayUI(sfxCloseKey, ignorePause: true);

        SetOpen(on);
    }

    /* ===== PUBLIC ===== */
    public void ForceSetOpen(bool value)
    {
        if (headerToggle) headerToggle.SetIsOnWithoutNotify(value);
        SetOpen(value);
    }

    public void ForceSetOpenInstant(bool value)
    {
        if (headerToggle) headerToggle.SetIsOnWithoutNotify(value);
        SetOpenInstant(value);
    }

    /* ===== CORE ===== */
    public void SetOpen(bool value)
    {
        if (value == IsOpen) { AnimateArrow(value); return; }
        if (fadeCR != null) StopCoroutine(fadeCR);
        fadeCR = StartCoroutine(FadeSequence(value));
    }

    public void SetOpenInstant(bool value)
    {
        if (fadeCR != null) StopCoroutine(fadeCR);
        ApplyStateInstant(value);
    }

    void ApplyStateInstant(bool open)
    {
        IsOpen = open;

        if (details)
        {
            details.SetActive(open);
            if (detailsCG)
            {
                detailsCG.alpha = open ? 1f : 0f;
                detailsCG.blocksRaycasts = open;
                detailsCG.interactable = open;
            }
        }

        UpdateArrowVisual(open);
        SnapArrowRotation(open);
        RequestLayoutRebuild();
    }

    IEnumerator FadeSequence(bool targetOpen)
    {
        IsOpen = targetOpen;

        UpdateArrowVisual(targetOpen);
        AnimateArrow(targetOpen);

        if (targetOpen)
        {
            if (details) details.SetActive(true);
            if (detailsCG)
            {
                detailsCG.blocksRaycasts = true;
                detailsCG.interactable = true;
                yield return StartCoroutine(FadeTo(1f));
            }
            RequestLayoutRebuild();
            yield break;
        }

        if (detailsCG)
        {
            detailsCG.blocksRaycasts = false;
            detailsCG.interactable = false;
            yield return StartCoroutine(FadeTo(0f));
        }
        if (details) details.SetActive(false);
        RequestLayoutRebuild();
    }

    IEnumerator FadeTo(float target)
    {
        if (!detailsCG || Mathf.Approximately(detailsCG.alpha, target) || fadeDuration <= 0f)
        { if (detailsCG) detailsCG.alpha = target; yield break; }

        float start = detailsCG.alpha;
        float t = 0f, dur = Mathf.Max(0.01f, fadeDuration);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            detailsCG.alpha = Mathf.Lerp(start, target, fadeCurve.Evaluate(Mathf.Clamp01(t)));
            yield return null;
        }
        detailsCG.alpha = target;
    }

    /* ---- Arrow ---- */
    void UpdateArrowVisual(bool open)
    {
        if (arrowUpGO || arrowDownGO)
        {
            if (arrowUpGO) arrowUpGO.SetActive(open);
            if (arrowDownGO) arrowDownGO.SetActive(!open);
        }
        else if (arrowIcon && (arrowUp || arrowDown))
        {
            arrowIcon.sprite = open ? (arrowUp ? arrowUp : arrowIcon.sprite)
                                    : (arrowDown ? arrowDown : arrowIcon.sprite);
        }
    }

    void SnapArrowRotation(bool open)
    {
        if (!arrowRect) return;
        arrowRect.localEulerAngles = new Vector3(0, 0, open ? arrowOpenedZ : arrowClosedZ);
    }

    void AnimateArrow(bool open)
    {
        if (!arrowRect || arrowRotateDuration <= 0f) { SnapArrowRotation(open); return; }
        if (arrowCR != null) StopCoroutine(arrowCR);
        arrowCR = StartCoroutine(ArrowRotateTo(open ? arrowOpenedZ : arrowClosedZ));
    }

    IEnumerator ArrowRotateTo(float toZ)
    {
        float fromZ = arrowRect.localEulerAngles.z;
        float t = 0f, dur = Mathf.Max(0.01f, arrowRotateDuration);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float k = arrowRotateCurve.Evaluate(Mathf.Clamp01(t));
            float z = Mathf.LerpAngle(fromZ, toZ, k);
            arrowRect.localEulerAngles = new Vector3(0, 0, z);
            yield return null;
        }
        arrowRect.localEulerAngles = new Vector3(0, 0, toZ);
    }

    /* ---- Header feedback ---- */
    void PulseHeader()
    {
        if (headerGraphic)
        {
            if (tintCR != null) StopCoroutine(tintCR);
            tintCR = StartCoroutine(TintOnce());
        }
        if (headerRect && popScale > 0f && popScale < 1.001f)
        {
            if (popCR != null) StopCoroutine(popCR);
            popCR = StartCoroutine(PopOnce());
        }
    }

    IEnumerator TintOnce()
    {
        Color from = headerGraphic.color;
        Color to = pressTint;
        float t = 0f, half = Mathf.Max(0.01f, tintDuration);
        while (t < 1f) { t += Time.unscaledDeltaTime / half; headerGraphic.color = Color.Lerp(from, to, t); yield return null; }
        t = 0f;
        while (t < 1f) { t += Time.unscaledDeltaTime / half; headerGraphic.color = Color.Lerp(to, headerNormal, t); yield return null; }
        headerGraphic.color = headerNormal;
    }

    IEnumerator PopOnce()
    {
        Vector3 baseS = Vector3.one;
        Vector3 minS = new Vector3(popScale, popScale, 1f);
        float t = 0f, half = Mathf.Max(0.01f, popDuration);
        while (t < 1f) { t += Time.unscaledDeltaTime / half; headerRect.localScale = Vector3.Lerp(baseS, minS, t); yield return null; }
        t = 0f;
        while (t < 1f) { t += Time.unscaledDeltaTime / half; headerRect.localScale = Vector3.Lerp(minS, baseS, t); yield return null; }
        headerRect.localScale = baseS;
    }

    /* ---- Layout ---- */
    void RequestLayoutRebuild()
    {
        if (!layoutRoot) return;
        LayoutRebuilder.MarkLayoutForRebuild(layoutRoot);
        StartCoroutine(RebuildNextFrame());
    }

    IEnumerator RebuildNextFrame()
    {
        yield return null;
        if (layoutRoot) LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
    }
}
