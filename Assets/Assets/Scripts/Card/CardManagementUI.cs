using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class CardManagementUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] CardInventory inventory;

    [Header("Owned Panel")]
    [SerializeField] Transform ownedGrid;       // Content ScrollView kiri
    [SerializeField] CardItemView cardPrefab;
    [SerializeField] ScrollRect ownedScroll;    // untuk notif auto-hide

    [Header("Owned Layout Fix")]
    [Tooltip("Paksa child alignment UpperLeft agar 1 kartu tidak center.")]
    [SerializeField] bool enforceOwnedTopLeft = true;
    [SerializeField] GridLayoutGroup ownedGridLayout; // auto-get if null
    [SerializeField] RectTransform ownedGridRect;     // auto-get if null

    [Header("Picked Panel")]
    [SerializeField] Transform pickedGrid;      // Grid bawah
    [SerializeField] TMP_Text energyText;       // "0/10 Energy Used"
    [SerializeField] Button removeAllButton;    // tombol "Remove" di PickedPanel
    [SerializeField] CanvasGroup pickedEmptyHint;   // <-- NEW

    [Header("Detail Panel (kanan)")]
    [SerializeField] GameObject detailRoot;     // container panel kanan (boleh null)
    [SerializeField] Image detailArt;
    [SerializeField] TMP_Text detailName;
    [SerializeField] TMP_Text detailEnergy;
    [SerializeField] TMP_Text detailRarity;
    [SerializeField] TMP_Text detailDesc;

    [Header("Controls")]
    [SerializeField] Button nextButton;

    [Header("Sorting (Owned)")]
    [SerializeField] Button sortEnergyBtn;
    [SerializeField] Button sortRarityBtn;
    [SerializeField] Button sortAddedBtn;
    [SerializeField] TMP_Text sortEnergyLabel;
    [SerializeField] TMP_Text sortRarityLabel;
    [SerializeField] TMP_Text sortAddedLabel;

    [Header("Sorting Visuals")]
    [SerializeField] Image sortEnergyBox;       // background image pada tombol Energy
    [SerializeField] Image sortRarityBox;       // background image pada tombol Rarity
    [SerializeField] Image sortAddedBox;        // background image pada tombol Added/Date
    [SerializeField] Sprite sortBoxNormal;      // sprite default
    [SerializeField] Sprite sortBoxActive;      // sprite aktif

    [Header("Reject/Warning Feedback")]
    [SerializeField] float shakeTime = .25f;
    [SerializeField] float shakeAmp = 12f;
    [SerializeField] string rejectSfxKey = "";
    [SerializeField] NoEnergyBanner noEnergy;   // drag obj "NoENERGY" (Image + CanvasGroup)

    [Header("SFX (UI buttons)")]
    [SerializeField] string sfxClickSort = "UIButton";
    [SerializeField] string sfxClickNext = "UIButton";
    [SerializeField] string sfxClickRemove = "UIButton";

    [Header("SFX (cards)")]
    [SerializeField] string sfxCardClick = "MainMenuClick"; // klik kartu di Owned/Picked
    [SerializeField] string sfxPick = "";                   // saat berpindah ke Picked (opsional)
    [SerializeField] string sfxUnpick = "";                 // saat kembali ke Owned (opsional)

    [Header("BGM")]
    [SerializeField] string bgmKey = "Music_CardManagement";
    [SerializeField] bool playBgmOnStart = true;

    [SerializeField] bool useSceneTransition = true;
    [SerializeField] string transitionSceneName = "_App";
    [SerializeField] NoEnergyBanner duplicateBanner;  

    readonly Dictionary<CardData, CardItemView> ownedMap = new();
    readonly List<CardItemView> spawnedPicked = new();

    enum SortKey { Energy, Rarity, Added }
    struct SortSpec { public SortKey key; public bool ascending; public SortSpec(SortKey k, bool a) { key = k; ascending = a; } }
    readonly List<SortSpec> _sortChain = new(); // index 0 = prioritas tertinggi
    int RarityRank(CardRarity r) => (int)r;
    bool _refreshPending;

    void Start()
    {
        if (!inventory) inventory = CardInventory.I;
        inventory.LoadFromSave();

        // BGM sekali saat masuk scene
        if (playBgmOnStart && AudioManager.I != null && !string.IsNullOrEmpty(bgmKey))
            AudioManager.I.StopMusic();                  // agar gameplay BGM sebelumnya diam
            AudioManager.I.PlayOverlayMusic(bgmKey);     // card management pakai overlay channel

        // --- Owned layout fix (hindari 1 kartu jadi center) ---
        if (!ownedGridRect && ownedGrid) ownedGridRect = ownedGrid as RectTransform;
        if (!ownedGridLayout && ownedGrid) ownedGridLayout = ownedGrid.GetComponent<GridLayoutGroup>();
        EnsureOwnedLayout();

        // Pastikan tombol sort tidak pakai SpriteSwap (biar tidak bentrok dengan kode kita)
        InitSortButtons();

        // Sort buttons
        if (sortEnergyBtn) sortEnergyBtn.onClick.AddListener(() => { PlayUI(sfxClickSort); ToggleSortButton(SortKey.Energy); });
        if (sortRarityBtn) sortRarityBtn.onClick.AddListener(() => { PlayUI(sfxClickSort); ToggleSortButton(SortKey.Rarity); });
        if (sortAddedBtn) sortAddedBtn.onClick.AddListener(() => { PlayUI(sfxClickSort); ToggleSortButton(SortKey.Added); });

        // Remove All Picked
        if (removeAllButton) removeAllButton.onClick.AddListener(() => { PlayUI(sfxClickRemove); RemoveAllPicked(); });

        if (_sortChain.Count == 0) _sortChain.Add(new SortSpec(SortKey.Rarity, true)); // default

        RefreshOwned();
        BuildPickedFromInventory();
        UpdateSortUI();

        UpdateEnergyUI();
        UpdatePickedEmptyHint();   // <-- NEW
        if (nextButton) nextButton.onClick.AddListener(() => { PlayUI(sfxClickNext); OnClickNext(); });

        // Auto-hide scrollbar notify on scroll
        if (ownedScroll) ownedScroll.onValueChanged.AddListener(_ => UIAutoHideScrollbar.NotifyUserScrolled(ownedScroll));

        // Hide detail di awal (biar tidak ada panel putih)
        HideDetail();
    }

    // OnEnable / OnDisable
    void OnEnable()
    {
        if (!inventory) inventory = CardInventory.I;
        if (inventory != null) inventory.OnInventoryChanged += HandleInventoryChanged;
    }

    void OnDisable()
    {
        if (inventory != null) inventory.OnInventoryChanged -= HandleInventoryChanged;
    }

    // ───────────────────── Owned Layout enforcement ─────────────────────
    void EnsureOwnedLayout()
    {
        if (!enforceOwnedTopLeft) return;

        if (ownedGridLayout)
        {
            ownedGridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            ownedGridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            ownedGridLayout.childAlignment = TextAnchor.UpperLeft;
        }
        if (ownedGridRect)
        {
            // Pivot & anchor top-left (aman untuk content)
            ownedGridRect.pivot = new Vector2(0f, 1f);
            // biarkan anchor sesuai setup designer; kita tidak paksa min/max biar tidak mengacaukan layout
        }
    }

    bool IsElementInfusion(CardData c)
    {
        if (!c) return false;
        var k = (c.effectKey ?? "").Trim();
        return k == "FlameInfusion" || k == "WaterInfusion" || k == "WindInfusion" || k == "EarthInfusion";
    }

    // ─────────────────── UI INIT (Sort Buttons) ───────────────────
    void InitSortButtons()
    {
        // Jika image box belum di-assign, coba ambil Image di objek tombol (aman fallback)
        if (!sortEnergyBox && sortEnergyBtn) sortEnergyBox = sortEnergyBtn.GetComponent<Image>();
        if (!sortRarityBox && sortRarityBtn) sortRarityBox = sortRarityBtn.GetComponent<Image>();
        if (!sortAddedBox && sortAddedBtn) sortAddedBox = sortAddedBtn.GetComponent<Image>();

        void FixButton(Button b)
        {
            if (!b) return;
            if (b.transition == Selectable.Transition.SpriteSwap)
            {
                b.transition = Selectable.Transition.ColorTint; // hindari perang sprite
                b.spriteState = new SpriteState();              // kosongkan supaya Unity tidak swap apa pun
            }
        }
        FixButton(sortEnergyBtn);
        FixButton(sortRarityBtn);
        FixButton(sortAddedBtn);

        // set sprite awal agar konsisten dengan _sortChain default
        ApplySortBoxSprites();
    }

    void ApplySortBoxSprites()
    {
        void SetBox(Image img, bool active)
        {
            if (!img || !sortBoxNormal || !sortBoxActive) return;
            img.sprite = active ? sortBoxActive : sortBoxNormal;
        }
        bool energyActive = _sortChain.Exists(s => s.key == SortKey.Energy);
        bool rarityActive = _sortChain.Exists(s => s.key == SortKey.Rarity);
        bool addedActive = _sortChain.Exists(s => s.key == SortKey.Added);

        SetBox(sortEnergyBox, energyActive);
        SetBox(sortRarityBox, rarityActive);
        SetBox(sortAddedBox, addedActive);
    }

    // ===== OWNED =====
    void RefreshOwned()
    {
        foreach (Transform t in ownedGrid) Destroy(t.gameObject);
        ownedMap.Clear();

        var addedIndex = new Dictionary<CardData, int>();
        for (int i = 0; i < inventory.Owned.Count; i++)
            if (inventory.Owned[i]?.data) addedIndex[inventory.Owned[i].data] = i;

        var source = inventory.Owned.Where(o => o != null && o.data != null).Select(o => o.data);
        var ordered = ApplySort(source, addedIndex);

        foreach (var cd in ordered)
        {
            var v = Instantiate(cardPrefab, ownedGrid);
            v.Bind(cd, ShowDetail);
            v.SetCount(inventory.GetAvailableCount(cd));
            ownedMap[cd] = v;

            EnsurePulse(v); // aktifkan klik feedback + SFX
        }

        PushZerosToBottom();
    }

    IEnumerable<CardData> ApplySort(IEnumerable<CardData> seq, Dictionary<CardData, int> addedIdx)
    {
        var chain = _sortChain.Count > 0 ? _sortChain : new List<SortSpec> { new SortSpec(SortKey.Rarity, true) };
        IOrderedEnumerable<CardData> current = null;

        for (int i = 0; i < chain.Count; i++)
        {
            var spec = chain[i];
            System.Func<CardData, object> keySel = spec.key switch
            {
                SortKey.Energy => (CardData c) => c.energyCost,
                SortKey.Rarity => (CardData c) => RarityRank(c.rarity),
                SortKey.Added => (CardData c) => addedIdx.TryGetValue(c, out var idx) ? idx : int.MaxValue,
                _ => (CardData c) => 0
            };
            current = (i == 0)
                ? (spec.ascending ? seq.OrderBy(keySel) : seq.OrderByDescending(keySel))
                : (spec.ascending ? current.ThenBy(keySel) : current.ThenByDescending(keySel));
        }
        return current.ThenBy(c => c.displayName);
    }

    void ToggleSortButton(SortKey key)
    {
        bool combine = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        int idx = _sortChain.FindIndex(s => s.key == key);

        if (!combine)
        {
            // 3-state: Asc -> Desc -> Off
            if (idx < 0) _sortChain.SetAsSingle(new SortSpec(key, true));
            else if (_sortChain[idx].ascending) _sortChain.SetAsSingle(new SortSpec(key, false));
            else _sortChain.Clear();
        }
        else
        {
            // Multi-sort chain (Shift): klik untuk add/toggle arah/remove
            if (idx < 0) _sortChain.Add(new SortSpec(key, true));
            else if (_sortChain[idx].ascending) _sortChain[idx] = new SortSpec(key, false);
            else _sortChain.RemoveAt(idx);
        }

        if (_sortChain.Count == 0) _sortChain.Add(new SortSpec(SortKey.Rarity, true));

        UpdateSortUI();
        RefreshOwned();
    }

    void UpdateSortUI()
    {
        string Label(SortKey k, string baseText)
        {
            int i = _sortChain.FindIndex(s => s.key == k);
            if (i < 0) return baseText;
            return baseText + (_sortChain[i].ascending ? " ▲" : " ▼");
        }
        if (sortEnergyLabel) sortEnergyLabel.text = Label(SortKey.Energy, "Energy");
        if (sortRarityLabel) sortRarityLabel.text = Label(SortKey.Rarity, "Rarity");
        if (sortAddedLabel) sortAddedLabel.text = Label(SortKey.Added, "Added");

        ApplySortBoxSprites(); // sinkronkan sprite box
    }

    // ===== PICKED =====
    void BuildPickedFromInventory()
    {
        foreach (Transform t in pickedGrid) Destroy(t.gameObject);
        spawnedPicked.Clear();

        foreach (var c in inventory.Picked)
        {
            var v = Instantiate(cardPrefab, pickedGrid);
            v.Bind(c, ShowDetail);
            v.SetCount(0);
            v.SendMessage("RefreshStyle", SendMessageOptions.DontRequireReceiver);
            EnsurePulse(v);
            spawnedPicked.Add(v);
            UpdatePickedEmptyHint();
        }
    }

    public void TryMoveToPicked(CardItemView view)
    {
        // kalau asal dari picked, no-op
        if (view.transform.IsChildOf(pickedGrid))
        {
            view.transform.SetParent(pickedGrid, false);
            view.SendMessage("RefreshStyle", SendMessageOptions.DontRequireReceiver);
            return;
        }

        var card = view.Card;

        if (IsElementInfusion(card))
        {
            bool alreadyHasInfusion = inventory.Picked.Any(p => IsElementInfusion(p));
            if (alreadyHasInfusion)
            {
                if (duplicateBanner) duplicateBanner.Pulse("ONLY ONE ELEMENT INFUSION");
                else if (noEnergy) noEnergy.Pulse("ONLY ONE ELEMENT INFUSION");
                Reject(view);
                return;
            }
        }


        // cek overflow energi → tampilkan banner
        bool energyWouldOverflow = false;
        if (card) energyWouldOverflow = (inventory.EnergyUsed + card.energyCost) > inventory.EnergyLimit;

        if (!inventory.CanPick(card, out var why))
        {
            if (why == "energy")
            {
                if (noEnergy) noEnergy.Pulse("NO ENERGY LEFT");
            }
            else if (why == "duplicate")
            {
                if (duplicateBanner) duplicateBanner.Pulse("ONLY ONE PER CARD");
                else if (noEnergy) noEnergy.Pulse("ONLY ONE PER CARD");
            }
            Reject(view);
            return;
        }

        // pick 1 copy → buat clone di picked
        inventory.TryPick(card, out _);

        var clone = Instantiate(cardPrefab, pickedGrid);
        clone.Bind(card, ShowDetail);
        clone.SetCount(0);
        clone.SendMessage("RefreshStyle", SendMessageOptions.DontRequireReceiver);
        EnsurePulse(clone);
        spawnedPicked.Add(clone);

        if (!string.IsNullOrEmpty(sfxPick)) PlayUI(sfxPick);

        UpdateEnergyUI();
        RefreshOwnedCountsOnly();
        UpdatePickedEmptyHint();
    }

    void UpdatePickedEmptyHint()
    {
        if (!pickedEmptyHint) return;

        // buang referensi yang sudah di-Destroy (Unity null)
        spawnedPicked.RemoveAll(v => v == null);

        bool empty = spawnedPicked.Count == 0;    // ← patokan utama
        pickedEmptyHint.alpha = empty ? 1f : 0f;
        pickedEmptyHint.interactable = false;
        pickedEmptyHint.blocksRaycasts = false;
    }

    public void TryMoveToOwned(CardItemView view)
    {
        if (view.transform.IsChildOf(pickedGrid))
        {
            if (inventory.TryUnpick(view.Card))
            {
                spawnedPicked.Remove(view);
                Destroy(view.gameObject);
                if (!string.IsNullOrEmpty(sfxUnpick)) PlayUI(sfxUnpick);
                UpdateEnergyUI();
                RefreshOwnedCountsOnly();
                UpdatePickedEmptyHint();
            }
            else
            {
                view.transform.SetParent(pickedGrid, false);
                view.SendMessage("RefreshStyle", SendMessageOptions.DontRequireReceiver);
            }
            return;
        }

        if (ownedMap.TryGetValue(view.Card, out var ownedView) && ownedView)
            view.transform.SetParent(ownedView.transform.parent, false);
        else
            view.transform.SetParent(ownedGrid, false);

        view.SendMessage("RefreshStyle", SendMessageOptions.DontRequireReceiver);
    }

    // Hapus semua kartu di PickedPanel
    void RemoveAllPicked()
    {
        // kumpulkan lebih dulu (hindari mutasi sambil iterate)
        var views = new List<CardItemView>();
        for (int i = 0; i < pickedGrid.childCount; i++)
        {
            var v = pickedGrid.GetChild(i).GetComponent<CardItemView>();
            if (v) views.Add(v);
        }

        // unpick dari inventory + hapus view
        foreach (var v in views)
        {
            if (v && v.Card) inventory.TryUnpick(v.Card);
            if (v) Destroy(v.gameObject);
        }
        spawnedPicked.Clear();

        UpdateEnergyUI();
        RefreshOwnedCountsOnly();
        UpdatePickedEmptyHint();
    }

    void HandleInventoryChanged()
    {
        if (!isActiveAndEnabled) return;
        if (_refreshPending) return;
        StartCoroutine(CoRefreshOwnedDebounced());
    }

    System.Collections.IEnumerator CoRefreshOwnedDebounced()
    {
        _refreshPending = true;
        yield return null; // tunggu end-of-frame; kumpulkan perubahan batch
                           // Sinkron ulang dari save (agar jumlah & daftar owned akurat)
        inventory.LoadFromSave();

        // Rebuild kolom Owned (karena item baru bisa muncul)
        RefreshOwned();

        // Picked biarkan apa adanya; cukup sinkronkan jumlah badge & energy
        RefreshOwnedCountsOnly();
        UpdateEnergyUI();
        HideDetail();
        UpdatePickedEmptyHint();   // <-- NEW

        _refreshPending = false;
    }


    void RefreshOwnedCountsOnly()
    {
        foreach (var kv in ownedMap)
        {
            var cd = kv.Key;
            var v = kv.Value;
            if (!cd || !v) continue;
            v.SetCount(inventory.GetAvailableCount(cd));
            v.SendMessage("RefreshStyle", SendMessageOptions.DontRequireReceiver);
        }
        PushZerosToBottom();
    }

    int GetPickableCount(CardData c)
    {
        // 0 kalau sudah ada di Picked, 1 kalau belum, 0 kalau tidak punya sama sekali
        int ownedCount = inventory.GetOwnedCount(c);
        if (ownedCount <= 0) return 0;
        return inventory.GetPickedCount(c) > 0 ? 0 : 1;
    }


    void PushZerosToBottom()
    {
        var children = new List<CardItemView>();
        for (int i = 0; i < ownedGrid.childCount; i++)
        {
            var v = ownedGrid.GetChild(i).GetComponent<CardItemView>();
            if (v) children.Add(v);
        }
        var nonZero = children.Where(v => v.Count > 0).ToList();
        var zeros = children.Where(v => v.Count == 0).ToList();

        int idx = 0;
        foreach (var v in nonZero) v.transform.SetSiblingIndex(idx++);
        foreach (var v in zeros) v.transform.SetSiblingIndex(idx++);
    }

    // ===== misc =====
    void UpdateEnergyUI()
    {
        if (energyText) energyText.text = $"{inventory.EnergyUsed}/{inventory.EnergyLimit}";
    }

    void Reject(CardItemView view)
    {
        if (!string.IsNullOrEmpty(rejectSfxKey) && AudioManager.I) AudioManager.I.PlayUI(rejectSfxKey);
        StartCoroutine(Shake(view.GetComponent<RectTransform>()));

        Transform parent = view.transform.parent;
        if (ownedMap.TryGetValue(view.Card, out var ownedView))
            parent = ownedView.transform;

        view.transform.SetParent(parent, false);
        view.SendMessage("RefreshStyle", SendMessageOptions.DontRequireReceiver);
    }

    System.Collections.IEnumerator Shake(RectTransform rt)
    {
        Vector2 start = rt.anchoredPosition;
        float t = 0f;
        while (t < shakeTime)
        {
            t += Time.deltaTime;
            float s = Mathf.Sin(t / shakeTime * Mathf.PI * 4f) * shakeAmp;
            rt.anchoredPosition = start + new Vector2(s, 0f);
            yield return null;
        }
        rt.anchoredPosition = start;
    }

    // ───────────────────── Detail Panel helpers ─────────────────────
    void HideDetail()
    {
        // sembunyikan panel terlebih dahulu
        if (detailRoot) detailRoot.SetActive(false);

        if (detailArt)
        {
            detailArt.sprite = null;
            detailArt.enabled = false;           // penting: hindari putih kosong
        }
        if (detailName) detailName.text = "";
        if (detailEnergy) detailEnergy.text = "";
        if (detailRarity) detailRarity.text = "";
        if (detailDesc) detailDesc.text = "";
    }

    void ShowDetail(CardItemView v)
    {
        var c = v.Card; if (!c) { HideDetail(); return; }

        if (detailRoot && !detailRoot.activeSelf) detailRoot.SetActive(true);

        Sprite sp = c.fullCardSprite ? c.fullCardSprite : c.icon;
        if (detailArt)
        {
            detailArt.sprite = sp;
            detailArt.enabled = (sp != null);
            detailArt.preserveAspect = true;
        }

        if (detailName) detailName.text = c.displayName;
        if (detailEnergy) detailEnergy.text = $"Energy: {c.energyCost}";
        if (detailRarity) detailRarity.text = c.rarity.ToString();
        if (detailDesc) detailDesc.text = c.description;
    }

    public void OnClickNext()
    {
        Debug.Log("[Cards] UI pickedGrid count = " + pickedGrid.childCount);
        // 1) Baca kartu yang benar-benar ada di panel Picked (grid UI) → PALING AMAN
        var pickedIds = new List<string>();
        for (int i = 0; i < pickedGrid.childCount; i++)
        {
            var v = pickedGrid.GetChild(i).GetComponent<CardItemView>();
            if (v && v.Card && v.Card.id != null)
                pickedIds.Add(v.Card.id);
        }

        // 2) Fallback: kalau entah kenapa grid kosong tapi inventory punya data, pakai inventory
        if (pickedIds.Count == 0 && inventory != null && inventory.Picked != null)
        {
            foreach (var c in inventory.Picked)
                if (c != null && !string.IsNullOrEmpty(c.id))
                    pickedIds.Add(c.id);
        }

        // 3) Simpan dan log
        int limit =
            (inventory && inventory.EnergyLimit > 0) ? inventory.EnergyLimit :
            (SaveManager.I != null && SaveManager.I.Data != null && SaveManager.I.Data.energyLimit > 0) ? SaveManager.I.Data.energyLimit :
            10; // fallback default
        SaveManager.I.SetPickedForNextLevel(pickedIds, limit);
        Debug.Log("[Cards] Saved pickedForNext → [" + string.Join(",", pickedIds) + "]  (limit=" + limit + ")");

        AudioManager.I?.StopOverlayMusic();
        // 4) Lanjut ke level berikutnya
        LevelManager.Instance.LoadNext();
    }

    public void RefreshAllNow()
    {
        inventory.LoadFromSave();
        RefreshOwned();
        RefreshOwnedCountsOnly();
        UpdateEnergyUI();
        HideDetail();
    }

    void PlayUI(string key)
    {
        if (string.IsNullOrEmpty(key) || AudioManager.I == null) return;
        AudioManager.I.PlayUI(key);
    }

    void EnsurePulse(CardItemView v)
    {
        if (!v) return;
        var pulse = v.GetComponent<UIClickPulse>();
        if (!pulse) pulse = v.gameObject.AddComponent<UIClickPulse>();

        // Preset rasa "subtle" + aktifkan SFX klik kartu
        pulse.useTilt = true;
        pulse.tiltDeg = 2f;
        pulse.pressScale = 0.97f;
        pulse.releaseOvershoot = 1.04f;
        pulse.clickPulseScale = 1.06f;
        pulse.sfxClickKey = sfxCardClick;   // <-- bunyi saat klik kartu
    }
}

static class ListExt
{
    public static void SetAsSingle<T>(this List<T> list, T item)
    {
        list.Clear();
        list.Add(item);
    }
}
