using System.Collections;
using TMPro;
using UnityEngine;

public class ScoreUI : MonoBehaviour
{
    public static ScoreUI Instance { get; private set; }

    [Header("Shot Score Popup (dua / atau satu komponen)")]
    [SerializeField] TMP_Text shotLineText;   // "100 × 7 pegs"
    [SerializeField] TMP_Text totalLineText;  // "700"
    [SerializeField] TMP_Text singleText;     // alternatif 1 komponen (dua baris)

    [Header("Durasi Rekap")]
    [Tooltip("Total waktu untuk menghitung 0→N (bukan per langkah).")]
    [SerializeField, Min(0.1f)] float perPegCountDuration = 0.6f;

    [Tooltip("Waktu tahan setelah hitungan selesai, sebelum popup hilang.")]
    [SerializeField, Min(0f)] float holdAfterCount = 0.6f;

    [Header("SFX Tiap Tick (opsional)")]
    [SerializeField] string tickSfxKey = "";

    [Header("Style Shot Popup")]
    [SerializeField] TMP_Text stylePopupPrefab;        // prefab TMP_Text
    [SerializeField] RectTransform stylePopupParent;   // biasanya GameCanvas
    [SerializeField, Min(0.1f)] float stylePopupDuration = 1.2f;
    [SerializeField] Vector2 stylePopupDrift = new(0f, 30f);

    Canvas parentCanvas;
    Camera uiCam;
    Coroutine shotRoutine;   // coroutine rekap popup

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        var rt = GetComponent<RectTransform>();
        if (rt) rt.localScale = Vector3.one;

        parentCanvas = stylePopupParent ? stylePopupParent.GetComponentInParent<Canvas>()
                                        : GetComponentInParent<Canvas>();
        if (parentCanvas)
            uiCam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;
    }

    // === DIPAKAI GameManager via: yield return StartCoroutine(ScoreUI.Instance.PlayLastShotPopup());
    public IEnumerator PlayLastShotPopup()
    {
        int pegs = ScoreManager.GetPegCount();
        if (pegs <= 0) yield break;

        int shotScore = ScoreManager.ShotPoints;
        int basePts = Mathf.Max(0, pegs > 0 ? shotScore / pegs : 0);

        // Pastikan teks aktif
        if (shotLineText) shotLineText.gameObject.SetActive(true);
        if (totalLineText) totalLineText.gameObject.SetActive(true);
        if (!shotLineText && !totalLineText && singleText) singleText.gameObject.SetActive(true);

        // Jalankan hitungan step by step (0→N)
        if (shotRoutine != null) StopCoroutine(shotRoutine);
        if (shotLineText || totalLineText)
            shotRoutine = StartCoroutine(StepCountTwoTexts(shotLineText, totalLineText, basePts, pegs));
        else
            shotRoutine = StartCoroutine(StepCountSingleText(singleText, basePts, pegs));

        // Tunggu sampai selesai menghitung
        yield return shotRoutine;

        // Tahan sebentar agar terbaca
        if (holdAfterCount > 0f)
            yield return new WaitForSecondsRealtime(holdAfterCount);

        // Sembunyikan
        HideAll();
    }

    IEnumerator StepCountTwoTexts(TMP_Text shotLine, TMP_Text totalLine, int basePts, int targetPegs)
    {
        int shownPegs = 0;
        float perStep = Mathf.Max(0.03f, perPegCountDuration / Mathf.Max(1, targetPegs));

        while (shownPegs < targetPegs)
        {
            shownPegs++;
            int subtotal = basePts * shownPegs;

            if (shotLine) shotLine.text = $"{basePts:N0} × {shownPegs} pegs";
            if (totalLine) totalLine.text = $"{subtotal:N0}";

            if (!string.IsNullOrEmpty(tickSfxKey))
                AudioManager.I?.PlayUI(tickSfxKey);

            yield return new WaitForSecondsRealtime(perStep);
        }
    }

    IEnumerator StepCountSingleText(TMP_Text t, int basePts, int targetPegs)
    {
        int shownPegs = 0;
        float perStep = Mathf.Max(0.03f, perPegCountDuration / Mathf.Max(1, targetPegs));

        while (shownPegs < targetPegs)
        {
            shownPegs++;
            int subtotal = basePts * shownPegs;
            if (t) t.text = $"{basePts:N0} × {shownPegs} pegs\n{subtotal:N0}";

            if (!string.IsNullOrEmpty(tickSfxKey))
                AudioManager.I?.PlayUI(tickSfxKey);

            yield return new WaitForSecondsRealtime(perStep);
        }
    }

    void HideAll()
    {
        if (shotLineText) shotLineText.gameObject.SetActive(false);
        if (totalLineText) totalLineText.gameObject.SetActive(false);
        if (singleText) singleText.gameObject.SetActive(false);
    }

    // ========== STYLE POPUP ==========
    public void ShowStyle(string label, int pts, Vector3 worldPos)
    {
        if (!stylePopupPrefab || !stylePopupParent) return;

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCam, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(stylePopupParent, screen, uiCam, out Vector2 local);

        var inst = Instantiate(stylePopupPrefab, stylePopupParent);
        if (!inst.gameObject.activeSelf) inst.gameObject.SetActive(true);

        inst.text = $"{label}\n+{pts:N0}";
        var rt = inst.rectTransform;
        rt.anchoredPosition = local;

        inst.color = RandomColor();

        var cg = inst.GetComponent<CanvasGroup>();
        if (!cg) cg = inst.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        var item = inst.gameObject.AddComponent<StylePopupItem>();
        item.Play(stylePopupDuration, stylePopupDrift, destroyOnEnd: true);
    }

    Color RandomColor()
    {
        return Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.8f, 1f);
    }
}
