using System.Collections.Generic;

public enum BuffCurrency { Gold, Diamonds }
public enum BuffGroup { Fusion, Collection, Economy, Combat }

/// <summary>One permanent buff (bought once, kept forever).</summary>
public class BuffDefinition
{
    public string id;
    public string name;
    public BuffGroup group;
    public BuffCurrency currency;
    public int price;
    public string description;
}

/// <summary>
/// Permanent purchasable buffs (user list, 15 Nov 2026). Prices are "mixed": economy and stamina buffs cost gold,
/// fusion, collection and combat buffs (and New Generation) cost diamonds. Change prices and numbers here.
/// "+X%" chances are added as percentage points.
/// </summary>
public static class Buffs
{
    // ids (saved in the save file, never rename)
    public const string FusionProtection = "fusion_protection";
    public const string FusionBlessing = "fusion_blessing";
    public const string FusionFortune = "fusion_fortune";
    public const string LuckyFusion = "lucky_fusion";
    public const string HuntersInstinct = "hunters_instinct";
    public const string RareEncounter = "rare_encounter";
    public const string TreasureHunter = "treasure_hunter";
    public const string Berserker = "berserker";
    public const string GoldCashback = "gold_cashback";
    public const string GoldenTouch = "golden_touch";
    public const string MerchantsFavor = "merchants_favor";
    public const string EfficientUpgrader = "efficient_upgrader";
    public const string SalvageExpert = "salvage_expert";
    public const string TheFlash = "the_flash";
    public const string HeadHunter = "head_hunter";
    public const string StrongestHero = "strongest_hero";
    public const string SecondWind = "second_wind";
    public const string StaminaMaster = "stamina_master";
    public const string Elixir = "elixir";
    public const string PotionOfStamina = "potion_of_stamina";
    public const string ReturningHeroes = "returning_heroes";
    public const string NewGeneration = "new_generation";

    // effect numbers
    public const float FusionBlessingChance = 0.03f;
    public const float FusionFortuneBonus = 0.03f;
    public const float LuckyFusionBase = 0.05f;
    public const float LuckyFusionPerOrb = 0.05f;
    public const float HuntersInstinctBonus = 0.10f;
    public const float RareEncounterChance = 0.03f;
    public const CardRarity RareEncounterMax = CardRarity.UltraRare;
    public const float TreasureHunterChance = 0.05f;
    public const float BerserkerDiscount = 0.10f;
    public const float GoldCashbackRate = 0.15f;
    public const float GoldenTouchBonus = 0.10f;
    public const float MerchantsFavorDiscount = 0.05f;
    public const float EfficientUpgraderDiscount = 0.10f;
    public const int FlashEvasion = 2;
    public const int HeadHunterCrit = 2;
    public const float StrongestHeroChance = 0.01f;
    public const float SecondWindChance = 0.01f;
    public const int StaminaMasterRegen = 1;
    public const int ElixirStamina = 2;
    public const int PotionMaxStamina = 20;
    public const int ReturningHeroesStamina = 1;

    const BuffCurrency G = BuffCurrency.Gold, D = BuffCurrency.Diamonds;

