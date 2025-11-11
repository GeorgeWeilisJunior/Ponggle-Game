using DG.Tweening;
using UnityEngine;

public class StylePopupItem : MonoBehaviour
{
    RectTransform rt; CanvasGroup cg;

    public void Play(float duration, Vector2 drift, bool destroyOnEnd)
    {
        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        var seq = DOTween.Sequence();
        seq.Join(rt.DOAnchorPos(rt.anchoredPosition + drift, duration).SetEase(Ease.OutQuad));
        seq.Join(cg.DOFade(0f, duration));
        if (destroyOnEnd) seq.OnComplete(() => Destroy(gameObject));

        // fallback kalau tween pernah ter-kill
        Destroy(gameObject, duration + 0.6f);
    }
}
