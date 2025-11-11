using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MenuListIntro : MonoBehaviour
{
    [Header("Targets (urut sesuai tampilan)")]
    [SerializeField] List<RectTransform> items = new List<RectTransform>();

    [Header("Slide In")]
    [SerializeField] float offsetX = 480f;
    [SerializeField] float duration = 0.35f;
    [SerializeField] float delayBetween = 0.06f;
    [SerializeField] float overshoot = 14f;

    [Header("Stabilize Layout")]
    [Tooltip("Berapa frame menunggu supaya Layout/ContentSizeFitter selesai dulu")]
    [SerializeField] int layoutWaitFrames = 1;

    [Header("Input gating selama animasi")]
    [Tooltip("Hanya blokir click/hover (raycast) tanpa mengubah warna Button")]
    [SerializeField] bool blockRaycastsDuringAnim = true;
    [Tooltip("Kalau true, ikut nonaktifkan CanvasGroup.interactable (akan memicu Disabled Color). Biarkan OFF agar tidak abu-abu.")]
    [SerializeField] bool alsoDisableInteractable = false;

    [Header("SFX (optional)")]
    [SerializeField] string swooshKey = "UISwoosh";
    [SerializeField] bool sfxEachItem = true;

    [Header("Play Options")]
    [SerializeField] bool playOnEnable = true;
    [SerializeField] bool onlyOnce = true;

    bool hasPlayed;
    CanvasGroup cg;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        if (!playOnEnable) return;
        if (onlyOnce && hasPlayed) return;
        StopAllCoroutines();
        StartCoroutine(PlayIntro());
    }

    public void PlayNow()
    {
        StopAllCoroutines();
        StartCoroutine(PlayIntro());
    }

    IEnumerator PlayIntro()
    {
        hasPlayed = true;

        // 1) Tunggu layout settle
        for (int i = 0; i < Mathf.Max(0, layoutWaitFrames); i++)
            yield return null;

        // 2) Simpan posisi akhir & geser ke kiri
        var endPos = new Vector2[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (!it) continue;
            endPos[i] = it.anchoredPosition;
            it.anchoredPosition = endPos[i] + Vector2.left * offsetX;
        }

        // 3) Gating input tanpa memicu Disabled Color
        bool prevBlocks = cg.blocksRaycasts;
        bool prevInteract = cg.interactable;
        if (blockRaycastsDuringAnim) cg.blocksRaycasts = false;   // cukup ini supaya tidak bisa diklik
        if (alsoDisableInteractable) cg.interactable = false;     // MATIKAN jika kamu memang ingin Button jadi disabled

        // 4) SFX awal (opsional)
        if (!string.IsNullOrEmpty(swooshKey) && !sfxEachItem)
            AudioManager.I?.PlayUI(swooshKey);

        // 5) Mainkan satu-satu (stagger)
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (!it) continue;
            StartCoroutine(SlideOne(it, endPos[i]));
            if (sfxEachItem && !string.IsNullOrEmpty(swooshKey))
                AudioManager.I?.PlayUI(swooshKey);
            yield return new WaitForSecondsRealtime(delayBetween);
        }

        // 6) Tunggu item terakhir selesai
        yield return new WaitForSecondsRealtime(duration + 0.05f);

        // 7) Rebase hover
        for (int i = 0; i < items.Count; i++)
            if (items[i]) items[i].SendMessage("RebaseNow", SendMessageOptions.DontRequireReceiver);

        // 8) Pulihkan gating input
        if (blockRaycastsDuringAnim) cg.blocksRaycasts = prevBlocks;
        if (alsoDisableInteractable) cg.interactable = prevInteract;
    }

    IEnumerator SlideOne(RectTransform rt, Vector2 target)
    {
        Vector2 start = rt.anchoredPosition;
        Vector2 over = target + Vector2.right * overshoot;

        float t = 0f;
        float half = duration * 0.75f;

        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOutCubic(t / half);
            rt.anchoredPosition = Vector2.LerpUnclamped(start, over, k);
            yield return null;
        }

        t = 0f;
        float rest = duration - half;
        while (t < rest)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOutCubic(t / rest);
            rt.anchoredPosition = Vector2.LerpUnclamped(over, target, k);
            yield return null;
        }

        rt.anchoredPosition = target;
        rt.SendMessage("RebaseNow", SendMessageOptions.DontRequireReceiver);
    }

    static float EaseOutCubic(float x)
    {
        x = Mathf.Clamp01(x);
        return 1f - Mathf.Pow(1f - x, 3f);
    }
}
