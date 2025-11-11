using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(GridLayoutGroup))]
public class GridChildNormalizer : MonoBehaviour
{
    public bool normalizeOnEnable = true;
    public bool normalizeEveryFrameInEditor = true; // nyaman saat nyusun UI

    GridLayoutGroup grid;
    RectTransform rt;

    void OnEnable()
    {
        grid = GetComponent<GridLayoutGroup>();
        rt = (RectTransform)transform;
        if (normalizeOnEnable) NormalizeChildren();
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && normalizeEveryFrameInEditor)
            NormalizeChildren();
#endif
    }

    public void NormalizeChildren()
    {
        if (!grid) return;
        var cell = grid.cellSize;

        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i) as RectTransform;
            if (!child) continue;

            // Root Card (yang jadi direct child Grid)
            child.localScale = Vector3.one;
            child.localRotation = Quaternion.identity;
            child.anchorMin = child.anchorMax = new Vector2(0.5f, 0.5f);
            child.pivot = new Vector2(0.5f, 0.5f);
            child.sizeDelta = cell;
            child.anchoredPosition = Vector2.zero;

            // Kalau di dalam Card ada node "Image", set supaya isi parent dgn rasio terjaga
            var image = child.GetComponentInChildren<Image>(true);
            if (image)
            {
                image.preserveAspect = true;
                var irt = image.rectTransform;
                irt.anchorMin = Vector2.zero;
                irt.anchorMax = Vector2.one;
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = Vector2.zero;
                irt.sizeDelta = Vector2.zero; // biar nge-fill cell
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }
}
