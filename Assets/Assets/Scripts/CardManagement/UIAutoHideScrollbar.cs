using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class UIAutoHideScrollbar : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] float visibleAlpha = 1f;
    [SerializeField] float hiddenAlpha = 0f;
    [SerializeField] float fadeTime = .2f;
    [SerializeField] float idleHideDelay = 1.2f;

    CanvasGroup cg;
    Coroutine fadeCo, hideCo;
    bool pointerInside;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        if (!scrollRect) scrollRect = GetComponentInParent<ScrollRect>();
        SetAlpha(hiddenAlpha);
    }

    void OnEnable()
    {
        if (scrollRect) scrollRect.onValueChanged.AddListener(_ => OnUserScrolled());
        // reset state ketika aktif lagi
        StopAllCoroutines();
        SetAlpha(hiddenAlpha);
        pointerInside = false;
    }

    void OnDisable()
    {
        if (scrollRect) scrollRect.onValueChanged.RemoveListener(_ => OnUserScrolled());
        fadeCo = hideCo = null;
    }

    public void OnPointerEnter(PointerEventData e) { pointerInside = true; Show(); }
    public void OnPointerExit(PointerEventData e) { pointerInside = false; DelayedHide(); }

    void OnUserScrolled()
    {
        if (!isActiveAndEnabled) return;
        Show();
        DelayedHide();
    }

    void Show()
    {
        if (!isActiveAndEnabled) return;
        if (hideCo != null) StopCoroutine(hideCo);
        FadeTo(visibleAlpha);
    }

    void DelayedHide()
    {
        if (!isActiveAndEnabled) return;
        if (hideCo != null) StopCoroutine(hideCo);
        hideCo = StartCoroutine(HideAfterDelay());
    }

    IEnumerator HideAfterDelay()
    {
        // ketika GO dimatikan di tengah jalan, otomatis break
        float t = 0f;
        while (t < idleHideDelay)
        {
            if (!isActiveAndEnabled) yield break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!pointerInside) FadeTo(hiddenAlpha);
    }

    void FadeTo(float a)
    {
        if (!isActiveAndEnabled) return;
        if (fadeCo != null) StopCoroutine(fadeCo);
        fadeCo = StartCoroutine(FadeRoutine(a));
    }

    IEnumerator FadeRoutine(float target)
    {
        float start = cg ? cg.alpha : 0f;
        float t = 0f;
        while (t < fadeTime)
        {
            if (!isActiveAndEnabled) yield break;
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(start, target, t / fadeTime));
            yield return null;
        }
        SetAlpha(target);
    }

    void SetAlpha(float a)
    {
        if (cg) cg.alpha = a;
    }

    // --- dipanggil dari luar kalau konten ScrollRect berubah ---
    public static void NotifyUserScrolled(ScrollRect sr)
    {
        if (!sr) return;
        // cari HANYA komponen yang aktif; kalau scrollbar dimatikan oleh ScrollRect, cukup diabaikan
        var h = sr.GetComponentInChildren<UIAutoHideScrollbar>(false);
        if (h && h.isActiveAndEnabled) h.OnUserScrolled();
    }
}
