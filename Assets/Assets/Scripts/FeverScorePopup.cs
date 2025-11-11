using TMPro;
using UnityEngine;
using DG.Tweening;        // hapus jika tak pakai DOTween

public class FeverScorePopup : MonoBehaviour
{
    [SerializeField] TMP_Text valueTxt;
    [SerializeField] TMP_Text subTxt;      // opsional
    [SerializeField] float showTime = 2f;

    public void Show(int feverScore, int leftover, int perBall)
    {
        valueTxt.text = feverScore.ToString("N0");

        // ── PERBAIKAN: hanya set jika subTxt terisi ──
        if (subTxt)
            subTxt.text = leftover > 0
                        ? $"+{(leftover * perBall):N0}  ({leftover} Balls Left)"
                        : "";

        gameObject.SetActive(true);

        transform.localScale = Vector3.zero;
        transform.DOScale(1f, .4f).SetEase(Ease.OutBack);

        Invoke(nameof(Hide), showTime);
    }
    void Hide() => gameObject.SetActive(false);
}
