using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

public class CardInventory : MonoBehaviour
{
    public static CardInventory I { get; private set; }

    [System.Serializable]
    public class OwnedCard
    {
        public CardData data;
        public int stacks = 1;
    }

    [Header("Owned Cards (run only)")]
    [SerializeField] List<OwnedCard> owned = new();

    [Header("Picked Build (dibawa ke level berikutnya)")]
    [SerializeField, Min(1)] int energyLimit = 10;
    [SerializeField] List<CardData> picked = new();

    public event Action OnInventoryChanged;
    void MarkInventoryChanged() => OnInventoryChanged?.Invoke();

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    // ==== NEW: sync dari Save ====
    public void LoadFromSave()
    {
        owned.Clear();
        picked.Clear();

        if (SaveManager.I == null) return;

        energyLimit = Mathf.Max(1, SaveManager.I.Data.energyLimit);

        // owned (tetap boleh banyak)
        if (SaveManager.I.Data.ownedCards != null)
            foreach (var id in SaveManager.I.Data.ownedCards)
            { var cd = CardLibrary.GetById(id); if (cd) AddCard(cd); }

        // pickedForNext → jadikan unik & patuhi energyLimit
        if (SaveManager.I.Data.pickedForNext != null)
        {
            foreach (var id in SaveManager.I.Data.pickedForNext)
            {
                var cd = CardLibrary.GetById(id);
                if (!cd) continue;
                if (picked.Contains(cd)) continue;                 // unik
                if (EnergyUsed + Mathf.Max(0, cd.energyCost) > energyLimit) continue; // jaga limit
                picked.Add(cd);
            }
        }
        MarkInventoryChanged();
    }


    // ── Readonly views ───────────────────────────────────────────
    public IReadOnlyList<OwnedCard> Owned => owned;
    public IReadOnlyList<CardData> Picked => picked;
    public int EnergyLimit => energyLimit;
    public int EnergyUsed => picked.Sum(c => Mathf.Max(0, c ? c.energyCost : 0));
    public void SetEnergyLimit(int v) { energyLimit = Mathf.Max(1, v); }

    // ── Owned ops ────────────────────────────────────────────────
    public void ClearAll()
    {
        owned.Clear();
        picked.Clear();
        MarkInventoryChanged();
    }

    public void AddCard(CardData card)
    {
        if (!card) return;

        if (card.stackable)
        {
            var found = owned.Find(o => o.data == card);
            if (found != null)
            {
                found.stacks = Mathf.Min(found.stacks + 1, card.maxStacks);
                MarkInventoryChanged();
                return;
            }
        }
        owned.Add(new OwnedCard { data = card, stacks = 1 });
        MarkInventoryChanged();
    }

    public bool RemoveCard(CardData card)
    {
        int idx = owned.FindIndex(o => o.data == card);
        if (idx < 0) return false;

        if (owned[idx].data.stackable && owned[idx].stacks > 1)
            owned[idx].stacks--;
        else
            owned.RemoveAt(idx);
        MarkInventoryChanged();
        return true;
    }

    // ── Pick/Unpick ops ──────────────────────────────────────────
    public bool CanPick(CardData card, out string reason)
    {
        reason = null;
        if (!card) { reason = "not_found"; return false; }

        // NEW: hanya boleh 1 per kartu
        if (picked.Contains(card)) { reason = "duplicate"; return false; }

        int newEnergy = EnergyUsed + Mathf.Max(0, card.energyCost);
        if (newEnergy > energyLimit) { reason = "energy"; return false; }

        // tak ada lagi pengecualian stackable – kepemilikan boleh banyak, picked tetap maksimal 1
        return true;
    }

    // ── Konsumsi stok (hangus) untuk id-id yang dipakai ──────────────────────────
    public void ConsumeOwnedByIds(IEnumerable<string> ids)
    {
        if (ids == null) return;

        // Buat counter id → berapa kali harus dikurangi
        var need = new Dictionary<string, int>();
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id)) continue;
            if (!need.ContainsKey(id)) need[id] = 0;
            need[id]++;
        }

        // Kurangi stok sesuai counter
        foreach (var kv in need)
        {
            var cd = CardLibrary.GetById(kv.Key);
            if (!cd) continue;

            int times = kv.Value;
            for (int i = 0; i < times; i++)
            {
                // RemoveCard sudah meng-handle stackable vs non-stackable
                if (!RemoveCard(cd))
                    break; // stok habis, keluar loop kecil
            }
        }

        // Tulis balik ke Save (flatten owned → list id sesuai jumlah stack)
        WriteBackOwnedToSave();
    }

    /// <summary>Flatten daftar owned (dengan stacks) menjadi list id untuk disimpan.</summary>
    private List<string> FlattenOwnedToIds()
    {
        var result = new List<string>();
        foreach (var o in owned)
        {
            if (o == null || !o.data) continue;
            int count = Mathf.Max(1, o.stacks);
            for (int i = 0; i < count; i++)
                result.Add(o.data.id);
        }
        return result;
    }

    /// <summary>Sinkronkan owned saat ini ke SaveManager dan simpan ke disk.</summary>
    private void WriteBackOwnedToSave()
    {
        if (SaveManager.I == null || SaveManager.I.Data == null) return;
        SaveManager.I.Data.ownedCards = FlattenOwnedToIds();
        SaveManager.I.SaveToDisk();
        MarkInventoryChanged();
    }


    public bool TryPick(CardData card, out string reason)
    {
        if (!CanPick(card, out reason)) return false;
        picked.Add(card);
        return true;
    }

    public bool TryUnpick(CardData card)
    {
        return picked.Remove(card);
    }

    // ── Helpers jumlah ───────────────────────────────────────────
    public int GetOwnedCount(CardData c)
    {
        if (!c) return 0;
        return owned.Where(o => o != null && o.data == c)
                    .Sum(o => Mathf.Max(1, o.stacks));
    }

    public int GetPickedCount(CardData c)
    {
        if (!c) return 0;
        return picked.Count(p => p == c);
    }

    public int GetAvailableCount(CardData c) => GetOwnedCount(c) - GetPickedCount(c);
    public int GetTotalEnergySelected() => EnergyUsed;
}
