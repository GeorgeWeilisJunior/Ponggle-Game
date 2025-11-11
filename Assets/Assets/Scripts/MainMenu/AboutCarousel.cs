using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class AboutCarousel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Pages (GameObjects)")]
    [SerializeField] Transform slidesParent;         // opsional: parent berisi 1of5,2of5,...
    [SerializeField] bool autoCollectChildren = false;
    [SerializeField] List<GameObject> slidePages = new(); // isi manual kalau tidak auto

    [Header("Page Indicator")]
    [SerializeField] Image pageIcon;                    // kecil "1/5"
    [SerializeField] List<Sprite> pageIcons = new();    // sprite 1of5..NofN (opsional)
    [SerializeField] TMP_Text pageLabel;                // fallback teks "1/5" (opsional)

    [Header("Buttons")]
    [SerializeField] Button btnPrev;
    [SerializeField] Button btnNext;
    [SerializeField] Button btnClose;
    [SerializeField] bool loop = false;

    [Header("Menu Link (optional)")]
    [SerializeField] MenuManager menu;                  // drag MenuManager
    [SerializeField] GameObject panelAboutRoot;         // drag Panel_About root (opsional)

    [Header("UI SFX (optional)")]
    [SerializeField] string sfxClickKey = "";
    [SerializeField] string sfxHoverKey = "";
    [SerializeField] bool playClickOnClose = false;


    int index = 0;
    bool hovered;

    void Awake()
    {
        if (!menu) menu = FindObjectOfType<MenuManager>();
        if (!panelAboutRoot) panelAboutRoot = transform.root ? transform.root.gameObject : null;
    }

    void OnEnable()
    {
        if (autoCollectChildren && slidesParent)
        {
            slidePages.Clear();
            for (int i = 0; i < slidesParent.childCount; i++)
                slidePages.Add(slidesParent.GetChild(i).gameObject);
        }

        WireButtons();
        index = Mathf.Clamp(index, 0, Mathf.Max(0, slidePages.Count - 1));
        Refresh();
    }

    void WireButtons()
    {
        if (btnPrev) { btnPrev.onClick.RemoveAllListeners(); btnPrev.onClick.AddListener(Prev); }
        if (btnNext) { btnNext.onClick.RemoveAllListeners(); btnNext.onClick.AddListener(Next); }
        if (btnClose) { btnClose.onClick.RemoveAllListeners(); btnClose.onClick.AddListener(Close); }
    }

    public void SetIndex(int i)
    {
        if (slidePages.Count == 0) return;
        index = Mathf.Clamp(i, 0, slidePages.Count - 1);
        Refresh();
    }

    public void Next()
    {
        if (slidePages.Count == 0) return;
        PlayClick();
        if (index < slidePages.Count - 1) index++;
        else if (loop) index = 0;
        Refresh();
    }

    public void Prev()
    {
        if (slidePages.Count == 0) return;
        PlayClick();
        if (index > 0) index--;
        else if (loop) index = slidePages.Count - 1;
        Refresh();
    }

    void Refresh()
    {
        int n = slidePages.Count;

        // aktif/nonaktif halaman
        for (int i = 0; i < n; i++)
        {
            if (slidePages[i])
                slidePages[i].SetActive(i == index);
        }

        // badge icon
        if (pageIcon)
        {
            if (pageIcons != null && pageIcons.Count == n && n > 0)
            {
                pageIcon.enabled = true;
                pageIcon.sprite = pageIcons[index];
            }
            else pageIcon.enabled = false;
        }

        // fallback teks
        if (pageLabel) pageLabel.text = (n > 0) ? $"{index + 1}/{n}" : "-/-";

        // interaksi tombol
        bool hasPages = n > 0;
        if (btnPrev) btnPrev.interactable = hasPages && (loop || index > 0);
        if (btnNext) btnNext.interactable = hasPages && (loop || index < n - 1);
    }

    public void Close()
    {
        if (playClickOnClose) PlayClick();
        if (menu) menu.HideDialog();
        else if (panelAboutRoot) panelAboutRoot.SetActive(false);
        else gameObject.SetActive(false);
    }

    void PlayClick()
    {
        if (!string.IsNullOrEmpty(sfxClickKey))
            AudioManager.I.PlayUI(sfxClickKey);
    }

    public void OnPointerEnter(PointerEventData _)
    {
        hovered = true;
        if (!string.IsNullOrEmpty(sfxHoverKey))
            AudioManager.I.PlayUI(sfxHoverKey);
    }
    public void OnPointerExit(PointerEventData _) { hovered = false; }
}
