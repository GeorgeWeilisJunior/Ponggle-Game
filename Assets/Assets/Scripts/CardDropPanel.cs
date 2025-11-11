// CardDropPanel.cs — Title pakai GameObject toggle (bukan ganti sprite)
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CardDropPanel : MonoBehaviour
{
    /* ───────── Flow ───────── */
    [Header("Flow")]
    [SerializeField] EndLevelPanel endLevelPanel;
    [SerializeField] bool autoOpenOnWin = false;
    [SerializeField] bool useStage5Rates = false;

    /* ───────── Root & Background ───────── */
    [Header("Root & Background")]
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] GameObject dimBackground, blurRoot;

    /* ───────── Panel Konten ───────── */
    [Header("Panel Konten")]
    [SerializeField] RectTransform cardPivot;

    /* ───────── Title pakai GO ───────── */
    [Header("Title Objects (toggle ON/OFF)")]
    [SerializeField] GameObject titleTap2TimesGO;  // "TAP THE CARD 2 TIMES!"
    [SerializeField] GameObject titleTap1TimeGO;   // "ONE MORE TAP!"
    [SerializeField] GameObject titleYouGotGO;     // "YOU GOT A CARD"

    /* ───────── Mystery (sebelum reveal) ───────── */
    [Header("Mystery (sebelum reveal)")]
    [SerializeField] Image mysteryCardImage;
    [SerializeField] GameObject cornerFlairs;

    /* ───────── Reveal Holder (kartu utuh) ───────── */
    [Header("Reveal Holder (kartu utuh)")]
    [SerializeField] RectTransform revealHolder;
    [SerializeField] GameObject defaultFullCardPrefab; // boleh kosong
    [SerializeField] GameObject spawnedView;

    /* ───────── Fallback UI (opsional) ───────── */
    [Header("Fallback UI (opsional)")]
    [SerializeField] Image cardIcon, rarityStripe;
    [SerializeField] TMP_Text cardNameText, cardRarityText, cardDescText;

    /* ───────── Timing & Anim ───────── */
    [Header("Timing & Anim")]
    [SerializeField] float fadeInTime = 0.25f;
    [SerializeField] float tap1ShakeAngle = 7.5f, tap2ShakeAngle = 11f;
    [SerializeField] float shakeDuration = 0.22f;
    [SerializeField] float flipDuration = 0.28f;
    [SerializeField] float revealPopScale = 1.08f, revealHold = 0.85f;

    /* ───────── SFX (opsional) ───────── */
    [Header("SFX (opsional)")]
    [SerializeField] string sfxTapKey = "UIClick";
    [SerializeField] string sfxRevealKey = "CardReveal";
    [SerializeField] string sfxDoneKey = "TaDa";

    /* ───────── FX & Styling ───────── */
    [Header("FX & Styling")]
    [SerializeField] Image boardImage;               // drag: Board (Image)
    [SerializeField] RectTransform fxRoot;           // drag: Board/FXRoot
    [SerializeField] bool enableConfetti = true;
    [SerializeField] bool enableShine = true;
    [SerializeField] Transform worldVfxRoot;         // opsional
    [SerializeField] ParticleSystem fireworksPrefab; // opsional

    /* ───────── Continue Hint (Image + CanvasGroup) ───────── */
    [Header("Continue Hint")]
    [SerializeField] CanvasGroup continueHint;   // drag GO gambar hint (punya CanvasGroup)
    [SerializeField] float continueFadeIn = 0.3f;
    [SerializeField] float continuePulsePeriod = 1.2f;

    /* ───────── Runtime ───────── */
    bool running, subscribed;
    Coroutine subscribeRoutine;

    /* ───────── Lifecycle ───────── */
    void Awake()
    {
        EnsureLayerOrder();

        if (canvasGroup)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        ToggleMystery(true);
        ToggleFallback(false);

        if (revealHolder)
        {
            revealHolder.gameObject.SetActive(false);
            ClearRevealHolder();
        }

        if (cornerFlairs) cornerFlairs.SetActive(false);

        // default: tampilkan "Tap 2 times"
        SetTitleState(TitleState.Tap2);

        if (continueHint)
        {
            continueHint.gameObject.SetActive(false);
            continueHint.alpha = 0f;
        }
    }

    void OnEnable()
    {
        if (autoOpenOnWin)
            subscribeRoutine = StartCoroutine(EnsureSubscribed());
    }

    void OnDisable()
    {
        if (subscribed && GameManager.Instance != null)
            GameManager.Instance.onLevelSuccess -= HandleWin;
        subscribed = false;

        if (subscribeRoutine != null)
        {
            StopCoroutine(subscribeRoutine);
            subscribeRoutine = null;
        }
    }

    IEnumerator EnsureSubscribed()
    {
        while (GameManager.Instance == null) yield return null;
        if (!subscribed)
        {
            GameManager.Instance.onLevelSuccess += HandleWin;
            subscribed = true;
        }
    }

    void HandleWin(LevelStats s)
    {
        if (autoOpenOnWin)
            BeginFromWin();
    }

    /// Dipanggil dari EndLevelPanel → tombol “Open Reward”.
    public void BeginFromWin()
    {
        if (!gameObject.activeInHierarchy) gameObject.SetActive(true);

        if (continueHint)
        {
            continueHint.alpha = 0f;
            continueHint.gameObject.SetActive(false);
        }

        if (!running) StartCoroutine(Sequence());
    }

    [ContextMenu("TEST Reveal Now")]
    void TestRevealNow() => BeginFromWin();

    /* ───────── Main Sequence ───────── */
    IEnumerator Sequence()
    {
        running = true;

        if (blurRoot) blurRoot.SetActive(true);
        if (dimBackground) dimBackground.SetActive(true);

        if (canvasGroup)
        {
            canvasGroup.gameObject.SetActive(true);
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            yield return Fade(canvasGroup, 0f, 1f, fadeInTime);
        }

        ToggleMystery(true);
        ToggleFallback(false);
        if (revealHolder) { revealHolder.gameObject.SetActive(false); ClearRevealHolder(); }
        if (cornerFlairs) cornerFlairs.SetActive(true);

        SetTitleState(TitleState.Tap2);

        // ── TAP #1 ──
        yield return ShowContinueHintAndWaitTap();
        PlayUI(sfxTapKey);
        yield return Shake(cardPivot, tap1ShakeAngle, shakeDuration);
        SetTitleState(TitleState.Tap1);

        // ── TAP #2 ──
        yield return ShowContinueHintAndWaitTap();
        PlayUI(sfxTapKey);
        yield return Shake(cardPivot, tap2ShakeAngle, shakeDuration);

        // ── Flip + reveal ──
        CardData revealed = null;
        yield return FlipReveal(cardPivot, flipDuration, () =>
        {
            ToggleMystery(false);

            var card = CardLibrary.DrawRandomCard(useStage5Rates);
            revealed = card;

            // 1) Tambah ke runtime inventory (langsung terasa di sesi sekarang)
            CardInventory.I.AddCard(card);

            // 2) Opsional: catat ke buffer save untuk diklaim ke owned saat keluar panel
            if (SaveManager.I != null && card != null)
                SaveManager.I.AddDropToBuffer(card.id);

            if (!SpawnFullCardView(card))
            {
                FillFallbackUI(card);
                ToggleFallback(true);
            }

            PlayRarityFX(card);
            PlayUI(sfxRevealKey);
            SetTitleState(TitleState.YouGot);
            if (cornerFlairs) cornerFlairs.SetActive(false);
        });

        // Pop kecil + hold
        yield return Pop(cardPivot, revealPopScale, 0.18f);
        yield return new WaitForSecondsRealtime(revealHold);
        PlayUI(sfxDoneKey);

        // ── Tampilkan hint & tunggu tap user ──
        yield return ShowContinueHintAndWaitTap();

        // 3) Klaim buffer → ownedCards (dan auto-save)
        if (SaveManager.I != null)
            SaveManager.I.ClaimDropsToInventory();

        // Tutup CardDrop
        if (canvasGroup) yield return Fade(canvasGroup, 1f, 0f, 0.20f);
        if (blurRoot) blurRoot.SetActive(false);
        if (dimBackground) dimBackground.SetActive(false);
        if (canvasGroup)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.gameObject.SetActive(false);
        }

        // Balikkan EndLevelPanel (mode Win)
        if (!endLevelPanel) endLevelPanel = FindObjectOfType<EndLevelPanel>(true);
        if (continueHint) continueHint.gameObject.SetActive(false);
        if (endLevelPanel) endLevelPanel.ShowPending();

        running = false;
    }

    /* ───────── Continue Hint ───────── */
    IEnumerator ShowContinueHintAndWaitTap()
    {
        if (!continueHint) yield break;

        continueHint.gameObject.SetActive(true);
        continueHint.alpha = 0f;

        float t = 0f;
        float dur = Mathf.Max(0.0001f, continueFadeIn);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            continueHint.alpha = Mathf.Clamp01(t / dur);
            yield return null;
        }
        continueHint.alpha = 1f;

        float period = Mathf.Max(0.01f, continuePulsePeriod);
        while (!AnyTapDown())
        {
            float a = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / period));
            continueHint.alpha = a;
            yield return null;
        }

        continueHint.alpha = 1f;
        continueHint.gameObject.SetActive(false);
    }

    bool AnyTapDown()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0)) return true;
