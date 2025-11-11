using UnityEngine;
using UnityEngine.UI;

public class NextBallUI : MonoBehaviour
{
    [Header("Target UI")]
    [SerializeField] Image icon;            // drag GameCanvas/NextBall/Icon (Image)

    [Header("Sprites")]
    [SerializeField] Sprite neutral;
    [SerializeField] Sprite fire;
    [SerializeField] Sprite water;
    [SerializeField] Sprite wind;
    [SerializeField] Sprite earth;

    [Header("Fireball Ready (Aposda)")]
    [Tooltip("Tampilkan ikon khusus saat next-shot = Fireball (Aposda).")]
    [SerializeField] bool showFireballReady = true;
    [SerializeField] Sprite fireballReadySprite;
    [SerializeField] float fireballScale = 2f;
    [SerializeField] string sfxOnFireballReady = "ElementPick";   // kosongkan jika tak ingin bunyi

    [Header("Sizing (baseline = ukuran RectTransform Icon di editor)")]
    [SerializeField] bool useIconCurrentSizeAsBaseline = true;
    [SerializeField] Vector2 fallbackBaselineSize = new Vector2(100, 100);

    [Tooltip("Skala untuk sprite NEUTRAL (1 = sama persis dgn baseline).")]
    [SerializeField] float neutralScale = 1f;

    [Tooltip("Skala untuk sprite ELEMEN (Fire/Water/Wind/Earth).")]
    [SerializeField] float elementScale = 2f;

    [Tooltip("Offset posisi ikon (opsional).")]
    [SerializeField] Vector2 anchoredOffset = Vector2.zero;

    [Header("SFX")]
    [SerializeField] bool playSfxOnChange = true;
    [SerializeField] string sfxOnElement = "ElementPick"; // kunci di AudioManager untuk saat berubah ke elemen
    [SerializeField] string sfxOnNeutral = "";            // isi jika ingin bunyi saat reset ke netral; kosong = diam

    Vector2 baselineSize;
    bool ready;
    ElementType lastShownElement = ElementType.Neutral;
    bool lastFireballShown = false;

    CharacterPowerManager cpm;

    void OnEnable()
    {
        if (!icon)
        {
            Debug.LogWarning("[NextBallUI] Icon belum di-assign.");
            return;
        }

        baselineSize = useIconCurrentSizeAsBaseline
            ? icon.rectTransform.sizeDelta
            : fallbackBaselineSize;

        // pastikan anchor tidak stretch (biar sizeDelta bekerja)
        var rt = icon.rectTransform;
        if (rt.anchorMin != rt.anchorMax)
        {
            var c = (rt.anchorMin + rt.anchorMax) * 0.5f;
            rt.anchorMin = c; rt.anchorMax = c;
        }

        ready = true;

        // subscribe element change
        ElementSystem.OnNextChanged += OnNextChanged;

        // subscribe power change (Aposda ready/consumed)
        cpm = CharacterPowerManager.Instance;
        if (cpm != null) cpm.OnPowerChanged += OnPowerChanged;

        // first refresh (tanpa bunyi)
        lastShownElement = ElementSystem.Next;
        lastFireballShown = IsFireballReady();
        Refresh(lastShownElement, /*maybeSfx*/ false);
    }

    void OnDisable()
    {
        ElementSystem.OnNextChanged -= OnNextChanged;
        if (cpm != null) cpm.OnPowerChanged -= OnPowerChanged;
        ready = false;
    }

    void OnNextChanged(ElementType e) => Refresh(e, /*maybeSfx*/ true);
    void OnPowerChanged(string _ignored) => Refresh(ElementSystem.Next, /*maybeSfx*/ true);

    // fallback guard: kalau event power-nya nggak ke-trigger karena urutan init, cek perubahan tiap frame ringan
    void LateUpdate()
    {
        if (!ready || !showFireballReady) return;
        bool now = IsFireballReady();
        if (now != lastFireballShown)
            Refresh(ElementSystem.Next, /*maybeSfx*/ true);
    }

    bool IsFireballReady()
    {
        return showFireballReady &&
               CharacterPowerManager.Instance &&
               CharacterPowerManager.Instance.nextShotFireball;
    }

    void Refresh(ElementType e, bool maybeSfx)
    {
        if (!ready || !icon) return;

        bool fireballReady = IsFireballReady();

        // 1) SFX
        if (maybeSfx && playSfxOnChange)
        {
            if (fireballReady != lastFireballShown)
            {
                if (fireballReady && !string.IsNullOrEmpty(sfxOnFireballReady))
                    AudioManager.I.PlayUI(sfxOnFireballReady);
            }
            else if (e != lastShownElement)
            {
                string key = (e == ElementType.Neutral) ? sfxOnNeutral : sfxOnElement;
                if (!string.IsNullOrEmpty(key))
                    AudioManager.I.PlayUI(key);
            }
        }

        // 2) Pilih sprite & scale
        Sprite sp;
        float scale;

        if (fireballReady && fireballReadySprite)
        {
            sp = fireballReadySprite;
            scale = Mathf.Max(0.0001f, fireballScale);
        }
        else
        {
            sp = e switch
            {
                ElementType.Fire => fire,
                ElementType.Water => water,
                ElementType.Wind => wind,
                ElementType.Earth => earth,
                _ => neutral
            };

            scale = Mathf.Max(0.0001f, (e == ElementType.Neutral) ? neutralScale : elementScale);
        }

        // 3) Apply ukuran & posisi
        var rt = icon.rectTransform;
        rt.sizeDelta = baselineSize * scale;
        rt.anchoredPosition = anchoredOffset;

        icon.sprite = sp;
        icon.preserveAspect = true;
        if (icon.color.a < 1f) icon.color = new Color(icon.color.r, icon.color.g, icon.color.b, 1f);
        icon.enabled = (icon.sprite != null);

        // 4) cache state
        lastShownElement = e;
        lastFireballShown = fireballReady;
    }
}
