using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class LoadingUIAnimator : MonoBehaviour
{
    [Header("Ball & Shadow Refs")]
    [SerializeField] private RectTransform ball;             // Ball (UI)
    [SerializeField] private RectTransform shadow;           // Shadow (UI)
    [SerializeField] private Image shadowImage;              // Image di Shadow (optional)

    [Header("Heights (UI coordinates)")]
    [Tooltip("Ketinggian lantai (Y) tempat bola menyentuh tanah.")]
    [SerializeField] private float groundY = -70f;
    [Tooltip("Ketinggian puncak lompatan bola.")]
    [SerializeField] private float topY = 420f;

    [Header("Physics Feel")]
    [SerializeField] private float gravity = -2600f;         // negatif = jatuh
    [SerializeField] private float bounce = 0.78f;           // dipakai jika constantBounceHeight = false
    [SerializeField] private float minUpVelocity = 200f;     // fallback minimum
    [SerializeField] private float initialDownSpeed = -100f; // mulai turun

    [Header("Constant Bounce Height")]
    [SerializeField] private bool constantBounceHeight = true; // << NYALA supaya tinggi pantulan konstan
    [Tooltip("Jika ON, tinggi pantulan selalu 'topY - groundY'. Kecepatan di-hitungan otomatis dari gravitasi.")]
    [SerializeField] private bool recomputeVelocityOnChange = true;

    [Header("Impact Squash")]
    [SerializeField] private float baseSquash = 0.12f;
    [SerializeField] private float squashPerSpeed = 0.0004f;
    [SerializeField] private float squashDuration = 0.12f;

    [Header("Shadow Behaviour (besar saat dekat tanah)")]
    [SerializeField] private bool lockShadowYAtStart = true;
    [SerializeField] private Vector2 shadowScaleGround = new Vector2(1.25f, 0.7f);
    [SerializeField] private Vector2 shadowScaleAir = new Vector2(0.55f, 0.35f);
    [SerializeField] private float shadowLerpSmooth = 12f;
    [SerializeField] private float shadowAlphaGround = 0.45f;
    [SerializeField] private float shadowAlphaAir = 0.12f;

    [Header("Text: Loading Dots")]
    [SerializeField] private Text legacyText;                 // optional
    [SerializeField] private TextMeshProUGUI tmpText;         // TMP
    [SerializeField] private string baseText = "Now Loading";
    [SerializeField] private int maxDots = 3;
    [SerializeField] private float dotInterval = 0.3f;
    [SerializeField] private bool dotsUseFade = true;
    [SerializeField] private float dotFadeDuration = 0.15f;

    [Header("Control")]
    [SerializeField] private bool playOnEnable = true;

    // runtime
    float vy;                 // velocity Y
    bool squashing;
    float shadowYFixed;
    Coroutine dotsCo, dotFadeCo;
    float cachedUpVelocity;   // kecepatan naik untuk capai topY saat constantBounceHeight

    void OnValidate()
    {
        if (recomputeVelocityOnChange) RecomputeConstantUpVelocity();
    }

    void OnEnable()
    {
        if (playOnEnable) StartAnim();
    }

    void OnDisable()
    {
        StopAnim();
    }

    public void StartAnim()
    {
        RecomputeConstantUpVelocity();
        vy = initialDownSpeed;
        squashing = false;

        if (ball)
        {
            var p = ball.anchoredPosition;
            p.y = Mathf.Lerp(groundY, topY, 0.6f);
            ball.anchoredPosition = p;
            ball.localScale = Vector3.one;
        }

        if (shadow)
        {
            shadowYFixed = lockShadowYAtStart ? shadow.anchoredPosition.y : groundY;
            SetShadowScale(shadowScaleAir);
            SetShadowAlpha(shadowAlphaAir);
        }

        if (dotsCo == null) dotsCo = StartCoroutine(DotsRoutine());
        enabled = true;
    }

    public void StopAnim()
    {
        if (dotsCo != null) { StopCoroutine(dotsCo); dotsCo = null; }
        if (dotFadeCo != null) { StopCoroutine(dotFadeCo); dotFadeCo = null; }
        enabled = false;
    }

    void RecomputeConstantUpVelocity()
    {
        // v = sqrt(2 * |g| * h), h = topY - groundY
        float height = Mathf.Max(0f, topY - groundY);
        float g = Mathf.Min(-1f, gravity); // pastikan negatif
        cachedUpVelocity = Mathf.Sqrt(2f * -g * height);
        if (cachedUpVelocity < minUpVelocity) cachedUpVelocity = minUpVelocity;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (!ball) return;

        vy += gravity * dt;
        var pos = ball.anchoredPosition;
        pos.y += vy * dt;

        // Puncak
        if (pos.y > topY)
        {
            pos.y = topY;
            if (vy > 0f) vy = 0f; // biar mulai turun
        }

        // Tanah
        if (pos.y <= groundY)
        {
            pos.y = groundY;

            // kecepatan impact untuk squash
            float impactSpeed = Mathf.Abs(vy);

            // pantul
            if (constantBounceHeight)
            {
                vy = cachedUpVelocity; // selalu sama / tetap
            }
            else
            {
                vy = Mathf.Max(minUpVelocity, impactSpeed * bounce);
            }

            if (!squashing)
            {
                // kalau constant, squash juga konstan (berdasar cachedUpVelocity)
                float speedForSquash = constantBounceHeight ? cachedUpVelocity : impactSpeed;
                float squashAmt = Mathf.Clamp(baseSquash + speedForSquash * squashPerSpeed, 0.08f, 0.28f);
                StartCoroutine(SquashRoutine(squashAmt));
            }

            vy = Mathf.Abs(vy); // pastikan ke atas
        }

        ball.anchoredPosition = pos;

        // Shadow: X ikut bola, Y fix di "lantai"
        if (shadow)
        {
            var sp = shadow.anchoredPosition;
            sp.x = ball.anchoredPosition.x;
            sp.y = shadowYFixed;
            shadow.anchoredPosition = sp;

            // 0=tanah (besar), 1=atas (kecil)
            float h = Mathf.InverseLerp(groundY, topY, pos.y);
            Vector2 targetScale = Vector2.Lerp(shadowScaleGround, shadowScaleAir, h);
            float targetAlpha = Mathf.Lerp(shadowAlphaGround, shadowAlphaAir, h);

            // smoothing
            float k = 1f - Mathf.Exp(-shadowLerpSmooth * dt);
            Vector2 cur = shadow.localScale;
            cur.x = Mathf.Lerp(cur.x, targetScale.x, k);
            cur.y = Mathf.Lerp(cur.y, targetScale.y, k);
            SetShadowScale(cur);

            float a = GetShadowAlpha();
            a = Mathf.Lerp(a, targetAlpha, k);
            SetShadowAlpha(a);
        }
    }

    IEnumerator SquashRoutine(float squashAmount)
    {
        squashing = true;
        float half = Mathf.Max(0.02f, squashDuration * 0.5f);

        yield return ScaleOverTime(ball, Vector3.one,
            new Vector3(1f + squashAmount, 1f - (squashAmount * 0.7f), 1f), half);

        yield return ScaleOverTime(ball, ball.localScale, Vector3.one, half);
        squashing = false;
    }

    IEnumerator ScaleOverTime(Transform t, Vector3 from, Vector3 to, float dur)
    {
        if (!t) yield break;
        float s = 0f;
        while (s < dur)
        {
            s += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(s / dur), 3f); // EaseOutCubic
            t.localScale = Vector3.Lerp(from, to, k);
            yield return null;
        }
        t.localScale = to;
    }

    // ------- Shadow helpers -------
    void SetShadowScale(Vector2 sc)
    {
        if (!shadow) return;
        shadow.localScale = new Vector3(sc.x, sc.y, 1f);
    }

    float GetShadowAlpha()
    {
        if (!shadowImage) return 1f;
        return shadowImage.color.a;
    }

    void SetShadowAlpha(float a)
    {
        if (!shadowImage) return;
        var c = shadowImage.color; c.a = a; shadowImage.color = c;
    }

    // ------- Dots text -------
    IEnumerator DotsRoutine()
    {
        int dots = 0;
        while (true)
        {
            string t = baseText + new string('.', dots);
            if (tmpText) tmpText.text = t;
            if (legacyText) legacyText.text = t;

            if (dotsUseFade && dotFadeDuration > 0f)
            {
                if (dotFadeCo != null) StopCoroutine(dotFadeCo);
                dotFadeCo = StartCoroutine(FadeTextOnce(dotFadeDuration));
            }

            dots = (dots + 1) % (maxDots + 1);
            yield return new WaitForSecondsRealtime(dotInterval);
        }
    }

    IEnumerator FadeTextOnce(float dur)
    {
        float t = 0f;
        Color? tmp0 = null, ui0 = null;

        if (tmpText) { tmp0 = tmpText.color; var c = tmpText.color; c.a = 0f; tmpText.color = c; }
        if (legacyText) { ui0 = legacyText.color; var c = legacyText.color; c.a = 0f; legacyText.color = c; }

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / dur);
            if (tmpText && tmp0.HasValue) { var c = tmp0.Value; c.a = a; tmpText.color = c; }
            if (legacyText && ui0.HasValue) { var c = ui0.Value; c.a = a; legacyText.color = c; }
            yield return null;
        }

        if (tmpText && tmp0.HasValue) { var c = tmp0.Value; c.a = 1f; tmpText.color = c; }
        if (legacyText && ui0.HasValue) { var c = ui0.Value; c.a = 1f; legacyText.color = c; }
    }
}
