// CharacterChoiceIntro.cs — Version C (hero title slides from right)
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class CharacterChoiceIntro : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] CanvasGroup rootFade;          // CanvasGroup yg di-fade (bukan BG)
    [SerializeField] RectTransform bg;              // optional (tidak diapa-apain di versi ini)
    [SerializeField] RectTransform arc;             // panel lengkung bawah
    [SerializeField] RectTransform portraitActive;  // BigPortrait aktif saat start
    [SerializeField] Behaviour diamondBob;        // UIFloatBob/Spin pada Diamond

    [Header("Choose Your Hero (Image)")]
    [SerializeField] Image chooseYourHeroImg;   // gambar judul
    [SerializeField] bool animateChooseHero = true; // slide dari kanan?
    RectTransform chooseHeroRT;
    Vector2 chooseHeroStart;

    [Header("Left / Right UI")]
    [SerializeField] RectTransform detailBox;       // kotak kiri "Character Detail"
    [SerializeField] RectTransform titleRight;      // kalau ada title lain di kanan (boleh null)
    [SerializeField] RectTransform btnLeft;         // panah kiri
    [SerializeField] RectTransform btnRight;        // panah kanan
    [SerializeField] RectTransform nameBox;         // box nama
    [SerializeField] RectTransform confirmBtn;      // tombol confirm

    [Header("Timings")]
    [SerializeField] float tFade = 0.25f;
    [SerializeField] float tArc = 0.30f;
    [SerializeField] float tPop = 0.28f;
    [SerializeField] float tSlide = 0.22f;
    [SerializeField] float delayBetween = 0.05f;

    [Header("Distances")]
    [SerializeField] float offX = 900f;   // masuk dari kiri/kanan
    [SerializeField] float offY = 260f;   // masuk dari bawah

    [Header("SFX (opsional)")]
    [SerializeField] string sfxWhoosh = "UIWhoosh";
    [SerializeField] string sfxPop = "UIPop";

    [Header("Input Lock")]
    [SerializeField] Behaviour selectController;  // CharacterSelectController (dikunci saat intro)

    [Header("Input Lock (Raycast)")]
    [SerializeField] CanvasGroup raycastGroup;

    [Header("BGM")]
    [SerializeField] bool playBGMOnShow = true;
    [SerializeField] string bgmKey = "BGM.CharacterSelect"; // ganti sesuai key BGM-mu
    [SerializeField] float bgmFadeIn = 0.75f;
    [SerializeField] float bgmFadeOut = 0.5f;

    [Header("Hover/Nudge Lock")]
    [SerializeField] bool hardLockHoverDuringIntro = true;
    UIHoverNudgeTint[] _hoverers;

    Vector2 detStart, titleStart, leftStart, rightStart, nameStart, confirmStart;
    Vector3 arcStart, portraitStartScale;

    void Reset() { rootFade = GetComponent<CanvasGroup>(); }

    void Awake()
    {
        if (!rootFade) rootFade = gameObject.AddComponent<CanvasGroup>();
        rootFade.alpha = 1f; // bg tidak ikut fade; UI lain diatur posnya saja

        if (raycastGroup)
        {
            raycastGroup.interactable = false;   // semua Selectable (Button, dll) nonaktif
            raycastGroup.blocksRaycasts = false;   // raycast UI diblokir → hover tidak kejadian
        }

        // posisi ON-screen untuk semua
        if (detailBox) detStart = detailBox.anchoredPosition;
        if (titleRight) titleStart = titleRight.anchoredPosition;
        if (btnLeft) leftStart = btnLeft.anchoredPosition;
        if (btnRight) rightStart = btnRight.anchoredPosition;
        if (nameBox) nameStart = nameBox.anchoredPosition;
        if (confirmBtn) confirmStart = confirmBtn.anchoredPosition;
        if (arc) arcStart = arc.localScale;

        // siapkan OFF-screen state (slide-in)
        if (detailBox) detailBox.anchoredPosition = detStart + Vector2.left * offX;
        if (titleRight) titleRight.anchoredPosition = titleStart + Vector2.right * offX;
        if (btnLeft) btnLeft.anchoredPosition = leftStart + Vector2.left * offX * 0.6f;
        if (btnRight) btnRight.anchoredPosition = rightStart + Vector2.right * offX * 0.6f;
        if (nameBox) nameBox.anchoredPosition = nameStart + Vector2.down * offY;
        if (confirmBtn) confirmBtn.anchoredPosition = confirmStart + Vector2.down * (offY + 80f);

        if (arc) arc.localScale = new Vector3(arcStart.x, 0.6f, arcStart.z);

        if (portraitActive)
        {
            portraitStartScale = portraitActive.localScale;
            portraitActive.localScale = portraitStartScale * 0.8f; // pop-in
            portraitActive.gameObject.SetActive(true);
        }
        if (hardLockHoverDuringIntro)
        {
            _hoverers = GetComponentsInChildren<UIHoverNudgeTint>(true);
            foreach (var h in _hoverers) h.enabled = false;
        }
        // setup ChooseYourHero
        if (chooseYourHeroImg)
        {
            chooseHeroRT = (RectTransform)chooseYourHeroImg.transform;
            chooseHeroStart = chooseHeroRT.anchoredPosition;

            var pulser = chooseYourHeroImg.GetComponent<UIPulseImage>();
            if (pulser) pulser.enabled = false; // dinyalakan setelah masuk

            if (animateChooseHero)
            {
                // geser off-screen kanan, alpha biarkan 1 supaya tidak 'hilang'
                chooseHeroRT.anchoredPosition = chooseHeroStart + Vector2.right * offX;
            }
        }

        if (diamondBob) diamondBob.enabled = false; // aktif setelah pop portrait
        if (selectController) selectController.enabled = false;
    }

    void OnEnable()
    {
        // Mulai BGM saat scene/intro tampil
        if (playBGMOnShow)
        {
            // pastikan tidak dobel
            AudioManager.I?.StopMusic();
            // mainkan musik utama (loop = true)
            AudioManager.I?.PlayMusic(bgmKey, true);
        }

        PlayIntro(); // panggilan existing kamu
    }

    void OnDisable()
    {
        // Fade-out saat keluar scene (opsional)
        AudioManager.I?.StopMusic();
        // Jika pakai fallback tanpa API BGM khusus dan mau hard stop:
        // AudioManager.I?.Stop(bgmKey);
    }

    public void PlayIntro()
    {
        var seq = DOTween.Sequence();

        // 1) (opsional) fade root ke 1 (jaga-jaga kalau pernah di-set lain)
        seq.Append(rootFade.DOFade(1f, tFade));

        // 2) Arc grow
        if (arc) seq.Append(((RectTransform)arc).DOScaleY(1f, tArc).SetEase(Ease.OutCubic));
        else seq.AppendInterval(tArc);

        // 3) Portrait pop + start diamond bob
        if (portraitActive)
        {
            seq.AppendCallback(() => AudioManager.I?.PlayUI(sfxPop));
            seq.Append(portraitActive.DOScale(portraitStartScale * 1.06f, tPop * 0.7f).SetEase(Ease.OutBack, 1.3f));
            seq.Append(portraitActive.DOScale(portraitStartScale, tPop * 0.3f).SetEase(Ease.OutSine));
            seq.AppendCallback(() => { if (diamondBob) diamondBob.enabled = true; });
        }

        // 4) Detail (kiri) + TitleRight (kalau ada) + ChooseYourHero (dari kanan)
        seq.AppendInterval(delayBetween);
        if (detailBox) seq.Join(detailBox.DOAnchorPos(detStart, tSlide).SetEase(Ease.OutCubic));
        if (titleRight) seq.Join(titleRight.DOAnchorPos(titleStart, tSlide).SetEase(Ease.OutCubic));
        if (chooseYourHeroImg && animateChooseHero)
        {
            seq.Join(chooseHeroRT.DOAnchorPos(chooseHeroStart, tSlide).SetEase(Ease.OutCubic));
        }
        // nyalakan pulse setelah step ini
        if (chooseYourHeroImg)
        {
            seq.AppendCallback(() =>
            {
                var pulser = chooseYourHeroImg.GetComponent<UIPulseImage>();
                if (pulser) pulser.enabled = true;
            });
        }

        // 5) Arrows slide + whoosh
        seq.AppendInterval(delayBetween);
        if (btnLeft) seq.Join(btnLeft.DOAnchorPos(leftStart, tSlide).SetEase(Ease.OutBack, 0.8f));
        if (btnRight) seq.Join(btnRight.DOAnchorPos(rightStart, tSlide).SetEase(Ease.OutBack, 0.8f));
        seq.AppendCallback(() => AudioManager.I?.PlayUI(sfxWhoosh));

        // 6) NameBox + Confirm naik dari bawah
        seq.AppendInterval(delayBetween);
        if (nameBox) seq.Join(nameBox.DOAnchorPos(nameStart, tSlide).SetEase(Ease.OutCubic));
        if (confirmBtn) seq.Join(confirmBtn.DOAnchorPos(confirmStart, tSlide).SetEase(Ease.OutCubic));

        // 7) Buka input setelah semua masuk
        seq.AppendCallback(() =>
        {
            if (selectController) selectController.enabled = true;

            // Rebase semua tombol hover (agar baseline = posisi akhir intro)
            var nudges = GetComponentsInChildren<UIHoverNudgeTint>(true);
            foreach (var n in nudges) n.RebaseNow();

            // NEW: hidupkan komponen hover BARU setelah rebase
            if (hardLockHoverDuringIntro && _hoverers != null)
                foreach (var h in _hoverers) h.enabled = true;

            // Buka raycast/input PALING TERAKHIR
            if (raycastGroup)
            {
                raycastGroup.blocksRaycasts = true;
                raycastGroup.interactable = true;
            }
        });
    }
}
