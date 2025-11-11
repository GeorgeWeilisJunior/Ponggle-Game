using UnityEngine;

public class BallElement : MonoBehaviour
{
    [Header("Prefab visual per elemen (SpriteRenderer-only, TANPA Rigidbody/Collider)")]
    [SerializeField] GameObject neutralPrefab;   // boleh null → fallback ke sprite awal root
    [SerializeField] GameObject firePrefab;
    [SerializeField] GameObject waterPrefab;
    [SerializeField] GameObject windPrefab;
    [SerializeField] GameObject earthPrefab;

    [Header("Fallback SPRITES (opsional; jika null akan pakai sprite awal root)")]
    [SerializeField] Sprite neutralSprite;
    [SerializeField] Sprite fireSprite;
    [SerializeField] Sprite waterSprite;
    [SerializeField] Sprite windSprite;
    [SerializeField] Sprite earthSprite;

    [Header("Options")]
    [SerializeField] bool copySortingFromRoot = true;   // salin sorting layer/order
    [SerializeField] bool stripPhysicsFromVisual = true;

    public enum MatchAxis { Width, Height }
    [SerializeField] bool matchRootSpriteSize = true;
    [SerializeField] MatchAxis matchAxis = MatchAxis.Width;
    [SerializeField] float sizeMultiplier = 1f;         // 1 = pas; >1 sedikit lebih besar

    public ElementType Current { get; private set; } = ElementType.Neutral;

    SpriteRenderer rootSR;        // SR milik prefab Ball (root)
    Sprite initialRootSprite;     // cache sprite awal root (anti invisible)
    GameObject activeVisual;      // instance visual yang sedang aktif

    void Awake()
    {
        rootSR = GetComponent<SpriteRenderer>();
        if (rootSR != null) initialRootSprite = rootSR.sprite;

        SetElement(ElementType.Neutral);
    }

    public void SetElement(ElementType e)
    {
        Current = e;

        // buang visual lama
        if (activeVisual) { Destroy(activeVisual); activeVisual = null; }

        // coba pakai prefab
        var prefab = GetPrefabFor(e);
        if (IsValidVisualPrefab(prefab))
        {
            EnsureRootDisabled();

            activeVisual = Instantiate(prefab, transform);
            var tr = activeVisual.transform;
            tr.localPosition = Vector3.zero;
            tr.localRotation = Quaternion.identity;
            // PENTING: jangan paksa scale=1; kita pertahankan scale awal prefab
            // karena auto-size mengalikan scale yang ada
            // tr.localScale = Vector3.one;

            if (stripPhysicsFromVisual)
            {
                foreach (var rb in activeVisual.GetComponentsInChildren<Rigidbody2D>(true)) Destroy(rb);
                foreach (var col in activeVisual.GetComponentsInChildren<Collider2D>(true)) Destroy(col);
            }

            if (copySortingFromRoot && rootSR)
            {
                foreach (var sr in activeVisual.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    sr.sortingLayerID = rootSR.sortingLayerID;
                    sr.sortingOrder = rootSR.sortingOrder + 1;
                }
            }

            if (matchRootSpriteSize) MatchSizeToRootWorldBounds(activeVisual);
            return;
        }

        // fallback ke sprite di root
        EnsureRootEnabled();
        var spriteToUse = GetSpriteFor(e);
        if (spriteToUse == null) spriteToUse = initialRootSprite; // anti-invisible
        rootSR.sprite = spriteToUse;
    }

    /* ---------------- helpers ---------------- */
    GameObject GetPrefabFor(ElementType e) => e switch
    {
        ElementType.Fire => firePrefab,
        ElementType.Water => waterPrefab,
        ElementType.Wind => windPrefab,
        ElementType.Earth => earthPrefab,
        _ => neutralPrefab
    };

    Sprite GetSpriteFor(ElementType e) => e switch
    {
        ElementType.Fire => fireSprite,
        ElementType.Water => waterSprite,
        ElementType.Wind => windSprite,
        ElementType.Earth => earthSprite,
        _ => neutralSprite
    };

    bool IsValidVisualPrefab(GameObject p)
    {
        if (p == null) return false;
        // Hindari siklus/duplikasi komponen gameplay
        if (p.GetComponent<BallController>() || p.GetComponent<BallElement>())
        {
            Debug.LogWarning($"[BallElement] Prefab '{p.name}' tampak seperti prefab gameplay. Fallback ke sprite.");
            return false;
        }
        return true;
    }

    void EnsureRootEnabled()
    {
        if (!rootSR) rootSR = gameObject.AddComponent<SpriteRenderer>();
        rootSR.enabled = true;
    }

    void EnsureRootDisabled()
    {
        if (rootSR) rootSR.enabled = false;
    }

    // --- KUNCI PERBAIKAN: samakan ukuran DUNIA (world bounds) child dengan root ---
    void MatchSizeToRootWorldBounds(GameObject visualGO)
    {
        if (!rootSR || rootSR.sprite == null) return;

        // target = ukuran dunia sprite root (sudah termasuk transform scale)
        float target = (matchAxis == MatchAxis.Width)
            ? rootSR.bounds.size.x
            : rootSR.bounds.size.y;

        // cari SR terbesar di visual (dalam dunia)
        SpriteRenderer chosen = null;
        float childSize = 0f;

        foreach (var sr in visualGO.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null) continue;

            float s = (matchAxis == MatchAxis.Width)
                ? sr.bounds.size.x    // WORLD size (sudah termasuk semua scale)
                : sr.bounds.size.y;

            if (s > childSize) { childSize = s; chosen = sr; }
        }

        if (chosen == null || childSize <= 0f || target <= 0f) return;

        // faktor skala (kalikan scale yang ada) agar ukuran dunia child == target
        float factor = (target / childSize) * Mathf.Max(0.0001f, sizeMultiplier);
        visualGO.transform.localScale *= factor;
    }
}
