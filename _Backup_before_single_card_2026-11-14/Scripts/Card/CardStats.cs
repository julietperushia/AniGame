using UnityEngine;

/// <summary>
/// Card progression math: level / evolution scaling, XP curves, rarity colours.
/// Anigame reference (dbgames.info): a maxed SR card has ~2.9x its base stats and a maxed UR ~3.4x.
/// Level 50 here gives 2.96x, and each evolution adds +6% on top.
///
/// GlobalStatScale: the CSV floor cards use a smaller stat scale than Anigame (a Common totals ~100-190,
/// an Anigame base card ~325). With tiny stats the damage formula's flat 638000 term one-shots everything,
/// so every runtime stat is multiplied by this factor. Card assets keep the original CSV numbers.
/// </summary>
public static class CardStats
{
    public const int MaxLevel = 50;
    public const int MaxEvolution = 3;
    public const float GlobalStatScale = 2f;

    public static float LevelMultiplier(int level) => 1f + 0.04f * (Mathf.Clamp(level, 1, MaxLevel) - 1);
    public static float EvolutionMultiplier(int evolution) => 1f + 0.06f * Mathf.Clamp(evolution, 0, MaxEvolution);
    public static float Multiplier(int level, int evolution) => LevelMultiplier(level) * EvolutionMultiplier(evolution);

    public static int Scale(int baseValue, int level, int evolution)
    {
        return Mathf.Max(1, Mathf.RoundToInt(baseValue * Multiplier(level, evolution) * GlobalStatScale));
    }

    /// <summary>XP needed to go from <paramref name="level"/> to level+1.</summary>
    public static int XpToNext(int level) => 60 + Mathf.Max(1, level) * 30;

    /// <summary>Power rating shown in the UI (stat total scaled by level/evo).</summary>
    public static int Power(Card card, int level, int evolution)
    {
        if (card == null) return 0;
        return Mathf.RoundToInt(card.StatTotal * Multiplier(level, evolution) * GlobalStatScale);
    }

    /// <summary>Level the campaign enemies of a location fight at (LOC 1 = Lv1, LOC 100 = Lv50).</summary>
    public static int EnemyLevelForLocation(int location) => Mathf.Clamp(1 + (location - 1) / 2, 1, MaxLevel);

    /// <summary>XP a card gives when used as enhancement fodder (Anigame: C 100, UC 200, R 300).</summary>
    public static int FodderXp(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common: return 100;
            case CardRarity.Uncommon: return 200;
            case CardRarity.Rare: return 300;
            case CardRarity.SuperRare: return 500;
            case CardRarity.UltraRare: return 800;
            case CardRarity.Epic: return 1200;
            case CardRarity.Legendary: return 1800;
            case CardRarity.Mythic: return 2600;
            default: return 4000;
        }
    }

    /// <summary>Gold cost to evolve (Anigame: SR 5,000 / UR 25,000; others scaled to fit this campaign).</summary>
    public static int EvolveCost(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common: return 300;
            case CardRarity.Uncommon: return 600;
            case CardRarity.Rare: return 1500;
            case CardRarity.SuperRare: return 5000;
            case CardRarity.UltraRare: return 10000;
            case CardRarity.Epic: return 15000;
            case CardRarity.Legendary: return 25000;
            case CardRarity.Mythic: return 40000;
            default: return 60000;
        }
    }

    public static string RarityName(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.SuperRare: return "Super Rare";
            case CardRarity.UltraRare: return "Ultra Rare";
            default: return rarity.ToString();
        }
    }

    public static string RarityShort(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common: return "C";
            case CardRarity.Uncommon: return "UC";
            case CardRarity.Rare: return "R";
            case CardRarity.SuperRare: return "SR";
            case CardRarity.UltraRare: return "UR";
            case CardRarity.Epic: return "EP";
            case CardRarity.Legendary: return "LG";
            case CardRarity.Mythic: return "MY";
            default: return "GOD";
        }
    }

    public static Color RarityColor(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common: return new Color(0.62f, 0.64f, 0.68f);
            case CardRarity.Uncommon: return new Color(0.36f, 0.78f, 0.40f);
            case CardRarity.Rare: return new Color(0.25f, 0.60f, 1.00f);
            case CardRarity.SuperRare: return new Color(0.68f, 0.38f, 1.00f);
            case CardRarity.UltraRare: return new Color(1.00f, 0.60f, 0.15f);
            case CardRarity.Epic: return new Color(1.00f, 0.30f, 0.50f);
            case CardRarity.Legendary: return new Color(1.00f, 0.82f, 0.20f);
            case CardRarity.Mythic: return new Color(0.20f, 0.95f, 0.95f);
            default: return new Color(1.00f, 0.96f, 0.75f);
        }
    }
}
