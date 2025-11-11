using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class InventoryIntroAnimator : MonoBehaviour
{
    [Header("Required")]
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] RectTransform leftPanel;
    [SerializeField] RectTransform rightPanel;
    [SerializeField] Transform gridContent;

    [Header("Optional")]
    [SerializeField] Image verticalScrollbar;
    [SerializeField] float tileStagger = 0.03f;
    [SerializeField] int maxStaggeredTiles = 12;

    [Header("Timings (unscaled)")]
    [SerializeField] float fadeInTime = 0.25f;
    [SerializeField] float panelsSlideTime = 0.35f;
    [SerializeField] float tilesPopTime = 0.18f;
    [SerializeField] float afterHold = 0.05f;

    [Header("Motion")]
    [SerializeField] float leftFromX = -80f;
    [SerializeField] float rightFromX = 80f;
    [SerializeField] float startScale = 0.96f;

    [Header("Behaviour")]
    [SerializeField] bool playOnlyOncePerSession = true;

    static bool hasPlayedOnce = false;
    Vector3 leftInitPos, rightInitPos;
    bool cachedInit;

    void Reset() { canvasGroup = GetComponent<CanvasGroup>(); }

    void Awake()
    {
        CacheInitIfNeeded();
    }

    void CacheInitIfNeeded()
    {
        if (cachedInit) return;
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        if (leftPanel) leftInitPos = leftPanel.anchoredPosition3D;
        if (rightPanel) rightInitPos = rightPanel.anchoredPosition3D;
        cachedInit = true;
    }

    void OnEnable()
    {
        CacheInitIfNeeded();

        if (playOnlyOncePerSession && hasPlayedOnce)
        {
            SetupImmediate();
            return;
        }
        hasPlayedOnce = true;
        StartCoroutine(PlayIntro());
    }

    void OnDisable()
    {
        // kembalikan ke keadaan “normal”
        if (!canvasGroup) return;
        canvasGroup.alpha = 1f;
        if (leftPanel) { leftPanel.anchoredPosition3D = leftInitPos; leftPanel.localScale = Vector3.one; }
        if (rightPanel) { rightPanel.anchoredPosition3D = rightInitPos; rightPanel.localScale = Vector3.one; }
        if (verticalScrollbar) verticalScrollbar.canvasRenderer.SetAlpha(1f);

        if (gridContent)
            foreach (Transform t in gridContent)
            {
                var cg = t.GetComponent<CanvasGroup>();
                if (cg) cg.alpha = 1f;
                t.localScale = Vector3.one;
            }
    }

    void SetupImmediate()
    {
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        if (leftPanel) { leftPanel.anchoredPosition3D = leftInitPos; leftPanel.localScale = Vector3.one; }
        if (rightPanel) { rightPanel.anchoredPosition3D = rightInitPos; rightPanel.localScale = Vector3.one; }
        if (verticalScrollbar) verticalScrollbar.canvasRenderer.SetAlpha(1f);

        if (gridContent)
            foreach (Transform t in gridContent)
            {
                var cg = t.GetComponent<CanvasGroup>(); if (!cg) continue;
                cg.alpha = 1f; t.localScale = Vector3.one;
            }
    }

    IEnumerator PlayIntro()
    {
        // tunggu 1 frame supaya InventoryUI selesai spawn kartu
        yield return new WaitForEndOfFrame();

        Vector3 leftStart = leftInitPos + new Vector3(leftFromX, 0, 0);
        Vector3 rightStart = rightInitPos + new Vector3(rightFromX, 0, 0);

        canvasGroup.alpha = 0f;
        if (leftPanel) { leftPanel.anchoredPosition3D = leftStart; leftPanel.localScale = Vector3.one * startScale; }
        if (rightPanel) { rightPanel.anchoredPosition3D = rightStart; rightPanel.localScale = Vector3.one * startScale; }
        if (verticalScrollbar) verticalScrollbar.canvasRenderer.SetAlpha(0f);

        // siapkan tiles pertama
        if (gridContent)
        {
            int prepared = 0;
            foreach (Transform s in gridContent)
            {
                var cg =s.GetComponent<CanvasGroup>();
                if (!cg) cg = s.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                s.localScale = Vector3.one * startScale;
                prepared++; if (prepared >= maxStaggeredTiles) break;
            }
        }

        // 1) Fade in
        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeInTime);
            yield return null;
        }

        // 2) Slide + pop panels
        t = 0f;
        while (t < panelsSlideTime)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOutBack(Mathf.Clamp01(t / panelsSlideTime));

            if (leftPanel)
            {
                leftPanel.anchoredPosition3D = Vector3.LerpUnclamped(leftStart, leftInitPos, k);
                leftPanel.localScale = Vector3.LerpUnclamped(Vector3.one * startScale, Vector3.one, k);
            }
            if (rightPanel)
            {
                rightPanel.anchoredPosition3D = Vector3.LerpUnclamped(rightStart, rightInitPos, k);
                rightPanel.localScale = Vector3.LerpUnclamped(Vector3.one * startScale, Vector3.one, k);
            }
            yield return null;
        }

        if (verticalScrollbar) verticalScrollbar.CrossFadeAlpha(1f, 0.2f, true);
        yield return new WaitForSecondsRealtime(afterHold);

        // 3) Stagger tiles
        if (gridContent)
        {
            int shown = 0;
            foreach (Transform tr in gridContent)
            {
                var cg = tr.GetComponent<CanvasGroup>();
                if (!cg) break;
                StartCoroutine(PopTile(tr, cg, tilesPopTime));
                shown++; if (shown >= maxStaggeredTiles) break;
                yield return new WaitForSecondsRealtime(tileStagger);
            }
        }
    }

    IEnumerator PopTile(Transform tr, CanvasGroup cg, float dur)
    {
        float t = 0f;
        Vector3 s0 = tr.localScale, s1 = Vector3.one;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOutBack(Mathf.Clamp01(t / dur));
            tr.localScale = Vector3.LerpUnclamped(s0, s1, k);
            cg.alpha = Mathf.Lerp(0f, 1f, k);
            yield return null;
        }
        tr.localScale = s1; cg.alpha = 1f;
    }

    float EaseOutBack(float x)
    {
        const float c1 = 1.70158f; const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
    }
}
