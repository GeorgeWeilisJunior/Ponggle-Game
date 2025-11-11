using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class CardItemView : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerClickHandler, IScrollHandler
{
    [Header("Sub-views")]
    [SerializeField] CardSpriteView spriteView;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text costText;          // dipakai utk JUMLAH -> "xN"
    [SerializeField] TMP_Text rarityText;

    [Header("Optional: root badge angka (ikon+angka)")]
    [SerializeField] GameObject energyBadgeRoot;

    public CardData Card { get; private set; }

    RectTransform rt;
    Canvas rootCanvas;
    CanvasGroup cg;
    Transform originalParent;
    GameObject ghost;

    System.Action<CardItemView> onClick;

    // jumlah available yg ditampilkan di bubble
    int _count = 0;
    public int Count => _count; // dipakai UI utk re-order zero ke bawah

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
    }

    void OnEnable() => RefreshStyle();
    void OnTransformParentChanged() => RefreshStyle();

    void OnDisable()
    {
        if (ghost) { Destroy(ghost); ghost = null; }
        if (cg) cg.blocksRaycasts = true;
    }

    public void Bind(CardData data, System.Action<CardItemView> onClickCb)
    {
        Card = data;
        onClick = onClickCb;

        if (!rootCanvas) rootCanvas = GetComponentInParent<Canvas>();

        if (spriteView) spriteView.Bind(data);
        if (nameText) nameText.text = data ? data.displayName : "";
        if (rarityText) rarityText.text = data ? data.rarity.ToString() : "";

        UpdateCountVisual();
        RefreshStyle();
    }

    public void RefreshStyle()
    {
        var zone = GetComponentInParent<CardDropZone>();
        bool inPicked = zone && zone.type == DropZoneType.Picked;
        bool showBubble = !inPicked && _count > 0;

        if (energyBadgeRoot) energyBadgeRoot.SetActive(showBubble);
        if (costText) costText.gameObject.SetActive(showBubble);

        // ⬇️ JANGAN abu-abu di Picked
        if (cg) cg.alpha = inPicked ? 1f : (_count > 0 ? 1f : 0.35f);
    }

    // === jumlah (available) ===
    public void SetCount(int count)
    {
        _count = Mathf.Max(0, count);
        UpdateCountVisual();
    }

    void UpdateCountVisual()
    {
        var zone = GetComponentInParent<CardDropZone>();
        bool inPicked = zone && zone.type == DropZoneType.Picked;
        bool showBubble = !inPicked && _count > 0;

        if (energyBadgeRoot) energyBadgeRoot.SetActive(showBubble);
        if (costText)
        {
            costText.gameObject.SetActive(showBubble);
            if (showBubble) costText.text = $"x{_count}";
        }

        // ⬇️ JANGAN abu-abu di Picked
        if (cg) cg.alpha = inPicked ? 1f : (_count > 0 ? 1f : 0.35f);
    }

    public void OnPointerClick(PointerEventData _) => onClick?.Invoke(this);

    // === Drag & Drop ===
    public void OnBeginDrag(PointerEventData e)
    {
        originalParent = transform.parent;
        cg.blocksRaycasts = false;

        // ghost di atas semua
        ghost = new GameObject("CardGhost", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(Canvas));
        var grt = ghost.GetComponent<RectTransform>();
        grt.SetParent(rootCanvas ? rootCanvas.transform : transform.root, false);
        grt.sizeDelta = rt.rect.size;

        var img = ghost.GetComponent<Image>();
        var art = spriteView ? spriteView.GetComponent<Image>() : null;
        img.sprite = art ? art.sprite : null;
        img.preserveAspect = true;
        img.raycastTarget = false;

        var gcg = ghost.GetComponent<CanvasGroup>();
        gcg.alpha = .7f;
        gcg.blocksRaycasts = false;
        gcg.ignoreParentGroups = true;

        var c = ghost.GetComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder = 9999;

        ghost.transform.position = e.position;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!ghost) return;

        // >>> Tidak ada kunci X lagi — ghost selalu mengikuti pointer
        ghost.transform.position = e.position;
    }

    public void OnEndDrag(PointerEventData e)
    {
        cg.blocksRaycasts = true;

        if (ghost) { Destroy(ghost); ghost = null; }

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(e, results);

        CardDropZone zone = null;
        foreach (var r in results)
        {
            zone = r.gameObject.GetComponentInParent<CardDropZone>();
            if (zone) break;
        }

        if (zone) zone.HandleDrop(this);
        else transform.SetParent(originalParent, false);
    }

    // === Forward scroll ke ScrollRect induk supaya tetap bisa scroll walau pointer di atas kartu ===
    public void OnScroll(PointerEventData eventData)
    {
        var sr = GetComponentInParent<ScrollRect>();
        if (sr)
        {
            ExecuteEvents.Execute<IScrollHandler>(sr.gameObject, eventData, ExecuteEvents.scrollHandler);
        }
    }
}
