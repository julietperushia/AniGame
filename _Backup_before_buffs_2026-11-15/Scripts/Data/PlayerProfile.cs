using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public enum OrbType { Fusion, Element, Talent }

/// <summary>Everything saved to disk (JSON in Application.persistentDataPath).</summary>
[Serializable]
public class SaveData
{
    public int version = PlayerProfile.SaveVersion;
    /// <summary>CardDatabase.dataVersion this save was made with (campaign floors change with the sheet).</summary>
    public string dataVersion;

    public int gold = GameRules.StartGold;
    public int diamonds;
    public int fusionOrbs = GameRules.StartFusionOrbs;
    public int elementOrbs;
    public int talentOrbs;

    public int stamina = 100;
    public long staminaTimestamp;

    public int nextUid = 1;
    public List<CardInstance> cards = new List<CardInstance>();
    public List<BuildSlot> slots = new List<BuildSlot>();
    public int activeSlot;

    /// <summary>Index = LOC-1, value = number of floors cleared in order.</summary>
    public List<int> clearedFloors = new List<int>();

    public int battlesWon;
    public int battlesLost;
    public int packsOpened;
    public int fusionsSucceeded;
    public int fusionsFailed;

    public int battleSpeed = 1;
    public float sfxVolume = 0.7f;
}

/// <summary>What a battle gave the player (shown on the result panel).</summary>
public class BattleRewards
{
    public bool victory;
    public bool firstClear;
    public bool locationCleared;
    public int gold;
    public int fusionOrbs;
    public CardInstance droppedCard;
    public readonly List<string> lines = new List<string>();
}

public class FusionResult
{
    public bool success;
    public float rate;
    public CardRarity from;
    public CardRarity to;
    public int orbsSpent;
}

/// <summary>
/// Static access to the save for the single-card system (design doc): currencies and orbs, owned card instances,
/// build slots, rerolls, fusion, locks, gacha, shop, floor rewards and campaign progress.
/// </summary>
public static class PlayerProfile
{
    public const int SaveVersion = 2;
    public const int StaminaCost = 5;
    public const int StaminaRegenSeconds = 120;
    public const int MaxStamina = 100;

    static SaveData data;

    /// <summary>Raised after every Save() so UI can refresh.</summary>
    public static event Action Changed;

    public static string SavePath => Path.Combine(Application.persistentDataPath, "anigame_save.json");
    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public static SaveData Data
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

    // ================================================================== save / load

    public static void Load()
    {
        data = null;
        try
        {
            if (File.Exists(SavePath)) data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AniGame] Could not read save file, starting a new game. {ex.Message}");
            data = null;
        }

        string current = CurrentDataVersion;
        if (data != null && (data.version != SaveVersion || (!string.IsNullOrEmpty(current) && data.dataVersion != current)))
        {
            BackupSave(data.version != SaveVersion ? $"v{data.version}" : (data.dataVersion ?? "legacy"));
            Debug.Log("[AniGame] Save made with an older game version or campaign sheet; starting a new save (old one backed up).");
            data = null;
        }

