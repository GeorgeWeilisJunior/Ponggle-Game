using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CardLibrary
{
    // Cache per rarity
    static bool cached;
    static List<CardData> commons, rares, epics, legendaries;

    // Panggil di awal (atau akan auto dipanggil saat draw)
    public static void EnsureCache()
    {
        if (cached) return;
        var all = Resources.LoadAll<CardData>("Cards");  // → letakkan asset di Resources/Cards
        commons = all.Where(c => c.rarity == CardRarity.Common).ToList();
        rares = all.Where(c => c.rarity == CardRarity.Rare).ToList();
        epics = all.Where(c => c.rarity == CardRarity.Epic).ToList();
        legendaries = all.Where(c => c.rarity == CardRarity.Legendary).ToList();
        cached = true;
#if UNITY_EDITOR
        Debug.Log($"[CardLibrary] Loaded Cards: C{commons.Count} R{rares.Count} E{epics.Count} L{legendaries.Count}");
#endif
    }

    public static CardData GetById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        EnsureCache();
        // gabungkan semua pool lalu cari yang id-nya cocok
        foreach (var c in commons) if (c && c.id == id) return c;
        foreach (var c in rares) if (c && c.id == id) return c;
        foreach (var c in epics) if (c && c.id == id) return c;
        foreach (var c in legendaries) if (c && c.id == id) return c;
        return null;
    }

    /// <summary>
    /// Ambil satu kartu random memakai distribusi rarity.
    /// useStage5Rates = true ⇒ 30/35/20/15 (sesuai proposal stage 5).
    /// </summary>
    public static CardData DrawRandomCard(bool useStage5Rates = false)
    {
        EnsureCache();

        // Bobot rarity default: 50/35/10/5  (Stage 1–4)
        int wC = useStage5Rates ? 30 : 50;
        int wR = useStage5Rates ? 35 : 35;
        int wE = useStage5Rates ? 20 : 10;
        int wL = useStage5Rates ? 15 : 5;

        var rarity = WeightedPick(new (CardRarity rarity, int weight)[] {
            (CardRarity.Common, wC),
            (CardRarity.Rare, wR),
            (CardRarity.Epic, wE),
            (CardRarity.Legendary, wL),
        });

        // Pilih satu kartu random dari kumpulannya
        List<CardData> pool = rarity switch
        {
            CardRarity.Common => commons,
            CardRarity.Rare => rares,
            CardRarity.Epic => epics,
            CardRarity.Legendary => legendaries,
            _ => commons
        };

        if (pool.Count == 0)
        {
            Debug.LogWarning($"[CardLibrary] Pool kosong untuk rarity {rarity}. Cek asset di Resources/Cards.");
            // fallback: cari rarity lain yang tidak kosong
            var fallback = new[] { commons, rares, epics, legendaries }.FirstOrDefault(l => l.Count > 0);
            return fallback != null && fallback.Count > 0 ? fallback[Random.Range(0, fallback.Count)] : null;
        }

        return pool[Random.Range(0, pool.Count)];
    }

    static CardRarity WeightedPick((CardRarity rarity, int weight)[] entries)
    {
        int total = entries.Sum(e => Mathf.Max(0, e.weight));
        int r = Random.Range(0, total);
        int acc = 0;
        foreach (var e in entries)
        {
            acc += Mathf.Max(0, e.weight);
            if (r < acc) return e.rarity;
        }
        return entries[0].rarity;
    }
}
