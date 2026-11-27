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

    // ---- HP multiplier and battle length (Assets/Resources/GameBalance.asset; Test Mode can override it)
    public const string HpOverrideKey = "AniGame.HpMultiplierOverride";

    /// <summary>HP = HP stat x this (default 10).</summary>
    public static int HpMultiplier
    {
        get
        {
            int o = PlayerPrefs.GetInt(HpOverrideKey, 0);
            return o > 0 ? o : Mathf.Max(1, GameBalance.Current.hpMultiplier);
        }
    }

    /// <summary>Test Mode: overrides the HP multiplier on this PC (0 = use GameBalance.asset).</summary>
    public static void SetHpMultiplierOverride(int value)
    {
        if (value <= 0) PlayerPrefs.DeleteKey(HpOverrideKey);
        else PlayerPrefs.SetInt(HpOverrideKey, Mathf.Clamp(value, 1, 200));
        PlayerPrefs.Save();
    }

    public static bool HpMultiplierOverridden => PlayerPrefs.GetInt(HpOverrideKey, 0) > 0;

    static float RoundScale => GameBalance.Current.scaleRoundsWithHp ? Mathf.Max(1f, HpMultiplier / 10f) : 1f;
    public static int MaxRounds => Mathf.Max(10, Mathf.RoundToInt(GameBalance.Current.maxRounds * RoundScale));
    public static int EnrageStartRound => Mathf.Max(1, Mathf.RoundToInt(GameBalance.Current.enrageStartRound * RoundScale));
    public static int EnragePercent => Mathf.Max(0, GameBalance.Current.enragePercent);

    // ---- Healing fatigue: after round N all healing received drops by X% per round
    public static int HealingFatigueAfterRound => Mathf.Max(0, GameBalance.Current.healingFatigueAfterRound);
    public static int HealingFatiguePerRound => Mathf.Max(0, GameBalance.Current.healingFatiguePerRound);
    public static bool HealingFatigueOn => HealingFatigueAfterRound > 0 && HealingFatiguePerRound > 0;

    /// <summary>Part of every heal a card still receives in this round (1 = all, 0 = none).</summary>
    public static float HealingMultiplier(int round)
    {
        if (!HealingFatigueOn || round <= HealingFatigueAfterRound) return 1f;
        return Mathf.Clamp01(1f - (round - HealingFatigueAfterRound) * HealingFatiguePerRound / 100f);
    }

    // ---- Hidden Location (3v3): 20 LOCs x 3 floors, unlocked by owning 3 cards of Mythic or higher
    public const int HiddenLocationCount = 20;
    public const int HiddenFloorsPerLocation = 3;
    public static int HiddenUnlockCards => Mathf.Max(1, GameBalance.Current.hiddenUnlockCards);
    public const CardRarity HiddenUnlockRarity = CardRarity.Mythic;

    /// <summary>Highest stat a player card of this tier can reach: max Roll, and every God Orb on a God card.</summary>
    public static int MaxStat(int tier) => Stat(MaxDeviation(tier), tier, tier >= MaxTier ? GodOrbMaxStacks : 0);

    /// <summary>Hidden Location enemy stat: the highest stat of its rarity x (100 + LOC)%, rounded.</summary>
    public static int HiddenEnemyStat(CardRarity rarity, int location) =>
        Mathf.RoundToInt(MaxStat(Tier(rarity)) * (100f + Mathf.Max(0, location)) / 100f);

    /// <summary>Gold for a Hidden Location win (first clear of that floor, or a replay).</summary>
    public static int HiddenGold(int location, int floor, bool firstClear)
    {
        var b = GameBalance.Current;
        int gold = b.hiddenGoldPerLoc * location + b.hiddenGoldPerFloor * floor;
        return firstClear ? gold : Mathf.Max(1, gold * b.hiddenReplayGoldPercent / 100);
    }

    /// <summary>Diamonds for the first clear of a Hidden Location floor (replays give none).</summary>
    public static int HiddenDiamonds(int location, int floor) => GameBalance.Current.hiddenDiamondsPerLoc * location + GameBalance.Current.hiddenDiamondsPerFloor * floor;

    /// <summary>3v3 simulator override for <see cref="TeamAttackScale"/> (0 or less = use GameBalance.asset).</summary>
    public static float TeamAttackScaleOverride = -1f;
    /// <summary>3v3: normal attack damage multiplier (team ATK/DEF are 3-card totals).</summary>
    public static float TeamAttackScale => TeamAttackScaleOverride > 0f ? TeamAttackScaleOverride : Mathf.Clamp(GameBalance.Current.teamAttackScale, 0.05f, 1f);

    // ---- Evasion: diminishing returns
    // The card's evasion stat is a RATING with no upper limit. The real chance to evade a normal attack is
    // cap x (1 - e^(-rating / cap)): almost 1:1 at low ratings, then flattening toward the cap (never reached).
    // 10 -> 9.4%, 20 -> 17.6%, 50 -> 36.5%, 100 -> 55%, 200 -> 70%, 300 -> 73.6%, 1000 -> 75%.
    public const float EvasionCap = 75f;

    /// <summary>Real evade chance in percent (0..75) for an evasion rating.</summary>
    public static float EvadeChance(int rating) => rating <= 0 ? 0f : EvasionCap * (1f - Mathf.Exp(-rating / EvasionCap));

    /// <summary>"50 (36.5%)" for tooltips and stat lines.</summary>
    public static string EvasionLabel(int rating) => $"{rating} ({EvadeChance(rating):0.#}%)";
}
