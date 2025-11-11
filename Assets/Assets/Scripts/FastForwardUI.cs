using UnityEngine;

public class FastForwardUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] FastForwardController controller; // drag komponen di scene
    [SerializeField] GameObject unlockedRoot;          // FFArea (container) – tidak pernah dimatikan
    [SerializeField] GameObject lockedGO;              // ikon gembok/overlay

    void Reset()
    {
        controller = FindObjectOfType<FastForwardController>(true);
    }

    void OnEnable()
    {
        if (!controller) controller = FindObjectOfType<FastForwardController>(true);

        // sinkron awal
        RefreshLockVisual();

        // subscribe
        if (controller)
        {
            controller.OnUnlockedChanged += _ => RefreshLockVisual();
            controller.OnActiveChanged += _ => RefreshLockVisual();
        }

        // pastikan root UI tidak pernah nonaktif
        if (!unlockedRoot) unlockedRoot = gameObject;
        if (!unlockedRoot.activeSelf) unlockedRoot.SetActive(true);

        // pastikan semua child image tidak menutupi klik area lain
        var graphics = unlockedRoot.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
        foreach (var g in graphics) g.raycastTarget = false;
    }

    void OnDisable()
    {
        if (controller)
        {
            controller.OnUnlockedChanged -= _ => RefreshLockVisual();
            controller.OnActiveChanged -= _ => RefreshLockVisual();
        }
    }

    void Update()
    {
        // Poll kecil tiap frame supaya responsif jika flag SaveManager berubah setelah scene load
        RefreshLockVisual();
    }

    void RefreshLockVisual()
    {
        if (!controller)
        {
            if (lockedGO) lockedGO.SetActive(true);
            return;
        }

        bool canFF = controller.CanFastForward();
        if (lockedGO) lockedGO.SetActive(!canFF);
    }
}
