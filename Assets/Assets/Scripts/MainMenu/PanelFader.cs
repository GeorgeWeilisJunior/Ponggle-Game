using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PanelFader : MonoBehaviour
{
    [Header("Refs")]
    public CanvasGroup cg;

    [Header("Durations")]
    public float fadeIn = 0.15f;
    public float fadeOut = 0.12f;

    void Reset()
    {
        cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    void Awake()
    {
        if (!cg) cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>Buka panel dengan fade-in. Aman dipanggil meski panel sebelumnya non-aktif.</summary>
    public void Show(System.Action onDone = null)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(Fade(0f, 1f, fadeIn, true, onDone));
    }

    /// <summary>Tutup panel dengan fade-out. Setelah selesai, GameObject di-set inactive.</summary>
    public void Hide(System.Action onDone = null)
    {
        if (!gameObject.activeInHierarchy)
        {
            onDone?.Invoke();
            return;
        }
        StopAllCoroutines();
        StartCoroutine(Fade(1f, 0f, fadeOut, false, () =>
        {
            gameObject.SetActive(false);
            onDone?.Invoke();
        }));
    }

    IEnumerator Fade(float a, float b, float t, bool enableAtEnd, System.Action end)
    {
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();

        // Selama transisi, biar aman: raycast diblokir saat tutup, aktif saat buka.
        cg.blocksRaycasts = enableAtEnd;
        cg.interactable = false;

        cg.alpha = a;
        float time = 0f;
        while (time < t)
        {
            time += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(a, b, time / t);
            yield return null;
        }
        cg.alpha = b;

        cg.blocksRaycasts = enableAtEnd;
        cg.interactable = enableAtEnd;

        end?.Invoke();
    }
}
