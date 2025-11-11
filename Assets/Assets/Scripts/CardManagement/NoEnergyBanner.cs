using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class NoEnergyBanner : MonoBehaviour
{
    [Header("Optional References")]
    [SerializeField] TMP_Text label;   // kalau pakai text
    [SerializeField] Image bannerImage; // kalau pakai image

    [Header("Behaviour")]
    [SerializeField] float showSeconds = 1.2f;
    [SerializeField] float shakeTime = .35f;
    [SerializeField] float shakeAmp = 12f;
    [SerializeField] string sfxKey = "UIReject";

    CanvasGroup cg;
    RectTransform rt;
    Coroutine playCo;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        rt = GetComponent<RectTransform>();
        if (cg)
        {
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// Panggil untuk memunculkan banner dengan shake.
    /// </summary>
    public void Pulse(string overrideText = "")
    {
        if (playCo != null) StopCoroutine(playCo);
        playCo = StartCoroutine(PulseRoutine(overrideText));
    }

    IEnumerator PulseRoutine(string text)
    {
        // kalau ada TMP_Text, update isi
        if (label && !string.IsNullOrEmpty(text))
            label.text = text;

        if (AudioManager.I && !string.IsNullOrEmpty(sfxKey))
            AudioManager.I.PlayUI(sfxKey);

        cg.alpha = 1f;

        // shake
        var start = rt.anchoredPosition;
        float t = 0f;
        while (t < shakeTime)
        {
            t += Time.unscaledDeltaTime;
            float s = Mathf.Sin(t / shakeTime * Mathf.PI * 6f) * shakeAmp;
            rt.anchoredPosition = start + new Vector2(s, 0);
            yield return null;
        }
        rt.anchoredPosition = start;

        // tunggu sisa waktu
        float hold = Mathf.Max(0f, showSeconds - shakeTime);
        if (hold > 0f) yield return new WaitForSecondsRealtime(hold);

        // fade out
        float fade = .2f; t = 0f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(1f, 0f, t / fade);
            yield return null;
        }
        cg.alpha = 0f;
    }
}
