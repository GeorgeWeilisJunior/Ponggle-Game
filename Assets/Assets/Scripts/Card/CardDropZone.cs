using UnityEngine;
using UnityEngine.EventSystems;

public enum DropZoneType { Owned, Picked, Detail }

[RequireComponent(typeof(RectTransform))]
public class CardDropZone : MonoBehaviour,
    IDropHandler,
    IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public DropZoneType type = DropZoneType.Owned;
    public CardManagementUI ui;

    public void OnDrop(PointerEventData eventData) { /* no-op */ }

    public void HandleDrop(CardItemView view)
    {
        if (!ui || !view) return;
        switch (type)
        {
            case DropZoneType.Picked: ui.TryMoveToPicked(view); break;
            case DropZoneType.Owned: ui.TryMoveToOwned(view); break;
            default: ui.TryMoveToOwned(view); break;
        }
    }

    // Telan scroll/drag di Picked agar ScrollRect Owned tidak ikut bergerak
    public void OnScroll(PointerEventData e) { if (type == DropZoneType.Picked) e.Use(); }
    public void OnBeginDrag(PointerEventData e) { if (type == DropZoneType.Picked) e.Use(); }
    public void OnDrag(PointerEventData e) { if (type == DropZoneType.Picked) e.Use(); }
    public void OnEndDrag(PointerEventData e) { if (type == DropZoneType.Picked) e.Use(); }
}
