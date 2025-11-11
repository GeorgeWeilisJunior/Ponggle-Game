// UIHoverNudgeTint.cs (drop-in safe update; backward-compatible fields)
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIHoverNudgeTint : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [Header("Targets")]
    [SerializeField] RectTransform moveTarget; // drag: Background (RectTransform)
    [SerializeField] Image tintTarget;         // drag: Background (Image/TMP's Graphic via Image if any)
    [SerializeField] Image glowOverlay;        // optional: child Image untuk glow

    [Header("Motion")]
    [SerializeField] float hoverNudgeX = 0f;
    [SerializeField] float hoverNudgeY = 8f;
    [SerializeField] float pressNudgeX = 0f;
    [SerializeField] float pressNudgeY = -2f;
    [SerializeField, Min(0.01f)] float animTime = 0.08f;

    [Header("Tint")]
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color highlightedColor = Color.white;
    [SerializeField] Color pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);

    [Header("Glow Overlay (optional)")]
    [SerializeField, Range(0f, 1f)] float glowNormalAlpha = 0f;
    [SerializeField, Range(0f, 1f)] float glowHighlightedAlpha = 0.35f;
    [SerializeField, Range(0f, 1f)] float glowPressedAlpha = 0.25f;

    [Header("SFX (optional)")]
    [SerializeField] string hoverSfxKey = "";
    [SerializeField] string clickSfxKey = "";

    [Header("Safety / QoL")]
    [Tooltip("Aktifkan agar LayoutGroup parent tidak menggeser tombol saat di-nudge.")]
    [SerializeField] bool autoIgnoreLayout = true;
    [Tooltip("Rebase ulang ke posisi terbaru ketika pointer keluar (aman untuk tombol yang pindah karena anim/intro).")]
    [SerializeField] bool rebaseOnPointerExit = false;

    Vector2 basePos;
    Coroutine tweenPos, tweenColor, tweenGlow;
    bool isHover;

    LayoutElement layoutElem;

    void Reset()
    {
        moveTarget = GetComponent<RectTransform>();
        tintTarget = GetComponent<Image>();
    }

    void Awake()
    {
        if (!moveTarget) moveTarget = GetComponent<RectTransform>();
        if (!tintTarget) tintTarget = GetComponent<Image>();

        if (tintTarget) tintTarget.color = normalColor;
        if (glowOverlay != null)
        {
            var c = glowOverlay.color; c.a = glowNormalAlpha; glowOverlay.color = c;
        }

        if (autoIgnoreLayout)
        {
            layoutElem = GetComponent<LayoutElement>();
            if (!layoutElem) layoutElem = gameObject.AddComponent<LayoutElement>();
            layoutElem.ignoreLayout = true;
        }
        // basePos akan dicapture di OnEnable (end-of-frame)
    }

    void OnEnable()
    {
        StopAllCoroutines();
        StartCoroutine(CaptureBaseEndOfFrame());
    }

    IEnumerator CaptureBaseEndOfFrame()
    {
        // Tunggu 1 frame supaya posisi final setelah layout/anim masuk
        yield return null;
        if (moveTarget) basePos = moveTarget.anchoredPosition;
        // Pastikan warna/alpha kembali ke normal state
        if (tintTarget) tintTarget.color = normalColor;
        if (glowOverlay)
        {
            var c = glowOverlay.color; c.a = glowNormalAlpha; glowOverlay.color = c;
        }
    }

    /// <summary> Dipanggil manual jika kamu tahu tombol selesai dianimasikan/berpindah. </summary>
    public void RebaseNow()
    {
        if (moveTarget) basePos = moveTarget.anchoredPosition;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        isHover = true;
        if (!string.IsNullOrEmpty(hoverSfxKey))
            AudioManager.I?.PlayUI(hoverSfxKey);

        MoveTo(basePos + new Vector2(hoverNudgeX, hoverNudgeY));
        TintTo(highlightedColor);
        GlowTo(glowHighlightedAlpha);
    }

    public void OnPointerExit(PointerEventData e)
    {
        isHover = false;
        MoveTo(basePos);
        TintTo(normalColor);
        GlowTo(glowNormalAlpha);

        if (rebaseOnPointerExit && moveTarget)
            basePos = moveTarget.anchoredPosition; // ambil posisi baru sbg base
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (!string.IsNullOrEmpty(clickSfxKey))
            AudioManager.I?.PlayUI(clickSfxKey);

        MoveTo(basePos + new Vector2(pressNudgeX, pressNudgeY));
        TintTo(pressedColor);
        GlowTo(glowPressedAlpha);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (isHover)
        {
            MoveTo(basePos + new Vector2(hoverNudgeX, hoverNudgeY));
            TintTo(highlightedColor);
            GlowTo(glowHighlightedAlpha);
        }
        else
        {
            MoveTo(basePos);
            TintTo(normalColor);
            GlowTo(glowNormalAlpha);
        }
    }

    void MoveTo(Vector2 target)
    {
        if (!moveTarget) return;
        if (tweenPos != null) StopCoroutine(tweenPos);
        tweenPos = StartCoroutine(TweenVector2(
            v => moveTarget.anchoredPosition = v,
            moveTarget.anchoredPosition, target, animTime));
    }

    void TintTo(Color target)
    {
        if (!tintTarget) return;
        if (tweenColor != null) StopCoroutine(tweenColor);
        tweenColor = StartCoroutine(TweenColor(
            c => tintTarget.color = c,
            tintTarget.color, target, animTime));
    }

    void GlowTo(float targetAlpha)
    {
        if (glowOverlay == null) return;
        if (tweenGlow != null) StopCoroutine(tweenGlow);
        Color from = glowOverlay.color;
        Color to = from; to.a = targetAlpha;
        tweenGlow = StartCoroutine(TweenColor(
            c => glowOverlay.color = c, from, to, animTime));
    }

    IEnumerator TweenVector2(System.Action<Vector2> apply, Vector2 from, Vector2 to, float t)
    {
        float el = 0f;
        while (el < t)
        {
            el += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, el / Mathf.Max(0.0001f, t));
            apply(Vector2.LerpUnclamped(from, to, k));
            yield return null;
        }
        apply(to);
    }

    IEnumerator TweenColor(System.Action<Color> apply, Color from, Color to, float t)
    {
        float el = 0f;
        while (el < t)
        {
            el += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, el / Mathf.Max(0.0001f, t));
            apply(Color.LerpUnclamped(from, to, k));
            yield return null;
        }
        apply(to);
    }

    // Jika canvas/anchor berubah (resize), rebase otomatis ke posisi baru
    void OnRectTransformDimensionsChange()
    {
        if (moveTarget)
            basePos = moveTarget.anchoredPosition;
    }
}
