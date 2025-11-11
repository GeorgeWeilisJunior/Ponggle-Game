using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Scrollbar))]
public class ScrollbarHandleFix : MonoBehaviour
{
    [SerializeField] float minSize = 0.05f;              // cegah size 0
    [SerializeField] bool forceAssignHandleByName = true;

    Scrollbar sb;

    void Awake() { sb = GetComponent<Scrollbar>(); }
    void OnEnable() { StartCoroutine(FixNextFrame()); }

    IEnumerator FixNextFrame()
    {
        yield return null;                    // tunggu layout 1 frame
        Canvas.ForceUpdateCanvases();

        if (forceAssignHandleByName && (!sb.handleRect || sb.handleRect.gameObject.name != "Handle"))
        {
            var handle = transform.Find("Sliding Area/Handle") as RectTransform;
            if (!handle) handle = GetComponentInChildren<RectTransform>(true);
            if (handle) sb.handleRect = handle;
        }

        // aktifkan renderer handle
        if (sb.handleRect)
        {
            var img = sb.handleRect.GetComponent<Image>();
            if (img) img.enabled = true;
            sb.handleRect.gameObject.SetActive(true);
        }

        // cegah size 0 yang bikin handle tidak terlihat saat Play
        if (sb.size < minSize) sb.size = minSize;
    }
}
