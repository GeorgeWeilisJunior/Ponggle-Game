using UnityEngine;
using System.Collections;

public class CardManagementIntro : MonoBehaviour
{
    [Header("Targets (RectTransform / CanvasGroup)")]
    [SerializeField] CanvasGroup wholeCanvas;     // CanvasGroup di root panel Card Management
    [SerializeField] RectTransform ownedPanel;    // panel kiri (ScrollView root)
    [SerializeField] RectTransform pickedPanel;   // panel bawah
    [SerializeField] RectTransform detailPanel;   // panel kanan
    [SerializeField] RectTransform headerGroup;   // grup kanan atas (energy text + Next)

    [Header("Timing")]
    [SerializeField, Min(0.1f)] float durationSlide = 0.55f;
    [SerializeField, Min(0.0f)] float durationPop = 0.42f;
    [SerializeField, Min(0.0f)] float stagger = 0.08f;

    [Header("SFX (opsional)")]
    [SerializeField] string sfxWhooshKey = "UIWhoosh";

    void OnEnable()
    {
        if (gameObject.activeInHierarchy) StartCoroutine(PlayIntro());
    }

    IEnumerator PlayIntro()
    {
        if (!wholeCanvas) yield break;

        // block input selama intro biar hover/drag tidak "nyangkut"
        bool prevInteractable = wholeCanvas.interactable;
        wholeCanvas.interactable = false;
        if (!Mathf.Approximately(wholeCanvas.alpha, 1f)) wholeCanvas.alpha = 1f;

        // simpan posisi/scale asli
        Vector2 ownedOrig = ownedPanel ? ownedPanel.anchoredPosition : Vector2.zero;
        Vector2 detailOrig = detailPanel ? detailPanel.anchoredPosition : Vector2.zero;
        Vector3 pickedOrig = pickedPanel ? pickedPanel.localScale : Vector3.one;
        Vector3 headerOrig = headerGroup ? headerGroup.localScale : Vector3.one;

        // set start pose (sedikit off-screen + kecil)
        if (ownedPanel) ownedPanel.anchoredPosition = ownedOrig + new Vector2(-120f, 0f);
        if (detailPanel) detailPanel.anchoredPosition = detailOrig + new Vector2(120f, 0f);
        if (pickedPanel) pickedPanel.localScale = Vector3.one * .82f;
        if (headerGroup) headerGroup.localScale = Vector3.one * .82f;

        // sfx
        if (!string.IsNullOrEmpty(sfxWhooshKey) && AudioManager.I) AudioManager.I.PlayUI(sfxWhooshKey);

        // slide-in kiri/kanan
        float t = 0f;
        while (t < durationSlide)
        {
            t += Time.deltaTime;
            float k = EaseOutCubic(Mathf.Clamp01(t / durationSlide));
            if (ownedPanel) ownedPanel.anchoredPosition = Vector2.Lerp(ownedOrig + new Vector2(-120f, 0f), ownedOrig, k);
            if (detailPanel) detailPanel.anchoredPosition = Vector2.Lerp(detailOrig + new Vector2(120f, 0f), detailOrig, k);
            yield return null;
        }

        yield return new WaitForSeconds(stagger);

        // pop-in untuk picked + header
        t = 0f;
        while (t < durationPop)
        {
            t += Time.deltaTime;
            float k = EaseOutBack(Mathf.Clamp01(t / durationPop));
            if (pickedPanel) pickedPanel.localScale = Vector3.Lerp(Vector3.one * .82f, pickedOrig, k);
            if (headerGroup) headerGroup.localScale = Vector3.Lerp(Vector3.one * .82f, headerOrig, k);
            yield return null;
        }

        wholeCanvas.interactable = prevInteractable;
    }

    static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float y = x - 1f;
        return 1 + c3 * y * y * y + c1 * y * y;
    }
}
