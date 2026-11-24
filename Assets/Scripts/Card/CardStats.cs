using UnityEngine;

/// <summary>
/// Stat helpers for the single-card system (design doc section 3) and rarity presentation (names, colours).
/// A player card's stat = (Base 100 + Roll) x (1 + 0.05 x tier); HP is shown x10 like Anigame.
/// Floor enemies use the sheet's stats (already scaled by the POWER column).
/// </summary>
public static class CardStats
{
    /// <summary>HP = HP stat x GameRules.HpMultiplier (10 by default, adjustable in GameBalance / Test Mode).</summary>
    public static int HpOf(CardRarity rarity, BuildData b, int godBonus = 0) => GameRules.Stat(b.hp, GameRules.Tier(rarity), godBonus) * GameRules.HpMultiplier;
    public static int AtkOf(CardRarity rarity, BuildData b, int godBonus = 0) => GameRules.Stat(b.atk, GameRules.Tier(rarity), godBonus);
    public static int DefOf(CardRarity rarity, BuildData b, int godBonus = 0) => GameRules.Stat(b.def, GameRules.Tier(rarity), godBonus);
    public static int SpdOf(CardRarity rarity, BuildData b, int godBonus = 0) => GameRules.Stat(b.spd, GameRules.Tier(rarity), godBonus);

    /// <summary>Power = HP/10 + ATK + DEF + SPD (same scale as the sheet's POWER column).</summary>
    public static int Power(CardRarity rarity, BuildData b, int godBonus = 0)
    {
        if (b == null) return 0;
        return GameRules.Stat(b.hp, GameRules.Tier(rarity), godBonus) + AtkOf(rarity, b, godBonus) + DefOf(rarity, b, godBonus) + SpdOf(rarity, b, godBonus);
    }

    public static int Power(CardInstance c) => c == null ? 0 : Power(c.rarity, c.build, c.godBonus);
    public static int Power(CardInstance c, BuildData b) => c == null ? 0 : Power(c.rarity, b ?? c.build, c.godBonus);

    /// <summary>Power of a floor enemy (sheet stats).</summary>
    public static int EnemyPower(Card card) => card == null ? 0 : Mathf.RoundToInt(card.StatTotal * GameRules.EnemyStatScale);

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

    /// <summary>Second colour used for frame gradients.</summary>
    public static Color RarityColorDark(CardRarity rarity) => Color.Lerp(RarityColor(rarity), Color.black, 0.45f);
}
