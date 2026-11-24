using System;
using System.Collections.Generic;
using UnityEngine;

public enum PackCurrency { Gold, Diamonds }

public enum RewardType { Card, Gold, FusionOrb, ElementOrb, TalentOrb, Diamonds, GodOrb }

/// <summary>One line of a pack table: what, how many, and the chance (percent) for each of slots 2-5.</summary>
[Serializable]
public class PackEntry
{
    public RewardType type;
    public CardRarity rarity;
    public int amount;
    public float chance;

    public PackEntry(RewardType type, float chance, int amount = 1, CardRarity rarity = CardRarity.Common)
    {
        this.type = type;
        this.chance = chance;
        this.amount = amount;
        this.rarity = rarity;
    }
}

public class PackDefinition
{
    public string id;
    public string name;
    public PackCurrency currency;
    public int price;
    public CardRarity guaranteed;
    public PackEntry[] entries;
    public const int ItemCount = 5;

    public float TotalChance
    {
        get
        {
            float t = 0f;
            foreach (var e in entries) t += e.chance;
            return t;
        }
    }

    /// <summary>Expected diamonds per pack = 4 slots x chance x amount (doc 7.1).</summary>
    public float ExpectedDiamonds
    {
        get
        {
            float d = 0f;
            foreach (var e in entries) if (e.type == RewardType.Diamonds) d += (ItemCount - 1) * e.chance / 100f * e.amount;
            return d;
        }
    }
}

/// <summary>A rolled item from a pack.</summary>
public class PackItem
{
    public RewardType type;
    public int amount;
    public CardRarity rarity;
    public CardInstance card; // set when type == Card (already added to the collection)
    public bool exchanged;    // auto exchange turned the card into a material (card kept for the reveal only)
}

/// <summary>Gacha tables from the design doc, section 7. Slot 1 is always a card of the pack's rarity; slots 2-5 roll these tables. No pity.</summary>
public static class Packs
{
    public static readonly PackDefinition[] All =
    {
        // 7.1 gold packs (the only diamond source)
        new PackDefinition
        {
            id = "common", name = "Common Pack", currency = PackCurrency.Gold, price = 1000, guaranteed = CardRarity.Common,
            entries = new[]
            {
                new PackEntry(RewardType.Card, 50f, 1, CardRarity.Common),
                new PackEntry(RewardType.Card, 10f, 1, CardRarity.Uncommon),
                new PackEntry(RewardType.Gold, 20f, 300),
                new PackEntry(RewardType.FusionOrb, 10f, 1),
                new PackEntry(RewardType.ElementOrb, 3f, 1),
                new PackEntry(RewardType.TalentOrb, 3f, 1),
                new PackEntry(RewardType.Diamonds, 4f, 5),
            }
        },
        new PackDefinition
        {
            id = "rare", name = "Rare Pack", currency = PackCurrency.Gold, price = 3000, guaranteed = CardRarity.Rare,
            entries = new[]
            {
                new PackEntry(RewardType.Card, 35f, 1, CardRarity.Uncommon),
                new PackEntry(RewardType.Card, 12f, 1, CardRarity.Rare),
                new PackEntry(RewardType.Card, 1f, 1, CardRarity.SuperRare),
                new PackEntry(RewardType.Gold, 18f, 1000),
                new PackEntry(RewardType.FusionOrb, 15f, 2),
                new PackEntry(RewardType.ElementOrb, 6f, 1),
                new PackEntry(RewardType.TalentOrb, 5f, 1),
                new PackEntry(RewardType.Diamonds, 8f, 15),
            }
        },
        new PackDefinition
        {
            id = "superrare", name = "Super Rare Pack", currency = PackCurrency.Gold, price = 10000, guaranteed = CardRarity.SuperRare,
            entries = new[]
            {
                new PackEntry(RewardType.Card, 30f, 1, CardRarity.Rare),
                new PackEntry(RewardType.Card, 10f, 1, CardRarity.SuperRare),
                new PackEntry(RewardType.Card, 1f, 1, CardRarity.UltraRare),
                new PackEntry(RewardType.Gold, 13f, 3000),
                new PackEntry(RewardType.FusionOrb, 18f, 3),
                new PackEntry(RewardType.ElementOrb, 8f, 1),
                new PackEntry(RewardType.TalentOrb, 8f, 1),
                new PackEntry(RewardType.Diamonds, 12f, 50),
            }
        },
        // 7.2 diamond packs (never drop diamonds)
        new PackDefinition
        {
            id = "ultrarare", name = "Ultra Rare Pack", currency = PackCurrency.Diamonds, price = 100, guaranteed = CardRarity.UltraRare,
            entries = new[]
            {
                new PackEntry(RewardType.Card, 30f, 1, CardRarity.SuperRare),
                new PackEntry(RewardType.Card, 8f, 1, CardRarity.UltraRare),
                new PackEntry(RewardType.Card, 1f, 1, CardRarity.Epic),
                new PackEntry(RewardType.FusionOrb, 20f, 5),
                new PackEntry(RewardType.ElementOrb, 15f, 2),
                new PackEntry(RewardType.TalentOrb, 16f, 2),
                new PackEntry(RewardType.Gold, 10f, 10000),
            }
        },
        new PackDefinition
        {
            id = "epic", name = "Epic Pack", currency = PackCurrency.Diamonds, price = 500, guaranteed = CardRarity.Epic,
            entries = new[]
            {
                new PackEntry(RewardType.Card, 30f, 1, CardRarity.UltraRare),
                new PackEntry(RewardType.Card, 6f, 1, CardRarity.Epic),
                new PackEntry(RewardType.Card, 0.5f, 1, CardRarity.Legendary),
                new PackEntry(RewardType.FusionOrb, 22f, 10),
                new PackEntry(RewardType.ElementOrb, 15f, 3),
                new PackEntry(RewardType.TalentOrb, 16.5f, 3),
                new PackEntry(RewardType.Gold, 10f, 30000),
            }
        },
        new PackDefinition
        {
            id = "legendary", name = "Legendary Pack", currency = PackCurrency.Diamonds, price = 1000, guaranteed = CardRarity.Legendary,
            entries = new[]
            {
                new PackEntry(RewardType.Card, 30f, 1, CardRarity.Epic),
                new PackEntry(RewardType.Card, 4f, 1, CardRarity.Legendary),
                new PackEntry(RewardType.Card, 0.3f, 1, CardRarity.Mythic),
                new PackEntry(RewardType.FusionOrb, 25f, 20),
                new PackEntry(RewardType.ElementOrb, 15f, 5),
                new PackEntry(RewardType.TalentOrb, 15.7f, 5),
                new PackEntry(RewardType.Gold, 10f, 80000),
            }
        },
    };

    public static PackDefinition Find(string id)
    {
        foreach (var p in All) if (p.id == id) return p;
        return null;
    }

    /// <summary>Rolls one entry of slots 2-5 (chances are percent and sum to 100).</summary>
    public static PackEntry RollEntry(PackDefinition pack)
    {
        float total = pack.TotalChance;
        float roll = UnityEngine.Random.value * total;
        float acc = 0f;
        foreach (var e in pack.entries)
        {
            acc += e.chance;
            if (roll < acc) return e;
        }
        return pack.entries[pack.entries.Length - 1];
    }
}
