using System.Collections.Generic;

/// <summary>Library filter groups for talents.</summary>
public enum TalentCategory { Attack, Defense, Healing, StatBoost, Debuff, Control, Special }

/// <summary>Which group every talent belongs to (by class name), plus display names.</summary>
public static class TalentCatalog
{
    static readonly Dictionary<string, TalentCategory> byClass = new Dictionary<string, TalentCategory>
    {
        // Attack
        { "BalancingStrike", TalentCategory.Attack }, { "BerserkerStrike", TalentCategory.Attack }, { "DexterityDrive", TalentCategory.Attack },
        { "DoubleEdgedStrike", TalentCategory.Attack }, { "ElementalStrike", TalentCategory.Attack }, { "VengeanceATK", TalentCategory.Attack },
        { "VengeanceDEF", TalentCategory.Attack }, { "Flurry", TalentCategory.Attack }, { "Assassinate", TalentCategory.Attack },
        { "FinalGambit", TalentCategory.Attack }, { "ChargeStrike", TalentCategory.Attack }, { "ChargeAttack", TalentCategory.Attack },
        { "Slayer", TalentCategory.Attack }, { "Infernus", TalentCategory.Attack }, { "Momentum", TalentCategory.Attack },
        { "Haste", TalentCategory.Attack }, { "PiercingEdge", TalentCategory.Attack }, { "LethalClarity", TalentCategory.Attack },
        // Defense
        { "Endurance", TalentCategory.Defense }, { "Evasion", TalentCategory.Defense }, { "Protector", TalentCategory.Defense },
        { "Reversion", TalentCategory.Defense }, { "BurstBarrier", TalentCategory.Defense }, { "CounterStance", TalentCategory.Defense },
        { "Guts", TalentCategory.Defense }, { "Thorns", TalentCategory.Defense }, { "Gigantify", TalentCategory.Defense },
        { "DeathsDance", TalentCategory.Defense },
        // Healing & drain
        { "Regeneration", TalentCategory.Healing }, { "Bloodthirster", TalentCategory.Healing }, { "BloodSurge", TalentCategory.Healing },
        { "LifeSap", TalentCategory.Healing }, { "Leech", TalentCategory.Healing }, { "Greedy", TalentCategory.Healing },
        { "Cursed", TalentCategory.Healing },
        // Stat boost
        { "AmplifierATK", TalentCategory.StatBoost }, { "AmplifierDEF", TalentCategory.StatBoost }, { "Berserker", TalentCategory.StatBoost },
        { "Dominance", TalentCategory.StatBoost }, { "DivineBlessing", TalentCategory.StatBoost }, { "OverloadATK", TalentCategory.StatBoost },
        { "OverloadDEF", TalentCategory.StatBoost }, { "Precision", TalentCategory.StatBoost }, { "LuckyCoin", TalentCategory.StatBoost },
        { "TrickRoomATK", TalentCategory.StatBoost }, { "TrickRoomDEF", TalentCategory.StatBoost }, { "TrueSight", TalentCategory.StatBoost },
        { "Doppelganger", TalentCategory.StatBoost },
        // Debuff
        { "Blaze", TalentCategory.Debuff }, { "Poison", TalentCategory.Debuff }, { "Paralysis", TalentCategory.Debuff },
        { "BreakerATK", TalentCategory.Debuff }, { "BreakerDEF", TalentCategory.Debuff }, { "GrievousLimiter", TalentCategory.Debuff },
        { "UnluckyCoin", TalentCategory.Debuff }, { "Frostbite", TalentCategory.Debuff }, { "Blind", TalentCategory.Debuff },
        { "Sleep", TalentCategory.Debuff }, { "HexTalent", TalentCategory.Debuff }, { "Hypnotize", TalentCategory.Debuff },
        { "Rot", TalentCategory.Debuff },
        // Control & mana
        { "RestrictedInstinct", TalentCategory.Control }, { "SilenceTalent", TalentCategory.Control }, { "CelestialBlessing", TalentCategory.Control },
        { "CelestialInfluence", TalentCategory.Control }, { "ManaReaver", TalentCategory.Control }, { "Purify", TalentCategory.Control },
        { "Dispel", TalentCategory.Control },
        // Special
        { "Blackhole", TalentCategory.Special }, { "Chaotic", TalentCategory.Special }, { "Gamble", TalentCategory.Special },
        { "Mimic", TalentCategory.Special }, { "Nothingness", TalentCategory.Special }, { "SoulSwap", TalentCategory.Special },
    };

    public static TalentCategory CategoryOf(Talent t) =>
        t != null && byClass.TryGetValue(t.GetType().Name, out var c) ? c : TalentCategory.Special;

    public static string Name(TalentCategory c)
    {
        switch (c)
        {
            case TalentCategory.Attack: return "Attack";
            case TalentCategory.Defense: return "Defense";
            case TalentCategory.Healing: return "Healing & drain";
            case TalentCategory.StatBoost: return "Stat boost";
            case TalentCategory.Debuff: return "Debuff";
            case TalentCategory.Control: return "Control & mana";
            default: return "Special";
        }
    }
}
