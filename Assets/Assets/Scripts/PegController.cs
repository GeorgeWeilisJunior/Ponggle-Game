using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public enum PegType { Blue, Orange, Green, Rainbow, Element }

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class PegController : MonoBehaviour
{
    public enum PegState { Idle, Glowing, Cleared }

    /* ───────── Sprites umum ───────── */
    [Header("Idle / Glow Sprites (Normal/Rainbow/Non-Hard)")]
    [SerializeField] Sprite idleSprite;
    [SerializeField] Sprite glowSprite;

    [Header("Rainbow Sprites (optional)")]
    [SerializeField] Sprite rainbowIdleSprite;
    [SerializeField] Sprite rainbowGlowSprite;

    [Header("Idle Colors (jika sprite cuma 1)")]
    [SerializeField] Color idleColor = Color.blue;
    [SerializeField] Color glowColor = new(0f, 1f, 1f);
    [SerializeField] Color rainbowGlowColor = Color.white;

    [Header("Glow & Fade")]
    [SerializeField] bool scaleOnGlow = false;          // NEW: matikan/pakai pembesaran glow
    [SerializeField, Range(1f, 1.5f)] float glowScale = 1.06f;
    [SerializeField] float fadeDuration = .35f;
    public float FadeDuration => fadeDuration;

    [Header("Rainbow Cycle")]
    [SerializeField, Range(.2f, 3f)] float rainbowCycleSpeed = .8f;

    [Header("Base Setup")]
    [SerializeField] PegType pegType = PegType.Blue;
    public PegType Type => pegType;

    /* ───────── Hard Peg ───────── */
    [Header("Hard Peg")]
    [SerializeField] bool isHardPeg = false;
    [SerializeField, Min(2)] int maxHitsToClear = 2;
    [SerializeField] bool limitHardToOneHitPerShot = true;
    int hitsRemaining;
    int lastShotHitId = -1;

    public bool CanClearWhenStuck => !isHardPeg || hitsRemaining <= 0;

    [Tooltip("Sprite khusus hard peg")]
    [SerializeField] Sprite hardIdle_2X;  // awal (2x)
    [SerializeField] Sprite hardGlow_1X;  // glow setelah hit pertama (1x)
    [SerializeField] Sprite hardIdle_1X;  // kembali idle dari 1x saat end turn
    [SerializeField] Sprite hardGlow_0X;  // glow clear (0x)

    public bool IsHard => isHardPeg;
    public int HitsRemaining => hitsRemaining;

    /* ───────── Element (opsional) ───────── */
    [Header("Element (aktif jika Peg Type = Element)")]
    [SerializeField] ElementType element = ElementType.Fire;
    [SerializeField] bool onlyWhenBallIsNeutral = true;

    /* ───────── Glow VFX (opsional) ───────── */
    [Header("Glow VFX (optional)")]
    [SerializeField] GameObject glowVfxPrefab;
    [SerializeField] bool vfxOnlyForElement = true;
    [SerializeField] Vector3 vfxLocalOffset = Vector3.zero;
    [SerializeField] float vfxScale = 1f;
    [SerializeField] int vfxOrderOffset = 1;

    /* ───────── Runtime ───────── */
    public PegState State { get; private set; } = PegState.Idle;

    SpriteRenderer sr;
    Collider2D col;

    // Skala dasar yang sudah “benar” (sesuai prefab/generator) dan
    // selalu dipakai ulang untuk idle/glow.
    Vector3 baseScale;

    // backup untuk rainbow
    Sprite originalSprite; Color originalColor;
    Coroutine rainbowCR; GameObject activeGlowVfx;

    public static event Action<PegType> OnPegCleared;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        baseScale = transform.localScale; // hormati skala dari prefab/generator

        if (isHardPeg) maxHitsToClear = Mathf.Max(2, maxHitsToClear);
        hitsRemaining = isHardPeg ? maxHitsToClear : 1;

        ApplyIdleVisualForCurrentDurability();

        if (pegType == PegType.Rainbow) StartRainbow();
    }

    /*──────── Public API ────────*/
    public void SetAsRainbow()
    {
        if (isHardPeg || pegType == PegType.Rainbow || pegType == PegType.Element) return;

        originalSprite = sr.sprite;
        originalColor = sr.color;

        pegType = PegType.Rainbow;
        if (rainbowIdleSprite) SwapSpriteKeepWorldSize(rainbowIdleSprite);
        sr.color = Color.white;

        StartRainbow();
        ApplyIdleScaleOnly();
    }

    public void RevertToBlue()
    {
        if (pegType != PegType.Rainbow) return;
        StopRainbow();
        StopGlowVfx(true);

        pegType = PegType.Blue;

        if (originalSprite) SwapSpriteKeepWorldSize(originalSprite);
        else if (idleSprite) SwapSpriteKeepWorldSize(idleSprite);

        sr.color = (idleSprite || originalSprite) ? Color.white : idleColor;

        ApplyIdleScaleOnly();
        State = PegState.Idle;
    }

    public void SetElementType(ElementType newElement, bool refreshVisual = true)
    {
        element = newElement;

        if (pegType != PegType.Element)
            ForceSetPegType(PegType.Element, refreshVisual: refreshVisual);
        else if (refreshVisual)
        {
            if (State == PegState.Glowing) ApplyGlowVisualForCurrentDurability();
            else ApplyIdleVisualForCurrentDurability();
        }
    }

    public bool MarkHitFromSpecial()
    {
        if (State == PegState.Cleared) return false;
        if (State != PegState.Idle && !isHardPeg) return false;

        State = PegState.Glowing;

        if (isHardPeg)
        {
            hitsRemaining = 0;
            if (pegType == PegType.Green)
                CharacterPowerManager.Instance?.TryActivatePower();
        }

        ApplyGlowVisualForCurrentDurability();
        TryStartGlowVfx();
        AudioManager.I.Play("Peghit_low", transform.position);

        GameManager.Instance.RegisterHitPeg(this, null);
        return true;
    }

    /*──────── Rainbow helpers ────────*/
    void StartRainbow() { StopRainbow(); rainbowCR = StartCoroutine(RainbowCycle()); }
    void StopRainbow() { if (rainbowCR != null) StopCoroutine(rainbowCR); rainbowCR = null; }

    IEnumerator RainbowCycle()
    {
        float h = 0f;
        while (true)
        {
            h += Time.deltaTime * rainbowCycleSpeed;
            if (h > 1f) h -= 1f;
            sr.color = Color.HSVToRGB(h, 1f, 1f);
            yield return null;
        }
    }

    /*──────── Visual helpers ────────*/

    // Ganti sprite sambil menjaga ukuran DUNIA — pakai rasio LOCAL bounds
    // agar tidak terpengaruh parent scale/rotation.
    void SwapSpriteKeepWorldSize(Sprite newSprite)
    {
        if (!sr || newSprite == null) return;
        if (sr.sprite == newSprite) return; // tidak perlu kompensasi

        Vector2 oldLocal = sr.sprite ? (Vector2)sr.sprite.bounds.size
                                     : (Vector2)newSprite.bounds.size;
        Vector2 newLocal = newSprite.bounds.size;

        var ls = transform.localScale;
        float fx = (newLocal.x > 0f) ? oldLocal.x / newLocal.x : 1f;
        float fy = (newLocal.y > 0f) ? oldLocal.y / newLocal.y : 1f;

        sr.sprite = newSprite;
        transform.localScale = new Vector3(ls.x * fx, ls.y * fy, ls.z);

        // baseline baru setelah kompensasi sprite
        baseScale = transform.localScale;
    }

    void ApplyIdleScaleOnly() => transform.localScale = baseScale;

    void ApplyIdleVisualForCurrentDurability()
    {
        Sprite target = null; Color colr = Color.white;

        if (isHardPeg)
        {
            bool mudActive = SafeMudActive();
            if (mudActive && hitsRemaining >= 1 && hardIdle_2X) target = hardIdle_2X;
            else
            {
                if (hitsRemaining >= 2 && hardIdle_2X) target = hardIdle_2X;
                else if (hitsRemaining == 1 && hardIdle_1X) target = hardIdle_1X;
                else if (idleSprite) target = idleSprite;
                else colr = idleColor;
            }
        }
        else
        {
            if (pegType == PegType.Rainbow && rainbowIdleSprite) target = rainbowIdleSprite;
            else if (idleSprite) target = idleSprite;
            else colr = idleColor;
        }

        if (target) { SwapSpriteKeepWorldSize(target); sr.color = Color.white; }
        else { sr.color = colr; }

        ApplyIdleScaleOnly();
    }

    void ApplyGlowVisualForCurrentDurability()
    {
        Sprite target = null; Color colr = Color.white;

        if (isHardPeg)
        {
            if (hitsRemaining <= 0 && hardGlow_0X) target = hardGlow_0X;
            else if (hitsRemaining == 1 && hardGlow_1X) target = hardGlow_1X;
            else if (glowSprite) target = glowSprite;
            else colr = glowColor;
        }
        else
        {
            if (pegType == PegType.Rainbow && rainbowGlowSprite) { target = rainbowGlowSprite; colr = rainbowGlowColor; }
            else if (glowSprite) target = glowSprite;
            else colr = glowColor;
        }

        if (target) { SwapSpriteKeepWorldSize(target); sr.color = (colr == Color.white ? Color.white : colr); }
        else { sr.color = colr; }

        // Tidak membesar kecuali diminta
        transform.localScale = baseScale * (scaleOnGlow ? glowScale : 1f);
    }

    /*──────── Collision ────────*/
    void OnCollisionEnter2D(Collision2D c)
    {
        if (!c.collider.CompareTag("Ball")) return;

        if (isHardPeg && limitHardToOneHitPerShot)
        {
            int shotId = GameManager.Instance.CurrentShotId;
            if (lastShotHitId == shotId) return;
            lastShotHitId = shotId;
        }

        bool canHit = (!isHardPeg && State == PegState.Idle) ||
                      (isHardPeg && State != PegState.Cleared && hitsRemaining > 0);
        if (!canHit) return;

        State = PegState.Glowing;

        if (isHardPeg)
        {
            if (CardEffects.I != null && CardEffects.I.overdriveActive)
                hitsRemaining = 0;
            else if (SafeMudActive())
                hitsRemaining = 0;
            else if (hitsRemaining > 0)
                hitsRemaining--;
            if (pegType == PegType.Orange && hitsRemaining == 0)
                GameManager.Instance?.NotifyHardOrangeBroken(this);
            if (pegType == PegType.Green && hitsRemaining == 0)
                CharacterPowerManager.Instance?.TryActivatePower();
        }


        ApplyGlowVisualForCurrentDurability();
        TryStartGlowVfx();
        AudioManager.I.Play("Peghit_low", transform.position);

        var ballC = c.collider.GetComponent<BallController>();
        var ballE = c.collider.GetComponent<BallElement>();

        if (pegType == PegType.Element)
        {
            var p = (c.contactCount > 0) ? c.GetContact(0).point : (Vector2)transform.position;

            if (CardEffects.I != null && CardEffects.I.elementaryMasteryActive)
            {
                // Legendary: reaksi SELALU dipicu (TryTrigger akan override elemen bola → lawan elemen peg)
                ElementReactions.TryTrigger(ballE ? ballE.Current : ElementType.Neutral, element, p, ballC);

                // (opsional) kamu bisa tetap izinkan SetNext agar tembakan berikut bisa diinfus,
                // tapi kalau ingin pure “semua tembakan selalu bisa bereaksi”, abaikan SetNext:
                // ElementSystem.SetNext(element);
            }
            else
            {
                // Perilaku normal (tanpa Legendary)
                if (ballE && ballE.Current != ElementType.Neutral)
                    ElementReactions.TryTrigger(ballE.Current, element, p, ballC);

                if (!onlyWhenBallIsNeutral || ballE == null || ballE.Current == ElementType.Neutral)
                    ElementSystem.SetNext(element);
            }
        }

        GameManager.Instance.RegisterHitPeg(this, ballC);
    }

    /*──────── End turn cleanup ────────*/
    public void OnEndTurnCleanup()
    {
        if (pegType == PegType.Element && CardEffects.I != null && CardEffects.I.elementRechargeActive)
        {
            StopGlowVfx(false);
            State = PegState.Idle;
            ApplyIdleVisualForCurrentDurability();
            return;
        }
        if (!isHardPeg) { ClearNow(); return; }

        if (hitsRemaining <= 0) ClearNow();
        else
        {
            StopGlowVfx(false);
            State = PegState.Idle;
            ApplyIdleVisualForCurrentDurability();
        }
    }

    public void ForceSetPegType(PegType t, bool refreshVisual = false)
    {
        if (pegType == t) return;

        if (pegType == PegType.Rainbow) StopRainbow();

        pegType = t;

        if (pegType == PegType.Rainbow) StartRainbow();

        if (refreshVisual)
        {
            if (State == PegState.Glowing) ApplyGlowVisualForCurrentDurability();
            else ApplyIdleVisualForCurrentDurability();
        }
    }

    /*──────── Clear ────────*/
    public void ClearNow()
    {
        if (pegType == PegType.Element && CardEffects.I != null && CardEffects.I.elementRechargeActive)
        {
            StopGlowVfx(false);                 // matikan glow-nya
            State = PegState.Idle;              // balik ke idle
            ApplyIdleVisualForCurrentDurability();
            return;                              // JANGAN hapus
        }
        if (State == PegState.Cleared) return;

        if (State == PegState.Idle)
            ApplyGlowVisualForCurrentDurability();

        StopRainbow();
        StopGlowVfx(false);

        State = PegState.Cleared;
        if (col) col.enabled = false;

        GameManager.Instance.RegisterPegCleared(pegType == PegType.Orange, isHardPeg);
        OnPegCleared?.Invoke(pegType);

        StartCoroutine(FadeAndDisable());
    }

    public void SimulateHitFromExplosion()
    {
        if (State == PegState.Cleared) return;

        State = PegState.Glowing;

        // ⬇️ Tambahan penting untuk HARD PEG
        if (isHardPeg)
        {
            if (CardEffects.I != null && CardEffects.I.overdriveActive)
                hitsRemaining = 0;
            else if (SafeMudActive())
                hitsRemaining = 0;
            else if (hitsRemaining > 0)
                hitsRemaining--;
            if (pegType == PegType.Orange && hitsRemaining == 0)
                GameManager.Instance?.NotifyHardOrangeBroken(this);

            if (pegType == PegType.Green && hitsRemaining == 0)
                CharacterPowerManager.Instance?.TryActivatePower();
        }

        ApplyGlowVisualForCurrentDurability();
        TryStartGlowVfx();
        AudioManager.I.Play("Peghit_low", transform.position);

        // ⬇️ Ganti direct-hit → indirect-hit, supaya tidak konsumsi StoneBreaker/popup skor
        GameManager.Instance.RegisterIndirectPegHit(this);
    }


    // Paksa ganti tipe + ganti skin mengikuti template peg hijau yang ada di scene.
    // Jika template tidak ditemukan, tetap ubah tipe saja (visual bisa menyusul).
    public void ForceSetGreenWithSkin()
    {
        // cari template green di scene
        PegController tpl = null;
        foreach (var p in FindObjectsOfType<PegController>())
        {
            if (p && p.Type == PegType.Green)
            { tpl = p; break; }
        }

        if (tpl != null)
        {
            // salin semua aset visual yg relevan
            idleSprite = tpl.idleSprite;
            glowSprite = tpl.glowSprite;
            rainbowIdleSprite = tpl.rainbowIdleSprite;
            rainbowGlowSprite = tpl.rainbowGlowSprite;

            idleColor = tpl.idleColor;
            glowColor = tpl.glowColor;
            rainbowGlowColor = tpl.rainbowGlowColor;
        }

        // set tipe + refresh visual
        ForceSetPegType(PegType.Green, refreshVisual: true);
    }

    // Paksa ganti ke Blue + salin skin dari peg biru yang ada di scene (jika ada)
    public void ForceSetBlueWithSkin()
    {
        // Cari template BLUE yg Rounded (prioritas); fallback ke Blue apa saja
        PegController tpl = null;

        // helper lokal
        bool IsRounded(PegController p)
        {
            var col = p ? p.GetComponent<Collider2D>() : null;
            bool circle = col is CircleCollider2D;
            var fam = p ? p.GetComponent<PegFamilyTag>() : null;
            bool familyRounded = fam && fam.family == PegFamily.Rounded;
            return circle && familyRounded;
        }

        var all = FindObjectsOfType<PegController>();
        tpl = all.FirstOrDefault(p => p && p.Type == PegType.Blue && IsRounded(p))
           ?? all.FirstOrDefault(p => p && p.Type == PegType.Blue);

        if (tpl != null)
        {
            // Salin aset visual dari template Blue
            idleSprite = tpl.idleSprite;
            glowSprite = tpl.glowSprite;
            rainbowIdleSprite = tpl.rainbowIdleSprite;
            rainbowGlowSprite = tpl.rainbowGlowSprite;

            idleColor = tpl.idleColor;
            glowColor = tpl.glowColor;
            rainbowGlowColor = tpl.rainbowGlowColor;

            // Skala/collider ikut template
            transform.localScale = tpl.transform.localScale;
            baseScale = transform.localScale;

            var myCC = GetComponent<CircleCollider2D>();
            var tplCC = tpl.GetComponent<CircleCollider2D>();
            if (myCC && tplCC) { myCC.radius = tplCC.radius; myCC.offset = tplCC.offset; }

            // 🔧 Normalisasi SHADOW child (nama umum: "Shadow" / "PegShadow")
            Transform mySh = transform.Find("Shadow") ?? transform.Find("PegShadow");
            Transform tpSh = tpl.transform.Find("Shadow") ?? tpl.transform.Find("PegShadow");
            if (mySh && tpSh)
            {
                mySh.localScale = tpSh.localScale;
                var mySR = mySh.GetComponent<SpriteRenderer>();
                var tplSR = tpSh.GetComponent<SpriteRenderer>();
                if (mySR && tplSR) mySR.sprite = tplSR.sprite; // samakan bentuk shadow
            }
        }

        // Reset hardness & behavior
        isHardPeg = false;
        maxHitsToClear = 1;
        hitsRemaining = 1;
        hardIdle_2X = hardIdle_1X = hardGlow_1X = hardGlow_0X = null;
        scaleOnGlow = false;

        // Set tipe → JANGAN panggil refresh visual otomatis (hindari kompensasi ukuran)
        ForceSetPegType(PegType.Blue, refreshVisual: false);
        State = PegState.Idle;

        // Terapkan sprite langsung (tanpa SwapSpriteKeepWorldSize)
        StopRainbow();
        StopGlowVfx(true);
        if (idleSprite) { sr.sprite = idleSprite; sr.color = Color.white; }
        else { sr.sprite = null; sr.color = idleColor; }

        // Pastikan skala final sesuai template
        transform.localScale = baseScale;
    }

    IEnumerator FadeAndDisable()
    {
        float t = 0f;
        var start = sr.color; start.a = 1f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            var c = start; c.a = 1f - t / fadeDuration;
            sr.color = c;
            yield return null;
        }
        gameObject.SetActive(false);
    }

    /*──────── Glow VFX helpers ────────*/
    void TryStartGlowVfx()
    {
        if (!glowVfxPrefab) return;
        if (vfxOnlyForElement && pegType != PegType.Element) return;
        if (activeGlowVfx) return;

        activeGlowVfx = Instantiate(glowVfxPrefab, transform);
        activeGlowVfx.transform.localPosition = vfxLocalOffset;
        activeGlowVfx.transform.localRotation = Quaternion.identity;
        activeGlowVfx.transform.localScale = Vector3.one * Mathf.Max(0.0001f, vfxScale);

        foreach (var r in activeGlowVfx.GetComponentsInChildren<Renderer>(true))
        { r.sortingLayerID = sr.sortingLayerID; r.sortingOrder = sr.sortingOrder + vfxOrderOffset; }
        foreach (var ps in activeGlowVfx.GetComponentsInChildren<ParticleSystem>(true))
            ps.Play(true);
    }

    void StopGlowVfx(bool immediate)
    {
        if (!activeGlowVfx) return;
        if (immediate) { Destroy(activeGlowVfx); activeGlowVfx = null; return; }

        foreach (var ps in activeGlowVfx.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(activeGlowVfx, 1f);
        activeGlowVfx = null;
    }

    /*──────── Safe helpers ────────*/
    bool SafeMudActive()
    {
        try { return ReactionExecutorV2.MudActive; }
        catch { return false; }
    }
}
