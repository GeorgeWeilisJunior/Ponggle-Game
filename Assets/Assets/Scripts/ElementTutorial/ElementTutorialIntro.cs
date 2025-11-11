using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[DisallowMultipleComponent]
public class ElementTutorialIntro : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private RectTransform listContent; // Rightpane/Viewport/ListContent
    [SerializeField] private RectTransform leftPane;    // optional: panel kiri (preview box)
    [SerializeField] private RectTransform rightPane;   // optional: panel kanan (frame list)

    [Header("Item Stagger Intro")]
    [SerializeField, Min(0f)] private float itemStagger = 0.035f;   // detik realtime antar item
    [SerializeField, Min(0.01f)] private float itemDuration = 0.18f; // durasi tiap item
    [SerializeField] private float itemSlideY = 28f;                 // px: start offset dari atas
    [SerializeField]
    private AnimationCurve itemCurve =               // posisi+alpha
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Pane Intro (optional)")]
    [SerializeField, Min(0.01f)] private float paneDuration = 0.22f;
    [SerializeField] private float paneSlideY = 24f;
    [SerializeField]
    private AnimationCurve paneCurve =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Run When Enabled")]
    [SerializeField] private bool playOnEnable = true;

    Coroutine introCR;

    void OnEnable()
    {
        if (playOnEnable)
        {
            // pastikan di frame berikut layout sudah settle
            introCR = StartCoroutine(PlayIntro());
        }
    }

    public void Play() { if (introCR != null) StopCoroutine(introCR); introCR = StartCoroutine(PlayIntro()); }
    public void StopIntroImmediate()
    {
        if (introCR != null) StopCoroutine(introCR);
        // snap semua ke state akhir (alpha 1, posisi normal)
        if (leftPane) SnapAlphaPos(leftPane, 1f, Vector2.zero, true);
        if (rightPane) SnapAlphaPos(rightPane, 1f, Vector2.zero, true);

        if (listContent)
        {
            for (int i = 0; i < listContent.childCount; i++)
            {
                var rt = listContent.GetChild(i) as RectTransform;
                if (!rt) continue;
                SnapAlphaPos(rt, 1f, Vector2.zero, true);
            }
        }
    }

    IEnumerator PlayIntro()
    {
        // 1) tunggu satu frame agar layout siap
        yield return null;
        if (listContent) LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);

        // 2) pane kiri/kanan (opsional)
        if (leftPane) StartCoroutine(SlideFade(leftPane, paneSlideY, paneDuration, paneCurve));
        if (rightPane) StartCoroutine(SlideFade(rightPane, paneSlideY, paneDuration, paneCurve));

        // 3) item stagger
        if (listContent)
        {
            for (int i = 0; i < listContent.childCount; i++)
            {
                var rt = listContent.GetChild(i) as RectTransform;
                if (!rt) continue;
                StartCoroutine(SlideFade(rt, itemSlideY, itemDuration, itemCurve));
                yield return new WaitForSecondsRealtime(itemStagger);
            }
        }
    }

    IEnumerator SlideFade(RectTransform target, float slideY, float duration, AnimationCurve curve)
    {
        if (!target) yield break;

        // pakai/buat CanvasGroup sementara
        var cg = target.GetComponent<CanvasGroup>();
        bool created = false;
        if (!cg) { cg = target.gameObject.AddComponent<CanvasGroup>(); created = true; }

        // simpan posisi asli (anchoredPosition)
        Vector2 basePos = target.anchoredPosition;
        Vector2 from = basePos + new Vector2(0f, slideY);
        Vector2 to = basePos;

        float t = 0f;
        duration = Mathf.Max(0.01f, duration);
        cg.alpha = 0f;
        target.anchoredPosition = from;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            float k = curve.Evaluate(Mathf.Clamp01(t));
            target.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
            cg.alpha = k;
            yield return null;
        }

        target.anchoredPosition = to;
        cg.alpha = 1f;

        // jika CanvasGroup dibuat hanya untuk intro, hapus agar tidak mengganggu
        if (created) Destroy(cg);
    }

    void SnapAlphaPos(RectTransform rt, float alpha, Vector2 deltaFromBase, bool toBase)
    {
        if (!rt) return;
        var cg = rt.GetComponent<CanvasGroup>();
        if (!cg) cg = rt.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = alpha;
        if (toBase) rt.anchoredPosition = rt.anchoredPosition + Vector2.zero; // no-op: pos akhir = posisi sekarang
    }
}
