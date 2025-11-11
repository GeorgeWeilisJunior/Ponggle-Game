using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Serializable]
    public class CardEntry
    {
        public ScriptableObject data; // CardData
        public string id;
        public Sprite art;
        public bool owned = false;
        public CardRarity rarity = CardRarity.Common;
    }

    [Header("Refs")]
    [SerializeField] Image detailImage;
    [SerializeField] Transform content;
    [SerializeField] GameObject cardTilePrefab;
    [SerializeField] ScrollRect scrollRect;

    [Header("Sorting Mode")]
    [Tooltip("OFF: rarity → owned → id. ON: owned → rarity → id")]
    [SerializeField] bool ownedFirstGlobally = false;

    [Tooltip("Kalau ON, kartu yang aktif di level ini/picked untuk next akan dinaikkan ke paling atas.")]
    [SerializeField] bool activeFirstGlobally = true;

    [Header("Audio (keys)")]
    [SerializeField] string sfxOpenKey = "InvOpen";
    [SerializeField] string sfxCloseKey = "InvClose";
    [SerializeField] string sfxClickKey = "InvCardClick";

    [Header("Overlay Music")]
    [SerializeField] string overlayMusicKey = "Music_Inventory";
    [SerializeField] bool pauseGameplayMusic = true;

    [Header("Scroll Tuning")]
    [SerializeField, Range(10f, 200f)] float mouseWheelSensitivity = 120f;

    [Header("Active Visuals")]
    [SerializeField] Color activeTint = new Color(0.85f, 1f, 0.95f, 1f);
    [SerializeField] float activeScale = 1.03f;

    readonly List<GameObject> spawned = new();
    readonly List<CardEntry> library = new();
    readonly HashSet<string> _activeIds = new();

    float prevTimeScale = 1f;
    bool gameplayWasPlaying;

    void OnEnable()
    {
        // Pause gameplay & audio
        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        Launcher.Instance?.LockInput();

        AudioManager.I?.PlayUI(sfxOpenKey, ignorePause: true);
        if (pauseGameplayMusic && AudioManager.I != null) { gameplayWasPlaying = AudioManager.I.IsMusicPlaying; AudioManager.I.PauseMusic(); }
        if (!string.IsNullOrEmpty(overlayMusicKey)) AudioManager.I?.PlayOverlayMusic(overlayMusicKey, true);

        if (scrollRect)
        {
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = mouseWheelSensitivity;
            scrollRect.onValueChanged.AddListener(OnScrollRectChanged);
        }

        RebuildLibraryFromSave();
        RebuildActiveIds();
        NormalizeAndSortLibrary();
        BuildGrid();

        if (scrollRect) scrollRect.verticalNormalizedPosition = 1f; // top
        var picked = library.Find(e => e.owned && e.art != null) ?? library.Find(e => e.art != null);
        ShowDetail(picked?.art);
        ForceVerticalOnly();
    }

    void OnDisable()
    {
        AudioManager.I?.PlayUI(sfxCloseKey, ignorePause: true);
        AudioManager.I?.StopOverlayMusic();
        if (pauseGameplayMusic && AudioManager.I != null && gameplayWasPlaying) AudioManager.I.ResumeMusic();

        if (scrollRect) scrollRect.onValueChanged.RemoveListener(OnScrollRectChanged);

        Time.timeScale = prevTimeScale;
        Launcher.Instance?.UnlockInput();
    }

    // ================== Build data ==================

    void RebuildLibraryFromSave()
    {
        library.Clear();

        // Ambil semua CardData dari Resources/Cards
        CardLibrary.EnsureCache();
        var all = Resources.LoadAll<CardData>("Cards");

        // Daftar owned dari Save
        var ownedIds = (SaveManager.I != null) ? SaveManager.I.Data.ownedCards : null;

        foreach (var c in all)
        {
            if (!c) continue;
            library.Add(new CardEntry
            {
                data = c,
                id = c.id,
                art = c.fullCardSprite ? c.fullCardSprite : c.icon,
                rarity = c.rarity,
                owned = ownedIds != null && ownedIds.Contains(c.id),
            });
        }
    }

    void RebuildActiveIds()
    {
        _activeIds.Clear();
        if (SaveManager.I == null || SaveManager.I.Data == null) return;

        // 1) Prioritas: kartu yang AKTIF di level berjalan
        var active = SaveManager.I.GetActiveCardsThisLevel();
        if (active != null && active.Count > 0)
        {
            foreach (var id in active) if (!string.IsNullOrEmpty(id)) _activeIds.Add(id);
            return;
        }

        // 2) Fallback: kartu yang dipick untuk next (sebelum level mulai)
        var picked = SaveManager.I.Data.pickedForNext;
        if (picked != null)
            foreach (var id in picked) if (!string.IsNullOrEmpty(id)) _activeIds.Add(id);
    }

    // ================== UI build ==================

    void OnScrollRectChanged(Vector2 _) => ForceVerticalOnly();
    void LateUpdate() => ForceVerticalOnly();

    void ForceVerticalOnly()
    {
        if (!scrollRect || !content) return;
        var rt = content as RectTransform;
        if (rt && Mathf.Abs(rt.anchoredPosition.x) > 0.01f)
            rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
        if (Mathf.Abs(scrollRect.horizontalNormalizedPosition) > 0.0001f)
            scrollRect.horizontalNormalizedPosition = 0f;
        var v = scrollRect.velocity;
        if (Mathf.Abs(v.x) > 0.01f) { v.x = 0f; scrollRect.velocity = v; }
    }

    void NormalizeAndSortLibrary()
    {
        library.Sort((a, b) =>
        {
            // 0) kartu aktif dulu (opsional)
            if (activeFirstGlobally)
            {
                bool aAct = _activeIds.Contains(a.id);
                bool bAct = _activeIds.Contains(b.id);
                int actCmp = bAct.CompareTo(aAct); // true > false
                if (actCmp != 0) return actCmp;
            }

            // 1) owned-first atau rarity-first (sesuai toggle)
            if (ownedFirstGlobally)
            {
                int o = b.owned.CompareTo(a.owned); if (o != 0) return o;
                int r = a.rarity.CompareTo(b.rarity); if (r != 0) return r;
            }
            else
            {
                int r = a.rarity.CompareTo(b.rarity); if (r != 0) return r;
                int o = b.owned.CompareTo(a.owned); if (o != 0) return o;
            }

            // 2) id sebagai penentu terakhir (stabil)
            return string.Compare(a.id, b.id, StringComparison.Ordinal);
        });
    }

    void BuildGrid()
    {
        foreach (var go in spawned) if (go) Destroy(go);
        spawned.Clear();

        foreach (var entry in library)
        {
            if (entry.art == null) continue;
            var go = Instantiate(cardTilePrefab, content);
            spawned.Add(go);

            var img = go.GetComponent<Image>();
            var btn = go.GetComponent<Button>();

            bool isActiveNow = _activeIds.Contains(entry.id);

            // Anggap "aktif" = owned secara visual agar selalu mencolok
            bool isOwnedOrActive = entry.owned || isActiveNow;

            if (img)
            {
                img.sprite = entry.art;
                img.preserveAspect = true;

                if (!isOwnedOrActive)
                {
                    // benar2 tidak dimiliki & tidak aktif → faded + lock
                    img.color = new Color(1f, 1f, 1f, 0.4f);
                    var lockOv = go.transform.Find("LockedOverlay");
                    if (lockOv) lockOv.gameObject.SetActive(true);
                    if (btn) btn.interactable = false;
                }
                else
                {
                    // owned ATAU aktif → jelas; jika aktif, pakai tint aktif
                    img.color = isActiveNow ? activeTint : Color.white;

                    // kalau kamu tetap mau munculkan gembok pada owned=false tapi aktif=true, hapus 2 baris di bawah
                    var lockOv = go.transform.Find("LockedOverlay");
                    if (lockOv) lockOv.gameObject.SetActive(false);
                }
            }

            if (isActiveNow)
            {
                var activeOv = go.transform.Find("ActiveOverlay");
                if (activeOv) activeOv.gameObject.SetActive(true);

                var badge = go.transform.Find("UsingBadge");
                if (badge) badge.gameObject.SetActive(true);

                go.transform.localScale = Vector3.one * activeScale;
            }

            if (btn) btn.onClick.AddListener(() =>
            {
                ShowDetail(entry.art);
                AudioManager.I?.PlayUI(sfxClickKey, ignorePause: true);
            });
        }

        ResizeContentForGrid();
    }

    void ResizeContentForGrid()
    {
        var rt = content as RectTransform;
        var grid = content.GetComponent<GridLayoutGroup>();
        if (!rt || !grid || !scrollRect) return;

        int visibleCount = 0; foreach (var e in library) if (e.art != null) visibleCount++;
        int cols = Mathf.Max(1, grid.constraintCount);
        int rows = Mathf.CeilToInt(visibleCount / (float)cols);

        float h = grid.padding.top + rows * grid.cellSize.y + Mathf.Max(0, rows - 1) * grid.spacing.y + grid.padding.bottom;
        var vpH = ((RectTransform)scrollRect.viewport).rect.height;
        if (h < vpH) h = vpH + 1f;

        rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
        rt.anchoredPosition = new Vector2(0f, 0f);
    }

    void ShowDetail(Sprite s)
    {
        if (!detailImage) return;
        detailImage.sprite = s;
        detailImage.preserveAspect = true;
        detailImage.enabled = s != null;
    }
}
