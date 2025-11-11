using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SortBoxGroup : MonoBehaviour
{
    public enum SortMode { None = -1, Energy = 0, Rarity = 1, Added = 2 }

    [Serializable]
    public class SortBox
    {
        public string label;

        [Header("Required")]
        public Button button;          // tombol yang diklik

        [Header("Visual Option A: satu Image (pakai sprite)")]
        public Image targetImage;      // Image yang frame-nya diganti
        public Sprite normalSprite;    // SortBox
        public Sprite activeSprite;    // ActiveSortBox

        [Header("Visual Option B: dua GameObject (on/off)")]
        public GameObject normalGO;    // GO frame normal (opsional)
        public GameObject activeGO;    // GO frame aktif (opsional)
    }

    [Header("Buttons (urut: Energy, Rarity, Added/Date)")]
    [SerializeField] SortBox[] boxes = new SortBox[3];

    [Header("Behavior")]
    [SerializeField] bool allowDeselectByReclick = false;
    [SerializeField] SortMode defaultActive = SortMode.None;

    [Header("Safety")]
    [Tooltip("Set ke TRUE untuk mematikan Sprite Swap pada Button agar tidak override sprite.")]
    [SerializeField] bool forceDisableButtonSpriteSwap = true;

    [Tooltip("Paksa pakai overrideSprite supaya tidak diubah oleh transisi/animasi lain.")]
    [SerializeField] bool useOverrideSprite = true;

    [SerializeField] bool logDebug = false;

    public SortMode ActiveMode { get; private set; } = SortMode.None;
    public event Action<SortMode> OnSortChanged;

    void Awake()
    {
        if (forceDisableButtonSpriteSwap)
        {
            foreach (var b in boxes)
                if (b?.button) b.button.transition = Selectable.Transition.None;
        }

        for (int i = 0; i < boxes.Length; i++)
        {
            int idx = i;
            if (boxes[i]?.button)
                boxes[i].button.onClick.AddListener(() => HandleClick(idx));
        }

        ValidateTargets();
        SetActiveInternal(defaultActive, invoke: false);
    }

    void HandleClick(int index)
    {
        var mode = (SortMode)index;
        if (allowDeselectByReclick && ActiveMode == mode)
            SetActiveInternal(SortMode.None);
        else if (ActiveMode != mode)
            SetActiveInternal(mode);
    }

    void SetActiveInternal(SortMode mode, bool invoke = true)
    {
        if (logDebug) Debug.Log($"[SortBoxGroup] SetActive => {mode}");

        // 1) Reset semua dulu
        for (int i = 0; i < boxes.Length; i++)
            ApplyVisual(i, false);

        // 2) Aktifkan yang dipilih
        ActiveMode = mode;
        if (mode != SortMode.None)
            ApplyVisual((int)mode, true);

        if (invoke) OnSortChanged?.Invoke(ActiveMode);
    }

    void ApplyVisual(int index, bool active)
    {
        var b = boxes[index];
        if (b == null) return;

        // Opsi B: dua GO (jika diisi, ini prioritas)
        if (b.activeGO || b.normalGO)
        {
            if (b.activeGO) b.activeGO.SetActive(active);
            if (b.normalGO) b.normalGO.SetActive(!active);
        }

        // Opsi A: satu Image (sprite/overrideSprite)
        if (b.targetImage)
        {
            var spr = active ? b.activeSprite : b.normalSprite;
            if (useOverrideSprite) b.targetImage.overrideSprite = spr;
            else b.targetImage.sprite = spr;
        }
    }

    void ValidateTargets()
    {
        var seen = new HashSet<Image>();
        for (int i = 0; i < boxes.Length; i++)
        {
            var img = boxes[i]?.targetImage;
            if (!img) continue;
            if (!seen.Add(img))
                Debug.LogWarning($"[SortBoxGroup] targetImage duplikat antara elemen array. Cek element index {i}.");
        }
    }

    public void SetActive(SortMode mode) => SetActiveInternal(mode);

#if UNITY_EDITOR
    void OnValidate()
    {
        if (boxes == null || boxes.Length != 3)
            Array.Resize(ref boxes, 3);
    }
#endif
}