    public static readonly BuffDefinition[] All =
    {
        // Fusion & RNG (diamonds)
        new BuffDefinition { id = FusionProtection, name = "Fusion Protection", group = BuffGroup.Fusion, currency = D, price = 300,
            description = "When a fusion fails you lose only 1 fodder card instead of 2." },
        new BuffDefinition { id = FusionBlessing, name = "Fusion Blessing", group = BuffGroup.Fusion, currency = D, price = 800,
            description = "3% chance a successful fusion jumps one rarity higher (never into Mythic or God)." },
        new BuffDefinition { id = FusionFortune, name = "Fusion Fortune", group = BuffGroup.Fusion, currency = D, price = 300,
            description = "+3% fusion success rate." },
        new BuffDefinition { id = LuckyFusion, name = "Lucky Fusion", group = BuffGroup.Fusion, currency = D, price = 100,
            description = "After a fusion: 5% + 5% per Fusion Orb used chance to get 1 Fusion Orb back." },
        // Card Acquisition & Collection (diamonds)
        new BuffDefinition { id = HuntersInstinct, name = "Hunter's Instinct", group = BuffGroup.Collection, currency = D, price = 300,
            description = "+10% chance to get a card from defeated enemies." },
        new BuffDefinition { id = RareEncounter, name = "Rare Encounter", group = BuffGroup.Collection, currency = D, price = 300,
            description = "3% chance a card you obtain is one rarity higher (up to Ultra Rare)." },
        new BuffDefinition { id = TreasureHunter, name = "Treasure Hunter", group = BuffGroup.Collection, currency = D, price = 100,
            description = "+5% chance an enemy drops rare loot (orbs or diamonds)." },
        new BuffDefinition { id = Berserker, name = "Berserker", group = BuffGroup.Collection, currency = D, price = 50,
            description = "-10% cost to unlock build slots." },
        // Economy & Resource Management (gold)
        new BuffDefinition { id = GoldCashback, name = "Gold Cashback", group = BuffGroup.Economy, currency = G, price = 40000,
            description = "15% of the gold you spend comes back." },
        new BuffDefinition { id = GoldenTouch, name = "Golden Touch", group = BuffGroup.Economy, currency = G, price = 15000,
            description = "+10% gold earned." },
        new BuffDefinition { id = MerchantsFavor, name = "Merchant's Favor", group = BuffGroup.Economy, currency = G, price = 5000,
            description = "5% discount in the orb shop." },
        new BuffDefinition { id = EfficientUpgrader, name = "Efficient Upgrader", group = BuffGroup.Economy, currency = G, price = 15000,
            description = "-10% gold on card rerolls." },
        new BuffDefinition { id = SalvageExpert, name = "Salvage Expert", group = BuffGroup.Economy, currency = G, price = 5000,
            description = "Unlocks dismantling cards for gold (half the stat reroll cost of their rarity)." },
        // Combat & Exploration (combat: diamonds, stamina: gold)
        new BuffDefinition { id = TheFlash, name = "The Flash", group = BuffGroup.Combat, currency = D, price = 300,
            description = "+2 evasion rating (about +1.9% evade chance at low evasion)." },
        new BuffDefinition { id = HeadHunter, name = "Head Hunter", group = BuffGroup.Combat, currency = D, price = 300,
            description = "+2% critical chance." },
        new BuffDefinition { id = StrongestHero, name = "Strongest Hero", group = BuffGroup.Combat, currency = D, price = 100,
            description = "1% chance the enemy loses 50% HP when the battle starts." },
        new BuffDefinition { id = SecondWind, name = "Second Wind", group = BuffGroup.Combat, currency = D, price = 100,
            description = "1% chance to revive with 1 HP instead of dying (once per battle)." },
        new BuffDefinition { id = StaminaMaster, name = "Stamina Master", group = BuffGroup.Combat, currency = G, price = 40000,
            description = "+1 stamina regen: 2 stamina every 5 minutes." },
        new BuffDefinition { id = Elixir, name = "Elixir", group = BuffGroup.Combat, currency = G, price = 15000,
            description = "+2 stamina after defeating an enemy." },
        new BuffDefinition { id = PotionOfStamina, name = "Potion of Stamina", group = BuffGroup.Combat, currency = G, price = 15000,
            description = "+20 max stamina (80 -> 100)." },
        new BuffDefinition { id = ReturningHeroes, name = "Returning Heroes", group = BuffGroup.Combat, currency = G, price = 5000,
            description = "+1 stamina when you are defeated." },
        new BuffDefinition { id = NewGeneration, name = "New Generation", group = BuffGroup.Combat, currency = D, price = 3000,
            description = "Unlocks the GOD rarity: Mythic cards can be fused into God." },
    };

    static Dictionary<string, BuffDefinition> byId;

    public static BuffDefinition Find(string id)
    {
        if (byId == null)
        {
            byId = new Dictionary<string, BuffDefinition>();
            foreach (var b in All) byId[b.id] = b;
        }
        return id != null && byId.TryGetValue(id, out var def) ? def : null;
    }

    public static string GroupName(BuffGroup g)
    {
        switch (g)
        {
            case BuffGroup.Fusion: return "Fusion & RNG";
            case BuffGroup.Collection: return "Cards & Collection";
            case BuffGroup.Economy: return "Economy & Resources";
            default: return "Combat & Exploration";
        }
    }
}
