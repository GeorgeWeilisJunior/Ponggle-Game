using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class KillZoneArea : MonoBehaviour
{
    public enum HideMode { AlphaFade, DisableRenderer, DeactivateRoot }

    [Header("Visual (Sprite & Pulse)")]
    public SpriteRenderer sprite;
    public bool pulse = true;
    [Min(0.1f)] public float pulseSpeed = 2f;
    [Range(0f, 1f)] public float pulseMinAlpha = 0.35f;
    [Range(0f, 1f)] public float pulseMaxAlpha = 0.8f;

    [Header("Appear / Disappear")]
    public bool toggleVisibility = true;
    [Min(0f)] public float visibleTime = 3f;
    [Min(0f)] public float hiddenTime = 2f;
    public bool startHidden = false;
    public bool disableColliderWhenHidden = true;

    [Tooltip("Bagaimana cara menyembunyikan saat Hidden.")]
    public HideMode hideMode = HideMode.AlphaFade;

    [Tooltip("Fade saat transisi (untuk AlphaFade).")]
    public bool fadeWhenToggling = true;
    [Min(0f)] public float fadeDuration = 0.35f;
    public AnimationCurve fadeCurve = AnimationCurve.Linear(0, 0, 1, 1);
    public float randomStartOffsetRange = 0.5f;

    [Header("Collider Helper")]
    public bool autoFitColliderToSprite = true;

    [Header("SFX / VFX (opsional)")]
    public string sfxEnterKey = "KillZoneEnter";
    public ParticleSystem burstVfx;
    public float sfxCooldown = 0.1f;

    [Header("Gizmo")]
    public Color gizmoColor = new Color(1f, 0.25f, 0.15f, 0.2f);

    [Header("Visual Roots (optional)")]
    [Tooltip("Kosongkan untuk auto. Isi untuk membatasi mana yang dimatikan/ditampilkan (beserta semua child-nya).")]
    public List<Transform> visualRoots = new List<Transform>();

    // ---- internals ----
    Collider2D col;
    AudioSource _src;
    float _lastSfxTime;

    readonly List<SpriteRenderer> _allSprites = new();
    readonly List<ParticleSystem> _allParticles = new();
    readonly List<Renderer> _allRenderers = new();

    Coroutine _loopCo;
    bool _isVisible;
    float _visibleAlpha = 1f;

    Transform _visualRoot; // parent untuk DeactivateRoot

    void Reset()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
        if (string.IsNullOrEmpty(gameObject.tag)) gameObject.tag = "KillZone";
        sprite = GetComponentInChildren<SpriteRenderer>();
    }

    void Awake()
    {
        col = GetComponent<Collider2D>();
        if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>();

        CacheVisuals();
        if (autoFitColliderToSprite) FitColliderToSprite();
    }

    void OnEnable()
    {
        StartVisibilityLoop();
    }

    void OnDisable()
    {
        if (_loopCo != null) StopCoroutine(_loopCo);
    }

    void OnValidate()
    {
        if (!col) col = GetComponent<Collider2D>();
        if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>();
        CacheVisuals();
        if (autoFitColliderToSprite) FitColliderToSprite();
    }

    void Update()
    {
        if (!pulse || !_isVisible || hideMode != HideMode.AlphaFade) return;

        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float a = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, t) * _visibleAlpha;
        ApplyAlphaToAll(a);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (Time.time - _lastSfxTime >= sfxCooldown)
        {
            _lastSfxTime = Time.time;
            if (!string.IsNullOrEmpty(sfxEnterKey))
                AudioManager.I?.Play(sfxEnterKey, transform.position); // 3D SFX terkontrol
                                                                       // kalau mau 2D/flat, pakai: AudioManager.I?.PlayUI(sfxEnterKey);
        }
        if (burstVfx) burstVfx.Play();
    }

    /* ---------- Visibility ---------- */
    void StartVisibilityLoop()
    {
        if (_loopCo != null) StopCoroutine(_loopCo);

        SetVisible(!startHidden, true);

        if (!toggleVisibility) return;
        _loopCo = StartCoroutine(VisibilityLoop());
    }

    IEnumerator VisibilityLoop()
    {
        float period = Mathf.Max(0.01f, visibleTime + hiddenTime);
        if (randomStartOffsetRange > 0f)
        {
            float off = Random.Range(0f, Mathf.Clamp01(randomStartOffsetRange)) * period;
            yield return new WaitForSeconds(off);
        }

        while (true)
        {
            if (_isVisible)
            {
                if (visibleTime > 0f) yield return new WaitForSeconds(visibleTime);
                yield return Toggle(false);
            }
            else
            {
                if (hiddenTime > 0f) yield return new WaitForSeconds(hiddenTime);
                yield return Toggle(true);
            }
        }
    }

    IEnumerator Toggle(bool show)
    {
        if (hideMode != HideMode.AlphaFade || !fadeWhenToggling || fadeDuration <= 0f)
        {
            SetVisible(show, true);
            yield break;
        }

        float start = CurrentAlpha();
        float end = show ? 1f : 0f;
        float t = 0f;

        if (!show && disableColliderWhenHidden && col) col.enabled = false;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / fadeDuration);
            float k = fadeCurve.Evaluate(u);
            float a = Mathf.Lerp(start, end, k);
            ApplyAlphaToAll(a);
            yield return null;
        }

        SetVisible(show, true);
    }

    void SetVisible(bool v, bool instant)
    {
        _isVisible = v;
        _visibleAlpha = v ? 1f : 0f;

        if (disableColliderWhenHidden && col) col.enabled = v;

        switch (hideMode)
        {
            case HideMode.AlphaFade:
                ApplyAlphaToAll(_visibleAlpha);
                SetRenderersEnabled(true); // renderer tetap ON, alpha yang diubah
                SetParticlesActive(v);
                break;

            case HideMode.DisableRenderer:
                // matikan/nyalakan semua renderer, dan jaga alpha ke nilai visible (agar saat ON langsung tampak)
                ApplyAlphaToAll(v ? pulseMaxAlpha : 0f);
                SetRenderersEnabled(v);
                SetParticlesActive(v);
                break;
            case HideMode.DeactivateRoot:
                if (visualRoots != null && visualRoots.Count > 0)
                {
                    foreach (var rt in visualRoots) if (rt) rt.gameObject.SetActive(v);
                }
                else
                {
                    if (!_visualRoot) _visualRoot = (sprite ? sprite.transform : transform);
                    if (_visualRoot) _visualRoot.gameObject.SetActive(v);
                }
                break;
        }
    }

    /* ---------- Helpers ---------- */
    void CacheVisuals()
    {
        _allSprites.Clear();
        _allParticles.Clear();
        _allRenderers.Clear();

        if (visualRoots != null && visualRoots.Count > 0)
        {
            foreach (var t in visualRoots)
            {
                if (!t) continue;
                t.GetComponentsInChildren<SpriteRenderer>(true, _allSprites);
                t.GetComponentsInChildren<ParticleSystem>(true, _allParticles);
                t.GetComponentsInChildren<Renderer>(true, _allRenderers);
            }
        }
        else
        {
            GetComponentsInChildren<SpriteRenderer>(true, _allSprites);
            GetComponentsInChildren<ParticleSystem>(true, _allParticles);
            GetComponentsInChildren<Renderer>(true, _allRenderers);
        }

        if (!sprite && _allSprites.Count > 0) sprite = _allSprites[0];

        // visual root default untuk DeactivateRoot
        _visualRoot = (visualRoots != null && visualRoots.Count > 0)
            ? visualRoots[0]
            : (sprite ? sprite.transform : transform);
    }


    float CurrentAlpha()
    {
        if (_allSprites.Count == 0) return 0f;
        return _allSprites[0].color.a;
    }

    void ApplyAlphaToAll(float a)
    {
        for (int i = 0; i < _allSprites.Count; i++)
        {
            var sr = _allSprites[i];
            if (!sr) continue;
            var c = sr.color;
            c.a = Mathf.Clamp01(a);
            sr.color = c;
        }
    }

    void SetRenderersEnabled(bool enabled)
    {
        for (int i = 0; i < _allRenderers.Count; i++)
        {
            var r = _allRenderers[i];
            if (!r) continue;
            r.enabled = enabled;
        }
    }

    void SetParticlesActive(bool active)
    {
        for (int i = 0; i < _allParticles.Count; i++)
        {
            var ps = _allParticles[i];
            if (!ps) continue;
            var emission = ps.emission;
            emission.enabled = active;
            if (active) ps.Play(); else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        if (burstVfx)
        {
            var e = burstVfx.emission; e.enabled = active;
            if (!active) burstVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void FitColliderToSprite()
    {
        if (!sprite || !col) return;

        var b = sprite.bounds; // world-space
        var lossy = transform.lossyScale;
        Vector2 sizeLocal = new(
            Mathf.Approximately(lossy.x, 0) ? 0 : b.size.x / Mathf.Abs(lossy.x),
            Mathf.Approximately(lossy.y, 0) ? 0 : b.size.y / Mathf.Abs(lossy.y)
        );

        if (col is BoxCollider2D box)
        {
            box.size = sizeLocal;
            box.offset = Vector2.zero;
        }
        else if (col is CircleCollider2D cc)
        {
            float rWorld = Mathf.Max(b.extents.x, b.extents.y);
            float rLocal = rWorld / Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y));
            cc.radius = rLocal;
            cc.offset = Vector2.zero;
        }
        else if (col is CapsuleCollider2D cap)
        {
            cap.size = sizeLocal;
            cap.offset = Vector2.zero;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        var c = GetComponent<Collider2D>();
        if (!c) return;

        if (c is BoxCollider2D bc)
        {
            var pos = transform.TransformPoint(bc.offset);
            var size = Vector3.Scale(bc.size, transform.lossyScale);
            Gizmos.DrawCube(pos, size);
        }
        else if (c is CircleCollider2D cc)
        {
            var pos = transform.TransformPoint(cc.offset);
            float r = cc.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
            Gizmos.DrawSphere(pos, r);
        }
        else if (c is CapsuleCollider2D cap)
        {
            var pos = transform.TransformPoint(cap.offset);
            var size = new Vector3(
                cap.size.x * Mathf.Abs(transform.lossyScale.x),
                cap.size.y * Mathf.Abs(transform.lossyScale.y),
                0f
            );
            Gizmos.DrawCube(pos, size);
        }
    }
}