        if (data == null) NewGame();
        else Validate();
        UpdateStamina();
    }

    public static void Save()
    {
        if (data == null) return;
        try { File.WriteAllText(SavePath, JsonUtility.ToJson(data, true)); }
        catch (Exception ex) { Debug.LogError($"[AniGame] Failed to save: {ex.Message}"); }
        Changed?.Invoke();
    }

    public static void DeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        data = null;
    }

    static string CurrentDataVersion
    {
        get
        {
            var db = CardDatabase.Instance;
            return db != null ? db.dataVersion : null;
        }
    }

    static void BackupSave(string tag)
    {
        try
        {
            if (!File.Exists(SavePath)) return;
            string safe = string.IsNullOrEmpty(tag) ? "old" : tag.Replace('/', '_').Replace('\\', '_');
            File.Copy(SavePath, Path.Combine(Application.persistentDataPath, $"anigame_save_backup_{safe}_{DateTime.Now:yyyyMMdd_HHmmss}.json"), true);
        }
        catch (Exception ex) { Debug.LogWarning($"[AniGame] Could not back up the old save: {ex.Message}"); }
    }

    static void NewGame()
    {
        data = new SaveData { staminaTimestamp = Now, dataVersion = CurrentDataVersion };
        EnsureSlots();
        GiveStarterCard();
        Save();
    }

    /// <summary>Starter: one Common card of a random LOC 1 character with a random build, placed in slot 1.</summary>
    static void GiveStarterCard()
    {
        var db = CardDatabase.Instance;
        var loc1 = db != null ? db.GetLocation(1) : null;
        if (loc1 == null || loc1.floors.Count == 0) return;
        var art = loc1.floors[UnityEngine.Random.Range(0, loc1.floors.Count)];
        var card = AddCard(art, CardRarity.Common, RandomBuild(CardRarity.Common));
        data.slots[0].cardUid = card.uid;
        data.slots[0].build = card.build.Clone();
        data.activeSlot = 0;
    }

    static void EnsureSlots()
    {
        if (data.slots == null) data.slots = new List<BuildSlot>();
        while (data.slots.Count < GameRules.SlotCount) data.slots.Add(new BuildSlot());
        data.slots[0].unlocked = true; // slot 1 is free
    }

    static void Validate()
    {
        if (data.cards == null) data.cards = new List<CardInstance>();
        if (data.clearedFloors == null) data.clearedFloors = new List<int>();
        EnsureSlots();
        foreach (var c in data.cards)
        {
            if (c.build == null) c.build = RandomBuild(c.rarity);
            if (c.uid >= data.nextUid) data.nextUid = c.uid + 1;
        }
        foreach (var s in data.slots)
            if (s.HasCard && FindCard(s.cardUid) == null) s.cardUid = 0;
        if (data.cards.Count == 0) GiveStarterCard();
        if (!data.slots.Any(s => s.HasCard) && data.cards.Count > 0)
        {
            data.slots[0].cardUid = data.cards[0].uid;
            data.slots[0].build = data.cards[0].build.Clone();
        }
        data.activeSlot = ValidActiveSlot(data.activeSlot);
    }

    // ================================================================== currencies & orbs

    public static int GetOrbs(OrbType type)
    {
        switch (type)
        {
            case OrbType.Fusion: return Data.fusionOrbs;
            case OrbType.Element: return Data.elementOrbs;
            default: return Data.talentOrbs;
        }
    }

    public static void AddOrbs(OrbType type, int amount)
    {
        switch (type)
        {
            case OrbType.Fusion: Data.fusionOrbs = Mathf.Max(0, Data.fusionOrbs + amount); break;
            case OrbType.Element: Data.elementOrbs = Mathf.Max(0, Data.elementOrbs + amount); break;
            default: Data.talentOrbs = Mathf.Max(0, Data.talentOrbs + amount); break;
        }
    }

    public static string OrbName(OrbType type) => type == OrbType.Fusion ? "Fusion Orb" : type == OrbType.Element ? "Element Orb" : "Talent Orb";

    public static int OrbPrice(OrbType type) =>
        type == OrbType.Fusion ? GameRules.FusionOrbPrice : type == OrbType.Element ? GameRules.ElementOrbPrice : GameRules.TalentOrbPrice;

    // ================================================================== cards

    public static Card Art(CardInstance card)
    {
        if (card == null) return null;
        var db = CardDatabase.Instance;
        return db != null ? db.FindByKey(card.key) : null;
    }

    public static CardInstance FindCard(int uid) => uid <= 0 ? null : Data.cards.FirstOrDefault(c => c.uid == uid);

    public static int RandomRoll(int tier)
    {
        int dev = GameRules.MaxDeviation(tier);
        return UnityEngine.Random.Range(-dev, dev + 1);
    }

    /// <summary>A fresh build: each stat rolled within +-MaxDeviation, element by Element Orb odds, a random talent.</summary>
    public static BuildData RandomBuild(CardRarity rarity, string talent = null, CardElement? element = null)
    {
        int tier = GameRules.Tier(rarity);
        var db = CardDatabase.Instance;
        if (string.IsNullOrEmpty(talent))
        {
            var t = db != null ? db.RandomTalent() : null;
            talent = t != null ? t.DisplayName : "";
        }
        return new BuildData
        {
            hp = RandomRoll(tier),
            atk = RandomRoll(tier),
            def = RandomRoll(tier),
            spd = RandomRoll(tier),
            element = element ?? ElementSystem.RollRandom(),
            talent = talent
        };
    }

    public static CardInstance AddCard(Card art, CardRarity rarity, BuildData build)
    {
        var d = data ?? Data;
        var card = new CardInstance
        {
            uid = d.nextUid++,
            key = art != null ? art.Key : "",
            rarity = rarity,
            build = build ?? RandomBuild(rarity)
        };
        d.cards.Add(card);
        return card;
    }

    static void RemoveCard(CardInstance card)
    {
        if (card == null) return;
        Data.cards.Remove(card);
        foreach (var s in Data.slots) if (s.cardUid == card.uid) s.cardUid = 0;
    }

    public static bool IsInSlot(int uid) => Data.slots.Any(s => s.unlocked && s.cardUid == uid);

    /// <summary>Locked by the player, or automatically because a build slot uses it (doc 10).</summary>
    public static bool IsProtected(CardInstance card) => card != null && (card.locked || IsInSlot(card.uid));

    public static void ToggleLock(CardInstance card)
    {
        if (card == null) return;
        card.locked = !card.locked;
        Save();
    }

    // ================================================================== build slots

    public static BuildSlot GetSlot(int index) => index >= 0 && index < Data.slots.Count ? Data.slots[index] : null;

    public static string UnlockSlot(int index)
    {
        var slot = GetSlot(index);
        if (slot == null) return "No such slot";
        if (slot.unlocked) return null;
        int cost = GameRules.SlotUnlockCost(index);
        if (Data.diamonds < cost) return $"Needs {cost} diamonds";
        Data.diamonds -= cost;
        slot.unlocked = true;
        Save();
        return null;
    }

    public static string SaveToSlot(int index, CardInstance card, BuildData build)
    {
        var slot = GetSlot(index);
        if (slot == null || card == null || build == null) return "Nothing to save";
        if (!slot.unlocked) return $"Slot {index + 1} is locked";
        slot.cardUid = card.uid;
        slot.build = build.Clone();
        var active = GetSlot(Data.activeSlot);
        if (active == null || !active.unlocked || !active.HasCard) Data.activeSlot = index;
        Save();
        return null;
    }

    public static int ActiveSlot
    {
        get => ValidActiveSlot(Data.activeSlot);
        set
        {
            Data.activeSlot = ValidActiveSlot(value);
            Save();
        }
    }

    static int ValidActiveSlot(int index)
    {
        var slots = (data ?? Data).slots;
        if (index >= 0 && index < slots.Count && slots[index].unlocked && slots[index].HasCard) return index;
        for (int i = 0; i < slots.Count; i++) if (slots[i].unlocked && slots[i].HasCard) return i;
        return 0;
    }

    /// <summary>The fighter built from a slot (null if the slot is empty).</summary>
    public static CardRuntime SlotRuntime(int index)
    {
        var slot = GetSlot(index);
        if (slot == null || !slot.unlocked || !slot.HasCard) return null;
        var card = FindCard(slot.cardUid);
        return card != null ? CardRuntime.ForBuild(card, slot.build) : null;
    }

    // ================================================================== rerolls (preview, then save to a slot)

    /// <summary>Spends gold and returns a copy of <paramref name="from"/> with all 4 stats re-rolled. Null reason = ok.</summary>
    public static string RerollStats(CardInstance card, BuildData from, out BuildData result)
    {
        result = null;
        if (card == null || from == null) return "No card selected";
        int cost = GameRules.StatRerollCost(card.Tier);
        if (Data.gold < cost) return $"Needs {cost:N0} gold";
        Data.gold -= cost;
        result = from.Clone();
        result.hp = RandomRoll(card.Tier);
        result.atk = RandomRoll(card.Tier);
        result.def = RandomRoll(card.Tier);
        result.spd = RandomRoll(card.Tier);
        Save();
        return null;
    }

    public static string RerollElement(CardInstance card, BuildData from, out BuildData result)
    {
        result = null;
        if (card == null || from == null) return "No card selected";
        if (Data.elementOrbs < 1) return "Needs 1 Element Orb";
        Data.elementOrbs--;
        result = from.Clone();
        result.element = ElementSystem.RollRandom();
        Save();
        return null;
    }

    public static string RerollTalent(CardInstance card, BuildData from, out BuildData result)
    {
        result = null;
        if (card == null || from == null) return "No card selected";
        if (Data.talentOrbs < 1) return "Needs 1 Talent Orb";
        var t = CardDatabase.Instance != null ? CardDatabase.Instance.RandomTalent() : null;
        if (t == null) return "No talents in the database";
        Data.talentOrbs--;
        result = from.Clone();
        result.talent = t.DisplayName;
        Save();
        return null;
    }

    // ================================================================== fusion (doc 4)

    /// <summary>Cards that can be fodder for <paramref name="main"/>: same rarity, not the main card, not locked / in a slot.</summary>
    public static List<CardInstance> FodderCandidates(CardInstance main)
    {
        if (main == null) return new List<CardInstance>();
        return Data.cards.Where(c => c != main && c.rarity == main.rarity && !IsProtected(c))
                         .OrderBy(c => c.build.TotalRoll).ToList();
    }

    public static string CanFuse(CardInstance main, IList<CardInstance> fodder, int extraOrbs)
    {
        if (main == null) return "Pick a main card";
        if (!GameRules.CanFuseFrom(main.Tier)) return "God is the maximum rarity";
        if (fodder == null || fodder.Count != GameRules.FusionFodderCount) return $"Needs {GameRules.FusionFodderCount} fodder cards of the same rarity";
        if (fodder.Distinct().Count() != fodder.Count) return "Pick two different fodder cards";
        foreach (var f in fodder)
        {
            if (f == null || f == main) return "Invalid fodder";
            if (f.rarity != main.rarity) return "Fodder must be the same rarity";
            if (IsProtected(f)) return "Locked or slotted cards can't be fodder";
        }
        int orbs = GameRules.FusionOrbCost(main.Tier) + Mathf.Clamp(extraOrbs, 0, GameRules.MaxExtraFusionOrbs);
        if (Data.fusionOrbs < orbs) return $"Needs {orbs} Fusion Orbs";
        return null;
    }

    /// <summary>Success: main card +1 rarity (Roll, element, talent kept; every slot using it ranks up too).
    /// Failure: the 2 fodder cards and all orbs are lost; the main card stays.</summary>
    public static FusionResult Fuse(CardInstance main, IList<CardInstance> fodder, int extraOrbs)
    {
        if (CanFuse(main, fodder, extraOrbs) != null) return null;
        extraOrbs = Mathf.Clamp(extraOrbs, 0, GameRules.MaxExtraFusionOrbs);
        var result = new FusionResult
        {
            from = main.rarity,
            rate = GameRules.FusionSuccessRate(main.Tier, extraOrbs),
            orbsSpent = GameRules.FusionOrbCost(main.Tier) + extraOrbs
        };
        Data.fusionOrbs -= result.orbsSpent;
        foreach (var f in fodder.ToList()) RemoveCard(f);
        result.success = UnityEngine.Random.value < result.rate;
        if (result.success)
        {
            main.rarity = (CardRarity)Mathf.Min(GameRules.MaxTier, main.Tier + 1);
            Data.fusionsSucceeded++;
        }
        else
        {
            Data.fusionsFailed++;
        }
        result.to = main.rarity;
        Save();
        return result;
    }

    // ================================================================== gacha (doc 7)

    public static string CanOpenPack(PackDefinition pack)
    {
        if (pack == null) return "Unknown pack";
        if (pack.currency == PackCurrency.Gold && Data.gold < pack.price) return $"Needs {pack.price:N0} gold";
        if (pack.currency == PackCurrency.Diamonds && Data.diamonds < pack.price) return $"Needs {pack.price:N0} diamonds";
        return null;
    }

    public static List<PackItem> OpenPack(PackDefinition pack)
    {
        if (CanOpenPack(pack) != null) return null;
        if (pack.currency == PackCurrency.Gold) Data.gold -= pack.price;
        else Data.diamonds -= pack.price;

        var items = new List<PackItem> { GiveCardItem(pack.guaranteed) };
        for (int i = 1; i < PackDefinition.ItemCount; i++)
        {
            var e = Packs.RollEntry(pack);
            if (e.type == RewardType.Card) items.Add(GiveCardItem(e.rarity));
            else
            {
                GiveCurrency(e.type, e.amount);
                items.Add(new PackItem { type = e.type, amount = e.amount });
            }
        }
        Data.packsOpened++;
        Save();
        return items;
    }

    static PackItem GiveCardItem(CardRarity rarity)
    {
        var db = CardDatabase.Instance;
        var art = db != null ? db.RandomCardOfRarity(rarity) : null;
        var card = AddCard(art, rarity, RandomBuild(rarity));
        return new PackItem { type = RewardType.Card, amount = 1, rarity = rarity, card = card };
    }

    static void GiveCurrency(RewardType type, int amount)
    {
        switch (type)
        {
            case RewardType.Gold: Data.gold += amount; break;
            case RewardType.Diamonds: Data.diamonds += amount; break;
            case RewardType.FusionOrb: Data.fusionOrbs += amount; break;
            case RewardType.ElementOrb: Data.elementOrbs += amount; break;
            case RewardType.TalentOrb: Data.talentOrbs += amount; break;
        }
    }

    // ================================================================== shop (doc 9)

    public static string BuyOrbs(OrbType type, int quantity)
    {
        quantity = Mathf.Max(1, quantity);
        int cost = OrbPrice(type) * quantity;
        if (Data.gold < cost) return $"Needs {cost:N0} gold";
        Data.gold -= cost;
        AddOrbs(type, quantity);
        Save();
        return null;
    }

    // ================================================================== stamina

    public static void UpdateStamina()
    {
        var d = data;
        if (d == null) return;
        long now = Now;
        if (d.staminaTimestamp <= 0 || d.staminaTimestamp > now) d.staminaTimestamp = now;
        if (d.stamina >= MaxStamina) { d.staminaTimestamp = now; return; }
        long gained = (now - d.staminaTimestamp) / StaminaRegenSeconds;
        if (gained > 0)
        {
            d.stamina = (int)Math.Min(MaxStamina, d.stamina + gained);
            d.staminaTimestamp += gained * StaminaRegenSeconds;
            if (d.stamina >= MaxStamina) d.staminaTimestamp = now;
        }
    }

    public static int SecondsToNextStamina
    {
        get
        {
            UpdateStamina();
            if (Data.stamina >= MaxStamina) return 0;
            return (int)Math.Max(0, StaminaRegenSeconds - (Now - Data.staminaTimestamp));
        }
    }

    public static bool TrySpendStamina(int amount)
    {
        UpdateStamina();
        if (Data.stamina < amount) return false;
        if (Data.stamina >= MaxStamina) Data.staminaTimestamp = Now;
        Data.stamina -= amount;
        Save();
        return true;
    }

    public static void RefillStamina()
    {
        Data.stamina = Mathf.Max(Data.stamina, MaxStamina);
        Data.staminaTimestamp = Now;
        Save();
    }

    // ================================================================== campaign progress

    public static int ClearedFloors(int location)
    {
        int i = location - 1;
        return i >= 0 && i < Data.clearedFloors.Count ? Data.clearedFloors[i] : 0;
    }

    static void SetClearedFloors(int location, int count)
    {
        while (Data.clearedFloors.Count < location) Data.clearedFloors.Add(0);
        Data.clearedFloors[location - 1] = count;
    }

    public static bool IsLocationCleared(int location)
    {
        var db = CardDatabase.Instance;
        int floors = db != null ? db.FloorCount(location) : 0;
        return floors > 0 && ClearedFloors(location) >= floors;
    }

    public static bool IsLocationUnlocked(int location) => location <= 1 || IsLocationCleared(location - 1);

    public static int HighestUnlockedLocation
    {
        get
        {
            var db = CardDatabase.Instance;
            int count = db != null ? db.LocationCount : 1;
            int loc = 1;
            while (loc < count && IsLocationCleared(loc)) loc++;
            return loc;
        }
    }

    public static bool IsFloorUnlocked(int location, int floor) => IsLocationUnlocked(location) && floor <= ClearedFloors(location) + 1;
    public static bool IsFloorCleared(int location, int floor) => floor <= ClearedFloors(location);

    public static int NextFloor(int location)
    {
        var db = CardDatabase.Instance;
        int floors = db != null ? db.FloorCount(location) : 1;
        return Mathf.Clamp(ClearedFloors(location) + 1, 1, Mathf.Max(1, floors));
    }

    // ================================================================== rewards (doc 8)

    public static BattleRewards ApplyVictory(int location, int floor)
    {
        var r = new BattleRewards { victory = true };
        var db = CardDatabase.Instance;
        Card enemy = db != null ? db.GetFloorCard(location, floor) : null;

        r.firstClear = !IsFloorCleared(location, floor);
        if (r.firstClear)
        {
            SetClearedFloors(location, Mathf.Max(ClearedFloors(location), floor));
            r.locationCleared = IsLocationCleared(location);
        }

        r.gold = GameRules.FloorGold(location, floor);
        Data.gold += r.gold;
        r.lines.Add($"+{r.gold:N0} Gold");

        if (enemy != null && UnityEngine.Random.value < GameRules.CardDropChance(enemy.rarity))
        {
            string talent = enemy.talent != null ? enemy.talent.DisplayName : null;
            r.droppedCard = AddCard(enemy, enemy.rarity, RandomBuild(enemy.rarity, talent, enemy.element));
            r.lines.Add($"CARD DROP: {enemy.DisplayName} ({CardStats.RarityName(enemy.rarity)})");
        }
        if (UnityEngine.Random.value < GameRules.FusionOrbDropChance)
        {
            r.fusionOrbs = 1;
            Data.fusionOrbs += 1;
            r.lines.Add("+1 Fusion Orb");
        }
        if (r.locationCleared) r.lines.Add($"LOC {location} CLEARED! LOC {location + 1} unlocked");

        Data.battlesWon++;
        Save();
        return r;
    }

    public static void ApplyDefeat()
    {
        Data.battlesLost++;
        Save();
    }
}
