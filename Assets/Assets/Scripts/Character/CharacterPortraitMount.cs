using UnityEngine;

[DefaultExecutionOrder(50)]
public class CharacterPortraitMount : MonoBehaviour
{
    [Header("Target Circle (bola biru)")]
    [SerializeField] SpriteRenderer circleRenderer;
    [SerializeField] SpriteMask circleMask;

    [Header("Fit Source (pilih salah satu)")]
    [SerializeField] Transform fitLeftEdge;
    [SerializeField] Transform fitRightEdge;
    [SerializeField] float manualWorldDiameter = -1f;
    [SerializeField, Range(0.1f, 1f)] float innerCirclePercent = 0.58f;

    [Header("Fit & Pos")]
    [SerializeField] bool autoFitToCircle = true;
    [SerializeField, Range(0.2f, 1f)] float fillPercent = 0.78f;
    [SerializeField] Vector3 localOffset;

    [Header("Sorting")]
    [SerializeField] string sortingLayerName = "HUDWorld";
    [SerializeField] int sortingOrder = 2;

    [Header("Manual Scale (jika autoFit=OFF)")]
    [SerializeField] float manualUniformScale = 0.2f;

    [Header("Safety Clamp")]
    [SerializeField] float minScale = 0.01f;
    [SerializeField] float maxScale = 2.0f;

    GameObject spawned;

    void OnEnable()
    {
        // subscribe biar ganti portrait ketika karakter berubah
        if (CharacterPowerManager.Instance != null)
            CharacterPowerManager.Instance.OnCharacterChanged += HandleCharacterChanged;

        Refresh(); // initial
        // berjaga-jaga kalau CPM apply setelah frame ini:
        StartCoroutine(RefreshNextFrame());
    }

    void OnDisable()
    {
        if (CharacterPowerManager.Instance != null)
            CharacterPowerManager.Instance.OnCharacterChanged -= HandleCharacterChanged;
    }

    System.Collections.IEnumerator RefreshNextFrame()
    {
        yield return null; // tunggu 1 frame — CPM biasanya ApplyFromSave() di Start()
        Refresh();
    }

    void HandleCharacterChanged(CharacterData _)
    {
        Refresh();
    }

    [ContextMenu("Refresh Now")]
    public void Refresh()
    {
        if (spawned)
        {
            if (Application.isPlaying) Destroy(spawned);
            else DestroyImmediate(spawned);
        }

        var ch = CharacterPowerManager.Instance
                 ? CharacterPowerManager.Instance.GetCurrentCharacter()
                 : null;
        if (!ch || !ch.launcherDisplayPrefab) return;

        spawned = Instantiate(ch.launcherDisplayPrefab, transform);
        spawned.transform.localPosition = localOffset;
        spawned.transform.localRotation = Quaternion.identity;
        spawned.transform.localScale = Vector3.one;

        // ── Sorting & Masking ──
        var srs = spawned.GetComponentsInChildren<SpriteRenderer>(true);
        bool maskUsable = (circleMask && circleMask.sprite != null);

        foreach (var sr in srs)
        {
            if (!string.IsNullOrEmpty(sortingLayerName))
                sr.sortingLayerName = sortingLayerName;

            int order = sortingOrder;
            var bump = sr.GetComponent<SortingBump>();
            if (bump) order += bump.delta;
            else if (sr.gameObject.name.ToLower().Contains("pupil")) order += 1;

            sr.sortingOrder = order;
            sr.maskInteraction = maskUsable
                ? SpriteMaskInteraction.VisibleInsideMask
                : SpriteMaskInteraction.None;
        }

        float circleWorldDiameter = ComputeTargetDiameterWorld();

        if (autoFitToCircle && TryGetWorldBounds(spawned, out Bounds b))
        {
            float target = circleWorldDiameter * fillPercent;
            float sizeMax = Mathf.Max(b.size.x, b.size.y);
            float s = sizeMax > 1e-5f ? target / sizeMax : 1f;
            s = Mathf.Clamp(s, minScale, maxScale);
            spawned.transform.localScale = Vector3.one * s;
        }
        else
        {
            spawned.transform.localScale = Vector3.one * Mathf.Clamp(manualUniformScale, minScale, maxScale);
        }
    }

    float ComputeTargetDiameterWorld()
    {
        if (fitLeftEdge && fitRightEdge)
            return Vector3.Distance(fitLeftEdge.position, fitRightEdge.position);

        if (manualWorldDiameter > 0f)
            return manualWorldDiameter;

        if (circleRenderer && circleRenderer.sprite != null)
        {
            float spriteW = circleRenderer.sprite.bounds.size.x;
            float worldW = spriteW * circleRenderer.transform.lossyScale.x;
            return worldW * innerCirclePercent;
        }
        return 1f * innerCirclePercent;
    }

    bool TryGetWorldBounds(GameObject go, out Bounds b)
    {
        var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
        if (srs.Length == 0) { b = new Bounds(go.transform.position, Vector3.zero); return false; }
        b = srs[0].bounds;
        for (int i = 1; i < srs.Length; i++) b.Encapsulate(srs[i].bounds);
        return true;
    }
}
