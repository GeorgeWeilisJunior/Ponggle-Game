using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class DisappearingPeg : MonoBehaviour
{
    [Header("Timing")]
    [Min(0f)] public float visibleDuration = 5f;   // lama terlihat penuh
    [Min(0f)] public float hiddenDuration = 2f;   // lama benar2 hilang
    [Min(0.05f)] public float fadeDuration = 0.35f;// lama transisi fade
    [Min(0f)] public float startDelay = 0f;
    public bool startHidden = false;
    [Tooltip("Agar tidak serempak: acak offset fase di awal (0..this).")]
    [Min(0f)] public float randomPhaseJitter = 0.5f;

    [Header("Fade Curve (0..1)")]
    [Tooltip("Kurva 0→1 untuk fade-in (dipakai terbalik untuk fade-out).")]
    public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Renderers (kosong = auto cari di children)")]
    public List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    [Range(0f, 1f)] public float hiddenAlpha = 0f;  // target alpha saat hilang

    [Header("VFX (opsional)")]
    [Tooltip("Prefab kecil saat MUNCUL (mis. sparkle tipis).")]
    public ParticleSystem appearVfxPrefab;
    [Tooltip("Prefab kecil saat MENGHILANG (mis. puff asap tipis).")]
    public ParticleSystem disappearVfxPrefab;
    [Tooltip("SFX (opsional)")]
    public string appearSfxKey = "";
    public string disappearSfxKey = "";

    Collider2D col;
    PegController peg; // opsional, kalau ada
    struct SRState { public SpriteRenderer sr; public Color baseColor; }
    List<SRState> states = new();

    Coroutine loopCo;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        peg = GetComponent<PegController>();

        if (renderers == null || renderers.Count == 0)
            renderers = new List<SpriteRenderer>(GetComponentsInChildren<SpriteRenderer>(true));

        foreach (var sr in renderers)
        {
            if (!sr) continue;
            states.Add(new SRState { sr = sr, baseColor = sr.color });
        }
    }

    void OnEnable()
    {
        if (loopCo != null) StopCoroutine(loopCo);
        loopCo = StartCoroutine(RunLoop());
    }

    void OnDisable()
    {
        if (loopCo != null) StopCoroutine(loopCo);
        // safety: kembalikan ke visible penuh saat dimatikan (opsional)
        SetAlphaOnAll(1f);
        if (col) col.enabled = true;
    }

    IEnumerator RunLoop()
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);
        if (randomPhaseJitter > 0f) yield return new WaitForSeconds(Random.Range(0f, randomPhaseJitter));

        bool visible = !startHidden;
        // set state awal
        if (visible) { SetAlphaOnAll(1f); if (col) col.enabled = true; }
        else { SetAlphaOnAll(hiddenAlpha); if (col) col.enabled = false; }

        while (enabled && (!peg || peg.State != PegController.PegState.Cleared))
        {
            // 1) Tahan pada state sekarang (visible/hidden)
            if (visible && visibleDuration > 0f) yield return new WaitForSeconds(visibleDuration);
            if (!visible && hiddenDuration > 0f) yield return new WaitForSeconds(hiddenDuration);

            // 2) Transisi ke state sebaliknya (fade)
            if (fadeDuration > 0f)
                yield return FadeTo(visible ? hiddenAlpha : 1f, fadeDuration, visible);
            else
            {
                SetAlphaOnAll(visible ? hiddenAlpha : 1f);
            }

            // 3) Toggle collider setelah fade selesai
            visible = !visible;
            if (col) col.enabled = visible;
        }
    }

    IEnumerator FadeTo(float targetAlpha, float duration, bool wasVisible)
    {
        // VFX + SFX di awal transisi
        if (wasVisible)
        {
            // sedang menghilang
            if (disappearVfxPrefab) Instantiate(disappearVfxPrefab, transform.position, Quaternion.identity, transform.parent);
            if (!string.IsNullOrEmpty(disappearSfxKey) && AudioManager.I) AudioManager.I.Play(disappearSfxKey, transform.position);
        }
        else
        {
            // sedang muncul
            if (appearVfxPrefab) Instantiate(appearVfxPrefab, transform.position, Quaternion.identity, transform.parent);
            if (!string.IsNullOrEmpty(appearSfxKey) && AudioManager.I) AudioManager.I.Play(appearSfxKey, transform.position);
        }

        float startA = GetCurrentAlpha();
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float k = fadeCurve.Evaluate(u); // 0..1
            float a = Mathf.Lerp(startA, targetAlpha, k);
            SetAlphaOnAll(a);

            // matikan collider saat hampir tak terlihat agar “aman”
            if (col) col.enabled = a > 0.5f;
            yield return null;
        }
        SetAlphaOnAll(targetAlpha);
    }

    float GetCurrentAlpha()
    {
        // ambil alpha dari renderer pertama yang valid
        foreach (var st in states)
        {
            if (st.sr) return st.sr.color.a;
        }
        return 1f;
    }

    void SetAlphaOnAll(float a)
    {
        for (int i = 0; i < states.Count; i++)
        {
            var st = states[i];
            if (!st.sr) continue;
            var c = st.baseColor; c.a = a;
            st.sr.color = c;
        }
    }
}
