using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class UIClickPulse : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IPointerClickHandler, IBeginDragHandler, IEndDragHandler
{
    [Header("Scale")]
    [Min(0f)] public float pressScale = 0.96f;
    [Min(0f)] public float pressTime = 0.06f;
    [Min(0f)] public float releaseOvershoot = 1.04f;
    [Min(0f)] public float releaseTime = 0.12f;
    [Min(0f)] public float clickPulseScale = 1.08f;
    [Min(0f)] public float clickPulseTime = 0.10f;

    [Header("Rotation (subtle)")]
    public bool useTilt = true;
    [Min(0f)] public float tiltDeg = 3f;

    [Header("SFX (optional)")]
    [SerializeField] public string sfxClickKey = "";   // contoh: "UIButton"

    RectTransform rt;
    Vector3 baseScale;
    Quaternion baseRot;
    bool dragging;
    Coroutine animCo;

    void Awake()
    {
        rt = transform as RectTransform;
        baseScale = rt.localScale;
        baseRot = rt.localRotation;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        dragging = false;
        PlayTo(baseScale * pressScale, useTilt ? Quaternion.Euler(0, 0, Random.Range(-tiltDeg, tiltDeg)) : baseRot, pressTime);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // spring balik (tanpa trigger “klik” di sini)
        PlayTo(baseScale * releaseOvershoot, baseRot, releaseTime, thenToBase: true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (dragging) return; // abaikan jika ini sebenarnya drag
        // pulse klik + sfx
        if (AudioManager.I && !string.IsNullOrEmpty(sfxClickKey)) AudioManager.I.PlayUI(sfxClickKey);
        PlayPulse();
    }

    public void OnBeginDrag(PointerEventData eventData) { dragging = true; }
    public void OnEndDrag(PointerEventData eventData) { /* no-op */ }

    public void PlayPulse()
    {
        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(PulseRoutine());
    }

    IEnumerator PulseRoutine()
    {
        // up to >1
        float t = 0f;
        var fromS = rt.localScale;
        var toS = baseScale * clickPulseScale;
        while (t < clickPulseTime)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(fromS, toS, EaseOutQuad(t / clickPulseTime));
            yield return null;
        }
        // back to base
        t = 0f; fromS = rt.localScale; toS = baseScale;
        while (t < clickPulseTime)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(fromS, toS, EaseInQuad(t / clickPulseTime));
            yield return null;
        }
        rt.localScale = baseScale;
    }

    void PlayTo(Vector3 targetScale, Quaternion targetRot, float dur, bool thenToBase = false)
    {
        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(TweenTo(targetScale, targetRot, dur, thenToBase));
    }

    IEnumerator TweenTo(Vector3 targetScale, Quaternion targetRot, float dur, bool thenToBase)
    {
        float t = 0f;
        Vector3 s0 = rt.localScale; Quaternion r0 = rt.localRotation;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOutQuad(t / dur);
            rt.localScale = Vector3.Lerp(s0, targetScale, k);
            rt.localRotation = Quaternion.Slerp(r0, targetRot, k);
            yield return null;
        }
        rt.localScale = targetScale; rt.localRotation = targetRot;

        if (thenToBase)
        {
            t = 0f; s0 = rt.localScale; r0 = rt.localRotation;
            while (t < dur * 0.8f)
            {
                t += Time.unscaledDeltaTime;
                float k = EaseInQuad(t / (dur * 0.8f));
                rt.localScale = Vector3.Lerp(s0, baseScale, k);
                rt.localRotation = Quaternion.Slerp(r0, baseRot, k);
                yield return null;
            }
            rt.localScale = baseScale; rt.localRotation = baseRot;
        }
    }

    static float EaseOutQuad(float x) => 1 - (1 - x) * (1 - x);
    static float EaseInQuad(float x) => x * x;
}
