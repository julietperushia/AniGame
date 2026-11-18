using UnityEngine;

/// <summary>
/// Every number from the design doc "Thiet ke Game The Bai - He thong 1 The Nang Cap" in one place.
/// Section numbers refer to that doc. Change balance here.
/// </summary>
public static class GameRules
{
    public const int MaxTier = 8; // Common = 0 ... God = 8

    // ---------------------------------------------------------------- 3. rarity & stats
    /// <summary>Base of every stat for every card (user decision: one card type, Base 100).</summary>
    public const int BaseStat = 100;

    public static int Tier(CardRarity rarity) => Mathf.Clamp((int)rarity, 0, MaxTier);

    /// <summary>Stat multiplier: additive, 1 + 0.05 x tier (God = +40%, hard cap).</summary>
    public static float RarityMultiplier(int tier) => 1f + 0.05f * Mathf.Clamp(tier, 0, MaxTier);

    /// <summary>Max Roll per stat: Common +-5, +2 per tier, God +-21.</summary>
    public static int MaxDeviation(int tier) => 5 + 2 * Mathf.Clamp(tier, 0, MaxTier);

    static readonly int[] statRerollCost = { 500, 800, 1200, 2000, 3500, 6000, 10000, 16000, 25000 };
    /// <summary>Gold per stat reroll (all 4 stats at once).</summary>
    public static int StatRerollCost(int tier) => statRerollCost[Mathf.Clamp(tier, 0, MaxTier)];

    /// <summary>Stat = (Base + Roll) x (1 + 0.05 x tier).</summary>
    public static int Stat(int roll, int tier) => Mathf.Max(1, Mathf.RoundToInt((BaseStat + roll) * RarityMultiplier(tier)));

    // ---------------------------------------------------------------- 6. talents
    /// <summary>Talent strength grows with rarity, up to +20% at God (user decision). Every talent is open at every rarity.</summary>
    public static float TalentPower(int tier) => 1f + 0.20f * Mathf.Clamp(tier, 0, MaxTier) / MaxTier;

    // ---------------------------------------------------------------- 4. fusion (index = current tier of the main card)
    static readonly float[] fusionRate = { 1.00f, 0.90f, 0.75f, 0.60f, 0.55f, 0.40f, 0.25f, 0.15f };
    static readonly int[] fusionOrbs = { 1, 1, 2, 3, 5, 8, 12, 20 };
    public const int FusionFodderCount = 2;
    public const int MaxExtraFusionOrbs = 3;      // optional: +5% per extra orb, max +15%
    public const float ExtraOrbBonus = 0.05f;

    public static bool CanFuseFrom(int tier) => tier >= 0 && tier < MaxTier;
    public static float FusionBaseRate(int tier) => CanFuseFrom(tier) ? fusionRate[tier] : 0f;
    public static int FusionOrbCost(int tier) => CanFuseFrom(tier) ? fusionOrbs[tier] : 0;
    public static float FusionSuccessRate(int tier, int extraOrbs) =>
        Mathf.Min(1f, FusionBaseRate(tier) + ExtraOrbBonus * Mathf.Clamp(extraOrbs, 0, MaxExtraFusionOrbs));

    // ---------------------------------------------------------------- 8. floor rewards
    /// <summary>Gold per floor win: 20 x LOC + 5 x floor.</summary>
    public static int FloorGold(int location, int floor) => 20 * location + 5 * floor;

    /// <summary>Chance to get the defeated card: Common 12%, -2% per tier, minimum 1% (Legendary, Mythic), God never.</summary>
    public static float CardDropChance(CardRarity rarity)
    {
        int tier = Tier(rarity);
        if (tier >= MaxTier) return 0f;
        return Mathf.Max(0.01f, 0.12f - 0.02f * tier);
    }

    public const float FusionOrbDropChance = 0.05f;

    // ---------------------------------------------------------------- 9. shop (gold)
    public const int FusionOrbPrice = 1000;
    public const int ElementOrbPrice = 3500;
    public const int TalentOrbPrice = 7000;

    // ---------------------------------------------------------------- 10. build slots
    public const int SlotCount = 3;
    static readonly int[] slotCost = { 0, 300, 600 };
    public static int SlotUnlockCost(int slot) => slotCost[Mathf.Clamp(slot, 0, SlotCount - 1)];

    // ---------------------------------------------------------------- not in the doc (project choices)
    public const int StartGold = 3000;
    public const int StartFusionOrbs = 2;
    /// <summary>Floor enemies use the sheet's stats as-is: the POWER column already scales them (user decision).</summary>
    public const float EnemyStatScale = 1f;
}
