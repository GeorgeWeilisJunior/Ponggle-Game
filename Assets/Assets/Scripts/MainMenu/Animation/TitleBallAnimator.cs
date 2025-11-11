using System.Collections;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class TitleBallAnimator : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] RectTransform socket;       // titik akhir 'O'
    [SerializeField] RectTransform word;         // opsional (tak dipakai krusial)

    [Header("Drop & Bounce")]
    [SerializeField] float startYOffset = 220f;   // jarak awal di atas socket (px)
    [SerializeField] float gravity = -4200f; // px/s^2 (UI = pixel)
    [SerializeField] float bounceDamping = 0.42f;  // 0..1 (semakin kecil, semakin pendek pantul)
    [SerializeField] float minBounceSpeed = 520f;   // kalau sudah di bawah ini → selesai
    [SerializeField] int maxBounces = 3;      // maksimum jumlah pantul
    [SerializeField] float initialDownVelocity = 900f; // dorong awal biar jatuh lebih cepat

    [Header("Clamp Tinggi Pantul")]
    [SerializeField] float maxBounceUpSpeed = 1200f; // batasi seberapa tinggi pantul pertama/kedua

    [Header("Global Speed")]
    [SerializeField] float timeMultiplier = 1.35f;   // >1 lebih cepat, <1 lebih lambat

    [Header("Squash / FX")]
    [SerializeField] float squashScaleX = 1.15f;
    [SerializeField] float squashScaleY = 0.85f;
    [SerializeField] float squashTime = 0.06f;
    [SerializeField] float settleTilt = 6f;
    [SerializeField] string bounceSfxKey = "";       // isi mis: "UIBounce"

    RectTransform rt;
    Vector2 restPos;
    Vector3 defaultScale;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        defaultScale = rt.localScale;
    }

    void OnEnable()
    {
        if (!socket) { Debug.LogWarning("Socket belum di-set"); return; }
        StopAllCoroutines();
        StartCoroutine(Play());
    }

    public IEnumerator Play()
    {
        restPos = socket.anchoredPosition;

        // posisi & kecepatan awal
        Vector2 p = restPos + Vector2.up * startYOffset;
        float vy = -Mathf.Abs(initialDownVelocity);

        rt.anchoredPosition = p;
        rt.localRotation = Quaternion.Euler(0, 0, settleTilt);

        int bounces = 0;

        while (true)
        {
            float dt = Time.unscaledDeltaTime * Mathf.Max(0.01f, timeMultiplier);

            // integrasi sederhana
            vy += gravity * dt;
            p.y += vy * dt;

            // kena "lantai" (socket)
            if (p.y <= restPos.y)
            {
                p.y = restPos.y;

                // FX: squash & SFX
                if (!string.IsNullOrEmpty(bounceSfxKey))
                    AudioManager.I.PlayUI(bounceSfxKey);
                yield return StartCoroutine(SquashFX());

                // pantul ke atas + clamp tinggi pantul
                vy = -vy * bounceDamping;
                if (vy > maxBounceUpSpeed) vy = maxBounceUpSpeed;

                bounces++;

                // selesai kalau sudah pelan atau sudah cukup kali
                if (Mathf.Abs(vy) < minBounceSpeed || bounces >= maxBounces)
                    break;
            }

            rt.anchoredPosition = p;
            yield return null;
        }

        // settle kecil ke posisi persis & rotasi 0
        float t = 0f, dur = .14f / Mathf.Max(0.2f, timeMultiplier);
        Vector2 start = rt.anchoredPosition;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0, 1, t / dur);
            rt.anchoredPosition = Vector2.Lerp(start, restPos, k);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(settleTilt, 0, k));
            rt.localScale = Vector3.Lerp(rt.localScale, defaultScale, k);
            yield return null;
        }

        rt.anchoredPosition = restPos;
        rt.localRotation = Quaternion.identity;
        rt.localScale = defaultScale;
    }

    IEnumerator SquashFX()
    {
        float dur = squashTime / Mathf.Max(0.2f, timeMultiplier);
        float t = 0f;
        Vector3 from = defaultScale;
        Vector3 to = new Vector3(squashScaleX, squashScaleY, 1f);

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = t / dur;
            rt.localScale = Vector3.Lerp(from, to, k);
            yield return null;
        }
        t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = t / dur;
            rt.localScale = Vector3.Lerp(to, defaultScale, k);
            yield return null;
        }
    }
}