#endif
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) return true;
        return false;
    }

    /* ───────── Prefab / Fallback ───────── */
    bool SpawnFullCardView(CardData card)
    {
        if (!card || !revealHolder) return false;

        GameObject prefab = card.fullCardPrefab ? card.fullCardPrefab : defaultFullCardPrefab;
        if (!prefab) return false;

        ClearRevealHolder();

        spawnedView = Instantiate(prefab, revealHolder);

        if (spawnedView.TryGetComponent<RectTransform>(out var rt))
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        var binder = spawnedView.GetComponent<CardSpriteView>();
        if (binder != null) binder.Bind(card);
        else
        {
            var img = spawnedView.GetComponentInChildren<Image>();
            if (img != null)
            {
                Sprite sp = card.fullCardSprite ? card.fullCardSprite : card.icon;
                img.sprite = sp;
                img.preserveAspect = true;
                img.enabled = sp != null;
            }
        }

        revealHolder.gameObject.SetActive(true);
        return true;
    }

    void ClearRevealHolder()
    {
        if (!revealHolder) return;
        for (int i = revealHolder.childCount - 1; i >= 0; i--)
            Destroy(revealHolder.GetChild(i).gameObject);
        spawnedView = null;
    }

    void FillFallbackUI(CardData card)
    {
        if (!card) return;
        Sprite sp = card.fullCardSprite ? card.fullCardSprite : card.icon;
        if (cardIcon) { cardIcon.sprite = sp; cardIcon.enabled = sp; cardIcon.preserveAspect = true; }
        if (cardNameText) cardNameText.text = card.displayName;
        if (cardRarityText) cardRarityText.text = card.rarity.ToString();
        if (cardDescText) cardDescText.text = card.description;
        if (rarityStripe) rarityStripe.color = card.RarityColor;
    }

    void ToggleFallback(bool on)
    {
        if (cardIcon) cardIcon.enabled = on && cardIcon.sprite;
        if (cardNameText) cardNameText.gameObject.SetActive(on);
        if (cardRarityText) cardRarityText.gameObject.SetActive(on);
        if (cardDescText) cardDescText.gameObject.SetActive(on);
        if (rarityStripe) rarityStripe.gameObject.SetActive(on);
    }

    /* ───────── FX ───────── */
    void PlayRarityFX(CardData card)
    {
        EnsureLayerOrder();
        if (boardImage) StartCoroutine(AnimateBoardGradient(card ? card.RarityColor : Color.white));
        if (enableShine) StartCoroutine(ShineSweepOverCard());
        if (enableConfetti && fxRoot) StartCoroutine(ConfettiBurst(28, 1.2f));

        if (worldVfxRoot && fireworksPrefab)
        {
            var ps = Instantiate(fireworksPrefab, worldVfxRoot);
            var main = ps.main;
            Destroy(ps.gameObject, main.duration + main.startLifetime.constantMax + 0.5f);
        }
    }

    IEnumerator AnimateBoardGradient(Color rarityCol)
    {
        Color top = Color.Lerp(Color.white, rarityCol, 0.35f);
        Color bottom = Color.Lerp(Color.black, rarityCol, 0.65f);

        Image gradImg = GetOrMakeGradientOverlay();
        Sprite sp = MakeVerticalGradientSprite(top, bottom, 96);
        gradImg.sprite = sp;
        gradImg.type = Image.Type.Simple;
        gradImg.raycastTarget = false;
        gradImg.enabled = true;

        float t = 0f, dur = 0.25f;
        Color bg0 = boardImage.color;
        Color bg1 = new Color(1, 1, 1, 0.95f);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = t / dur;
            boardImage.color = Color.Lerp(bg0, bg1, k);
            gradImg.canvasRenderer.SetAlpha(Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        boardImage.color = bg1;
        gradImg.canvasRenderer.SetAlpha(1f);
    }

    Image GetOrMakeGradientOverlay()
    {
        Transform ex = boardImage ? boardImage.transform.Find("GradientOverlay") : null;
        if (ex)
        {
            ex.SetAsFirstSibling();
            return ex.GetComponent<Image>();
        }

        GameObject go = new GameObject("GradientOverlay", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(boardImage.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.SetAsFirstSibling();

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = Color.white;
        img.canvasRenderer.SetAlpha(0f);
        return img;
    }

    Sprite MakeVerticalGradientSprite(Color top, Color bottom, int h)
    {
        var tex = new Texture2D(1, h, TextureFormat.RGBA32, false, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < h; y++)
        {
            float tt = (float)y / (h - 1);
            tex.SetPixel(0, y, Color.Lerp(top, bottom, tt));
        }
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 1f);
    }

    IEnumerator ShineSweepOverCard()
    {
        var go = new GameObject("Shine", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(cardPivot, false);
        rt.anchorMin = new Vector2(0, 0.1f);
        rt.anchorMax = new Vector2(2, 0.9f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.localRotation = Quaternion.Euler(0, 0, 18f);

        var img = go.GetComponent<Image>();
        img.sprite = MakeHorizontalAlphaGradient(96);
        img.color = new Color(1, 1, 1, 0.8f);
        img.raycastTarget = false;

        float w = cardPivot.rect.width;
        float t = 0f, dur = 0.7f;
        float from = -w * 0.9f, to = w * 0.9f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = t / dur;
            rt.anchoredPosition = new Vector2(Mathf.Lerp(from, to, k), 0f);
            img.canvasRenderer.SetAlpha(Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        Destroy(go);
    }

    Sprite MakeHorizontalAlphaGradient(int w)
    {
        var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int x = 0; x < w; x++)
        {
            float tt = (float)x / (w - 1);
            float a = Mathf.Sin(tt * Mathf.PI);
            tex.SetPixel(x, 0, new Color(1f, 1f, 1f, a));
        }
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, w, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    IEnumerator ConfettiBurst(int count, float dur)
    {
        if (!fxRoot) yield break;

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            if (boardImage)
            {
                float s = 1f + 0.005f * Mathf.Sin(Time.unscaledTime * 17f);
                boardImage.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            yield return null;
        }
        if (boardImage) boardImage.rectTransform.localScale = Vector3.one;
    }

    /* ───────── Tiny Anims ───────── */
    IEnumerator Fade(CanvasGroup cg, float from, float to, float dur)
    {
        if (!cg) yield break;
        cg.alpha = from;
        dur = Mathf.Max(0.0001f, dur);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }
        cg.alpha = to;
    }

    IEnumerator Shake(RectTransform rt, float angle, float dur)
    {
        if (!rt) yield break;
        dur = Mathf.Max(0.0001f, dur);
        float t = 0f;
        float freq = 42f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Sin(t * freq) * Mathf.SmoothStep(angle, 0f, t / dur);
            rt.localRotation = Quaternion.Euler(0, 0, a);
            yield return null;
        }
        rt.localRotation = Quaternion.identity;
    }

    IEnumerator Pop(RectTransform rt, float targetScale, float dur)
    {
        if (!rt) yield break;
        dur = Mathf.Max(0.0001f, dur);
        float t = 0f;
        Vector3 start = Vector3.one;
        Vector3 end = Vector3.one * targetScale;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = t / dur;
            rt.localScale = Vector3.Lerp(start, end, k);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    IEnumerator FlipReveal(RectTransform rt, float dur, Action onFlipMiddle)
    {
        if (!rt) yield break;
        dur = Mathf.Max(0.0001f, dur);

        float t = 0f;
        while (t < dur * 0.5f)
        {
            t += Time.unscaledDeltaTime;
            float k = t / (dur * 0.5f);
            float angle = Mathf.Lerp(0f, 90f, k);
            rt.localRotation = Quaternion.Euler(0f, angle, 0f);
            yield return null;
        }

        onFlipMiddle?.Invoke();

        t = 0f;
        while (t < dur * 0.5f)
        {
            t += Time.unscaledDeltaTime;
            float k = t / (dur * 0.5f);
            float angle = Mathf.Lerp(90f, 0f, k);
            rt.localRotation = Quaternion.Euler(0f, angle, 0f);
            yield return null;
        }
        rt.localRotation = Quaternion.identity;
    }

    /* ───────── Utilities ───────── */
    void EnsureLayerOrder()
    {
        var cv = GetComponent<Canvas>();
        if (cv)
        {
            cv.overrideSorting = true;
            if (cv.sortingOrder < 60) cv.sortingOrder = 60;
        }
    }

    void ToggleMystery(bool on)
    {
        if (mysteryCardImage) mysteryCardImage.enabled = on;
    }

    enum TitleState { Tap2, Tap1, YouGot }
    void SetTitleState(TitleState s)
    {
        if (titleTap2TimesGO) titleTap2TimesGO.SetActive(s == TitleState.Tap2);
        if (titleTap1TimeGO) titleTap1TimeGO.SetActive(s == TitleState.Tap1);
        if (titleYouGotGO) titleYouGotGO.SetActive(s == TitleState.YouGot);
    }

    void PlayUI(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (AudioManager.I) AudioManager.I.Play(key, Camera.main ? Camera.main.transform.position : transform.position);
    }
}
