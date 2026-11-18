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

    /// <summary>Stat = (Base + Roll) x (1 + 0.05 x tier) x (1 + 1% per God Orb success).</summary>
    public static int Stat(int roll, int tier, int godBonus = 0) =>
        Mathf.Max(1, Mathf.RoundToInt((BaseStat + roll) * RarityMultiplier(tier) * (1f + GodOrbStatBonus * Mathf.Clamp(godBonus, 0, GodOrbMaxStacks))));

    // ---------------------------------------------------------------- 6. talents
    /// <summary>Talent strength grows with rarity, up to +20% at God (user decision). Every talent is open at every rarity.</summary>
    public static float TalentPower(int tier) => 1f + 0.20f * Mathf.Clamp(tier, 0, MaxTier) / MaxTier;

    // ---------------------------------------------------------------- 4. fusion (index = current tier of the main card)
    static readonly float[] fusionRate = { 1.00f, 0.90f, 0.75f, 0.60f, 0.55f, 0.40f, 0.25f, 0.15f };
    static readonly int[] fusionOrbs = { 1, 1, 2, 3, 5, 8, 12, 20 };
    public const int FusionFodderCount = 2;

    public static bool CanFuseFrom(int tier) => tier >= 0 && tier < MaxTier;
    public static float FusionBaseRate(int tier) => CanFuseFrom(tier) ? fusionRate[tier] : 0f;
    public static int FusionOrbCost(int tier) => CanFuseFrom(tier) ? fusionOrbs[tier] : 0;
    /// <summary>Base rate + bonus (Fusion Fortune buff), capped at 100%. Extra orbs no longer raise the rate (user change).</summary>
    public static float FusionSuccessRate(int tier, float bonus = 0f) => Mathf.Min(1f, FusionBaseRate(tier) + bonus);

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

    // ---------------------------------------------------------------- stamina (user rules, 15 Nov 2026)
    public const int BaseMaxStamina = 80;          // +20 with Potion of Stamina
    public const int StaminaCost = 5;              // per fight
    public const int StaminaRegenSeconds = 300;    // +1 every 5 minutes (+2 with Stamina Master)
    public const int StaminaNewFloor = 2;          // first clear of a floor
    // Unlocking a new LOC refills stamina to max.

    // ---------------------------------------------------------------- dismantle (Salvage Expert buff): half the stat reroll cost
    public static int DismantleGold(int tier) => StatRerollCost(tier) / 2;

    // ---------------------------------------------------------------- God Orb (user idea): God cards only, may fail, not sold anywhere
    public const float GodOrbSuccess = 0.5f;       // chance a God Orb works
    public const float GodOrbStatBonus = 0.01f;    // +1% to every stat per success
    public const int GodOrbMaxStacks = 20;         // max +20%
    public const float GodOrbEnemyDrop = 0.10f;    // God-rarity floor enemies
    /// <summary>Chance a diamond pack adds a bonus God Orb (on top of its 5 items).</summary>
    public static float GodOrbPackChance(string packId) => packId == "ultrarare" ? 0.01f : packId == "epic" ? 0.03f : packId == "legendary" ? 0.06f : 0f;

    // ---------------------------------------------------------------- not in the doc (project choices)
    public const int StartGold = 3000;
    public const int StartFusionOrbs = 2;
    /// <summary>Floor enemies use the sheet's stats as-is: the POWER column already scales them (user decision).</summary>
    public const float EnemyStatScale = 1f;
}
