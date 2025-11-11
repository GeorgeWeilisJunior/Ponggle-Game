using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class NameEntryUI : MonoBehaviour
{
    [Header("Refs")]
    public CanvasGroup cg;          // optional (kalau ada PanelFader, tetap oke)
    public Button btnClose;
    public TMP_Text txtA, txtB, txtC;

    // Bisa Image atau TMP_Text
    public Graphic usA, usB, usC;   // underscore A/B/C
    public Button btnConfirm;

    [Header("Visual")]
    public Color normalColor = Color.white;
    public Color highlightColor = new Color(1f, 0.85f, 0.3f);
    public float blinkSpeed = 6f;
    [Range(0f, 1f)] public float offAlpha = 0.20f;
    [Range(0f, 1f)] public float idleAlpha = 0.55f;

    [Header("Integration")]
    public MenuManager menu;        // drag MenuManager
    public string firstLevelSceneOverride = "";

    [Header("Audio")]
    [SerializeField] string sfxMoveKey = "UICarousel"; // untuk panah kiri/kanan/atas/bawah
    [SerializeField] string sfxConfirmKey = "UIClick"; // untuk tombol Confirm / Enter
    [SerializeField] string sfxCloseKey = "UIClick";   // untuk tombol Close / Escape

    int cursor = 0;                 // 0=A,1=B,2=C
    char[] letters = new char[3] { 'A', 'A', 'A' };
    Coroutine blinkCo;

    public void UI_SelectA() => SetCursor(0);
    public void UI_SelectB() => SetCursor(1);
    public void UI_SelectC() => SetCursor(2);

    void Reset()
    {
        cg = GetComponent<CanvasGroup>();
    }

    void Awake()
    {
        if (btnClose) btnClose.onClick.AddListener(Hide);
        if (btnConfirm) btnConfirm.onClick.AddListener(OnConfirm);

        // Init dari SaveManager (fallback "YOU")
        string saved = (SaveManager.I != null) ? SaveManager.I.Data.playerName : "YOU";
        saved = string.IsNullOrWhiteSpace(saved) ? "YOU" : saved.Trim().ToUpperInvariant();

        if (saved.Length >= 3)
        {
            letters[0] = saved[0];
            letters[1] = saved[1];
            letters[2] = saved[2];
        }

        RefreshTexts();
        SetCursor(0);
        HideImmediate();
    }

    void Update()
    {
        if (!gameObject.activeInHierarchy) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) UI_Left();
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) UI_Right();
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) UI_Up();
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) UI_Down();

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            OnConfirm();

        if (Input.GetKeyDown(KeyCode.Escape)) Hide();
    }

    // ==== Show/Hide ====
    public void Show()
    {
        gameObject.SetActive(true);
        RefreshTexts();
        SetCursor(0);
        if (menu) menu.ShowDim();

        if (TryGetComponent<PanelFader>(out var pf)) pf.Show();
        else if (cg) { cg.alpha = 1f; cg.blocksRaycasts = true; cg.interactable = true; }
    }
    public void Hide() => Hide(true);
    public void Hide(bool playSfx = true)
    {
        if (playSfx) PlayClose();   // <--- hanya bunyi kalau diminta
        StopBlink();

        if (TryGetComponent<PanelFader>(out var pf))
        {
            pf.Hide(() => {
                gameObject.SetActive(false);
                if (menu) menu.HideDim();
            });
        }
        else
        {
            if (cg) { cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false; }
            gameObject.SetActive(false);
            if (menu) menu.HideDim();
        }
    }

    public void HideImmediate()
    {
        if (cg) { cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false; }
        gameObject.SetActive(false);
        StopBlink();
    }

    // ==== Logic + UI hooks ====
    public void UI_Left() { PlayMove(); MoveCursor(-1); }
    public void UI_Right() { PlayMove(); MoveCursor(+1); }
    public void UI_Up() { PlayMove(); NudgeLetter(+1); }
    public void UI_Down() { PlayMove(); NudgeLetter(-1); }

    void MoveCursor(int dir)
    {
        cursor = Mathf.Clamp(cursor + dir, 0, 2);
        SetCursor(cursor);
    }

    void SetCursor(int i)
    {
        cursor = Mathf.Clamp(i, 0, 2);

        if (txtA) txtA.color = (cursor == 0) ? highlightColor : normalColor;
        if (txtB) txtB.color = (cursor == 1) ? highlightColor : normalColor;
        if (txtC) txtC.color = (cursor == 2) ? highlightColor : normalColor;

        SetGraphicAlpha(usA, idleAlpha);
        SetGraphicAlpha(usB, idleAlpha);
        SetGraphicAlpha(usC, idleAlpha);

        StopBlink();
        blinkCo = StartCoroutine(BlinkUnderscore());
    }

    IEnumerator BlinkUnderscore()
    {
        Graphic active = cursor == 0 ? usA : cursor == 1 ? usB : usC;
        if (!active) yield break;

        while (true)
        {
            float t = (Mathf.Sin(Time.unscaledTime * blinkSpeed) + 1f) * 0.5f; // 0..1
            float a = Mathf.Lerp(offAlpha, 1f, t);
            SetGraphicAlpha(active, a);
            yield return null;
        }
    }

    void StopBlink()
    {
        if (blinkCo != null) { StopCoroutine(blinkCo); blinkCo = null; }
    }

    void SetGraphicAlpha(Graphic g, float a)
    {
        if (!g) return;
        var c = g.color; c.a = a; g.color = c;
    }

    void NudgeLetter(int delta)
    {
        int idx = cursor;
        char c = letters[idx];
        if (c < 'A' || c > 'Z') c = 'A';
        int off = c - 'A';
        off = (off + delta) % 26;
        if (off < 0) off += 26;
        letters[idx] = (char)('A' + off);

        RefreshTexts();
    }

    void RefreshTexts()
    {
        if (txtA) txtA.text = letters[0].ToString();
        if (txtB) txtB.text = letters[1].ToString();
        if (txtC) txtC.text = letters[2].ToString();
    }

    void OnConfirm()
    {
        PlayConfirm();  // bunyi sekali di sini

        string initials = new string(letters).ToUpperInvariant();
        if (SaveManager.I) SaveManager.I.NewGame(initials);

        Hide(false);    // tutup tanpa SFX close
        if (menu) menu.StartNewGameAfterName(firstLevelSceneOverride, /*playSfx:*/ false);
    }
    // ==== SFX helpers ====
    void PlayMove()
    {
        if (AudioManager.I != null && !string.IsNullOrEmpty(sfxMoveKey))
            AudioManager.I.PlayUI(sfxMoveKey);
    }

    void PlayConfirm()
    {
        if (AudioManager.I != null && !string.IsNullOrEmpty(sfxConfirmKey))
            AudioManager.I.PlayUI(sfxConfirmKey);
    }

    void PlayClose()
    {
        if (AudioManager.I != null && !string.IsNullOrEmpty(sfxCloseKey))
            AudioManager.I.PlayUI(sfxCloseKey);
    }
}
