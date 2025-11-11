using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[RequireComponent(typeof(Image))]
public class UIPulseImage : MonoBehaviour
{
    [Header("Scale Pulse")]
    [SerializeField] float scaleMin = 0.96f;
    [SerializeField] float scaleMax = 1.04f;
    [SerializeField] float period = 1.8f;

    [Header("Fade")]
    [SerializeField, Range(0f, 1f)] float alphaMin = 0.40f;
    [SerializeField, Range(0f, 1f)] float alphaMax = 1.00f;
    [SerializeField] bool useUnscaledTime = false;

    Image img;
    Tween tScale, tFade;

    void Awake() { img = GetComponent<Image>(); }

    void OnEnable()
    {
        KillTweens();

        // start dari nilai minimum, lalu yoyo
        transform.localScale = Vector3.one * scaleMin;
        var c = img.color; c.a = alphaMin; img.color = c;

        tScale = transform.DOScale(scaleMax, period * 0.5f)
                          .SetEase(Ease.InOutSine)
                          .SetLoops(-1, LoopType.Yoyo)
                          .SetUpdate(useUnscaledTime)
                          .SetLink(gameObject);

        tFade = img.DOFade(alphaMax, period * 0.5f)
                      .From(alphaMin)
                      .SetEase(Ease.InOutSine)
                      .SetLoops(-1, LoopType.Yoyo)
                      .SetUpdate(useUnscaledTime)
                      .SetLink(gameObject);
    }

    void OnDisable() => KillTweens();

    void KillTweens()
    {
        tScale?.Kill(); tScale = null;
        tFade?.Kill(); tFade = null;
    }
}
