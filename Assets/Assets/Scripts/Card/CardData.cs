using UnityEngine;

[CreateAssetMenu(fileName = "Card_", menuName = "Ponggle/Card Data", order = 1000)]
public class CardData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;
    [TextArea(2, 4)] public string description;

    [Header("Meta")]
    public CardRarity rarity = CardRarity.Common;
    [Min(0)] public int energyCost = 0;

    [Header("Visual (sprite terpisah)")]
    [Tooltip("Icon/art kecil (dipakai fallback bila tidak ada fullCardSprite).")]
    public Sprite icon;

    [Tooltip("Sprite kartu penuh (sudah termasuk frame & teks).")]
    public Sprite fullCardSprite;

    [Header("Stacking (opsional)")]
    public bool stackable = false;
    [Min(1)] public int maxStacks = 1;

    [Header("Effect Hook")]
    public string effectKey;

    [Header("Full Card View (opsional prefab)")]
    [Tooltip("Prefab UI untuk tampilan penuh. Jika kosong, script bisa pakai default prefab di CardDropPanel.")]
    public GameObject fullCardPrefab;

    public Color RarityColor =>
        rarity == CardRarity.Common ? new Color(0.82f, 0.82f, 0.82f) :
        rarity == CardRarity.Rare ? new Color(0.40f, 0.65f, 1.00f) :
        rarity == CardRarity.Epic ? new Color(0.70f, 0.40f, 1.00f) :
                                          new Color(1.00f, 0.75f, 0.15f);
}
