using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What a prize gives (bosses, minigames).</summary>
public enum PrizeType { Gold, Diamonds, FusionOrbs, ElementOrbs, TalentOrbs, GodOrbs, Stamina, Material, Pack } // new types go at the end (saved as numbers)

/// <summary>One reward: a type and an amount (materials also have a rarity tier). Edited in Test Mode, saved as JSON.</summary>
[Serializable]
public class Prize
{
    public PrizeType type = PrizeType.Gold;
    public int amount = 100;
    [Tooltip("Material rarity tier when type is Material (0 = Common ... 8 = God).")]
    public int materialTier = 8;

    public Prize() { }
    public Prize(PrizeType type, int amount, int materialTier = 8) { this.type = type; this.amount = amount; this.materialTier = materialTier; }

    [Tooltip("Card pack when type is Pack (common, rare, superrare, ultrarare): opened for free at once.")]
    public string packId = "common";

    public static Prize Pack(string packId, int amount = 1) => new Prize(PrizeType.Pack, amount) { packId = packId };

    public Prize Clone() => new Prize(type, amount, materialTier) { packId = packId };

    /// <summary>Packs a prize can be: up to the Ultra Rare Pack.</summary>
    public static PackDefinition[] PackChoices => System.Array.FindAll(Packs.All, p => p.guaranteed <= CardRarity.UltraRare);

    public static string PackName(string id)
    {
        var p = Packs.Find(id);
        return p != null ? p.name : "Card Pack";
    }

    public static string TypeName(PrizeType t, int materialTier = 8)
    {
        switch (t)
        {
            case PrizeType.Gold: return "Gold";
            case PrizeType.Diamonds: return "Diamonds";
            case PrizeType.FusionOrbs: return "Fusion Orbs";
            case PrizeType.ElementOrbs: return "Element Orbs";
            case PrizeType.TalentOrbs: return "Talent Orbs";
            case PrizeType.GodOrbs: return "God Orbs";
            case PrizeType.Stamina: return "Stamina";
            case PrizeType.Material: return PlayerProfile.MaterialName(materialTier) + "s";
            case PrizeType.Pack: return "Card Packs";
        }
        return t.ToString();
    }

    public string Label
    {
        get
        {
            if (amount <= 0) return "nothing";
            string name = type == PrizeType.Pack ? PackName(packId) + "s" : TypeName(type, materialTier);
            if (amount == 1 && name.EndsWith("s")) name = name.Substring(0, name.Length - 1); // 1 God Orb, 1 God Material
            return $"{amount:N0} {name}";
        }
    }
}

public static partial class PlayerProfile
{
    /// <summary>What the last pack prize gave (cards, gold...), for the result screens.</summary>
    public static readonly List<PackItem> LastPackItems = new List<PackItem>();

    /// <summary>"Rare card, Common card, 300 Gold..." for pack contents.</summary>
    public static string DescribePackItems(IEnumerable<PackItem> items)
    {
        var parts = new List<string>();
        foreach (var it in items)
        {
            if (it == null) continue;
            string s = it.amount == 1 ? "" : "s";
            switch (it.type)
            {
                case RewardType.Card: parts.Add($"{CardStats.RarityName(it.rarity)} card{(it.exchanged ? " (exchanged)" : "")}"); break;
                case RewardType.Gold: parts.Add($"{it.amount:N0} Gold"); break;
                case RewardType.Diamonds: parts.Add($"{it.amount} Diamonds"); break;
                case RewardType.FusionOrb: parts.Add($"{it.amount} Fusion Orb{s}"); break;
                case RewardType.ElementOrb: parts.Add($"{it.amount} Element Orb{s}"); break;
                case RewardType.TalentOrb: parts.Add($"{it.amount} Talent Orb{s}"); break;
                case RewardType.GodOrb: parts.Add($"{it.amount} God Orb{s}"); break;
            }
        }
        return string.Join(", ", parts);
    }

    /// <summary>Gives a prize (share &lt; 1 = part of it; a leftover fraction is a chance for one more). Returns what was given.</summary>
    public static Prize GivePrize(Prize p, float share = 1f, bool save = true)
    {
        if (p == null || p.amount <= 0 || share <= 0f) return new Prize(p != null ? p.type : PrizeType.Gold, 0, p != null ? p.materialTier : 8);
        float exact = p.amount * Mathf.Min(1f, share);
        int n = Mathf.FloorToInt(exact);
        if (UnityEngine.Random.value < exact - n) n++;
        var given = new Prize(p.type, n, p.materialTier) { packId = p.packId };
        if (p.type == PrizeType.Pack) LastPackItems.Clear();
        if (n <= 0) return given;
        switch (p.type)
        {
            case PrizeType.Gold: given.amount = EarnGold(n); break;
            case PrizeType.Diamonds: Data.diamonds += n; break;
            case PrizeType.FusionOrbs: AddOrbs(OrbType.Fusion, n); break;
            case PrizeType.ElementOrbs: AddOrbs(OrbType.Element, n); break;
            case PrizeType.TalentOrbs: AddOrbs(OrbType.Talent, n); break;
            case PrizeType.GodOrbs: AddOrbs(OrbType.God, n); break;
            case PrizeType.Stamina: given.amount = AddStamina(n); break;
            case PrizeType.Pack:
            {
                var pack = Packs.Find(p.packId);
                if (pack == null || pack.guaranteed > CardRarity.UltraRare) { given.amount = 0; break; }
                for (int i = 0; i < n; i++) LastPackItems.AddRange(OpenPackFree(pack));
                break;
            }
            case PrizeType.Material:
                if (Data.materials == null || Data.materials.Length < 9) Array.Resize(ref Data.materials, 9);
                Data.materials[Mathf.Clamp(p.materialTier, 0, Data.materials.Length - 1)] += n;
                break;
        }
        if (save) Save();
        return given;
    }
}
