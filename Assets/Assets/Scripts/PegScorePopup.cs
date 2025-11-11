using DG.Tweening;
using TMPro;
using UnityEngine;

public class PegScorePopup : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] TextMeshProUGUI label;

    [Header("Motion & Timing (default)")]
    [SerializeField] float rise = 0.6f;
    [SerializeField] float duration = 0.6f;

    [Header("Visual (default)")]
    [SerializeField] Color defaultColor = Color.white;

    CanvasGroup _cg;

    void Awake()
    {
        if (!_cg)
        {
            _cg = GetComponent<CanvasGroup>();
            if (!_cg) _cg = gameObject.AddComponent<CanvasGroup>();
        }
        _cg.alpha = 1f;
    }

    // === Versi lama: tetap ada agar kompatibel ===
    public void Show(int amount)
    {
        // Format ribuan: 25 000 → 25,000 (atau sesuai culture)
        string text = amount.ToString("N0");
        Show(text, defaultColor, duration);
    }

    // === Versi baru: mendukung teks bebas + warna + durasi ===
    public void Show(string text, Color color, float customDuration)
    {
        if (!label) label = GetComponentInChildren<TextMeshProUGUI>(true);

        if (label)
        {
            label.text = text;
            label.color = color;
        }

        Vector3 start = transform.position;             // world-space
        Vector3 end = start + Vector3.up * rise;      // naik sedikit

        // Hentikan animasi lama
        transform.DOKill();
        _cg.DOKill();

        // Reset alpha
        _cg.alpha = 1f;

        Sequence s = DOTween.Sequence();
        s.Join(transform.DOMove(end, customDuration).SetEase(Ease.OutQuad));
        s.Join(_cg.DOFade(0f, customDuration));
        s.OnComplete(() => Destroy(gameObject));
    }

    // Overload helper dengan durasi & warna default (opsional)
    public void Show(string text)
    {
        Show(text, defaultColor, duration);
    }
}
