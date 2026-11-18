using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>A card the player owns (one copy). Stats come from the Card asset found by <see cref="key"/>.</summary>
[Serializable]
public class OwnedCard
{
    public int uid;
    public string key;
    public int level = 1;
    public int xp;
    public int evolution;
    public bool favorite;
}

/// <summary>Everything saved to disk (JSON in Application.persistentDataPath).</summary>
[Serializable]
public class SaveData
{
    public int version = 1;
    /// <summary>CardDatabase.dataVersion this save was made with (campaign floors change with the sheet).</summary>
    public string dataVersion;
    public int playerLevel = 1;
    public int playerXp;
    public int gold = 1000;
    public int diamonds = 10;
    public int stamina = 100;
    public long staminaTimestamp;
    public int nextUid = 1;
    public List<OwnedCard> cards = new List<OwnedCard>();
    public List<int> team = new List<int>();
    /// <summary>Index = LOC-1, value = number of floors cleared in order.</summary>
    public List<int> clearedFloors = new List<int>();
    public int battlesWon;
    public int battlesLost;
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
    public int diamonds;
    public int playerXp;
    public int cardXp;
    public bool playerLevelUp;
    public Card newCard;
    public readonly List<string> lines = new List<string>();
}

/// <summary>
/// Static access to the player's save: currencies, stamina, collection, team and campaign progress.
/// </summary>
public static class PlayerProfile
{
    public const int TeamSize = 3;
    public const int StaminaCost = 5;
    public const int StaminaRegenSeconds = 120;   // Anigame: +2 stamina every 4 minutes
    public const int MaxStaminaCap = 210;         // Anigame: 210 max stamina at level 80
    public const float RepeatDropChance = 0.10f;

    static SaveData data;

    /// <summary>Raised after every Save() so UI can refresh.</summary>
    public static event Action Changed;

    public static string SavePath => Path.Combine(Application.persistentDataPath, "anigame_save.json");
    public static bool HasSaveFile => File.Exists(SavePath);
    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public static SaveData Data
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

    // ------------------------------------------------------------------ save / load

    public static void Load()
    {
        data = null;
        try
        {
            if (File.Exists(SavePath))
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AniGame] Could not read save file, starting a new game. {ex.Message}");
            data = null;
        }

        // A save made with a different campaign sheet points at floors that no longer mean the same card.
        string current = CurrentDataVersion;
        if (data != null && !string.IsNullOrEmpty(current) && data.dataVersion != current)
        {
            BackupSave(string.IsNullOrEmpty(data.dataVersion) ? "legacy" : data.dataVersion);
            Debug.Log($"[AniGame] Campaign data changed ({data.dataVersion ?? "legacy"} -> {current}); starting a new save. The old one was backed up.");
            data = null;
        }

        if (data == null) NewGame();
        else Validate();
        UpdateStamina();
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
            string backup = Path.Combine(Application.persistentDataPath, $"anigame_save_backup_{safe}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            File.Copy(SavePath, backup, true);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AniGame] Could not back up the old save: {ex.Message}");
        }
    }

    public static void Save()
    {
        if (data == null) return;
        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AniGame] Failed to save: {ex.Message}");
        }
        Changed?.Invoke();
    }

    public static void DeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        data = null;
    }

    static void NewGame()
    {
        data = new SaveData { staminaTimestamp = Now, dataVersion = CurrentDataVersion };
        GiveStarterCards();
        Save();
    }

    public const int StarterLevel = 5;

    static void GiveStarterCards()
    {
        foreach (var c in PickStarterCards(CardDatabase.Instance))
        {
            var oc = AddCard(c, StarterLevel);
            data.team.Add(oc.uid);
        }
    }

    /// <summary>
    /// Starter team = the LOC 1 characters (highest rarity tier there) that win the most simulated fights against
    /// LOC 1's floors. Stat totals are too flat in the sheet's early locations to pick by numbers alone.
    /// </summary>
    public static List<Card> PickStarterCards(CardDatabase db, int count = TeamSize)
    {
        var loc1 = db != null ? db.GetLocation(1) : null;
        if (loc1 == null || loc1.floors.Count == 0) return new List<Card>();
        var floors = loc1.floors.Where(c => c != null).ToList();
        var topRarity = floors.Max(c => c.rarity);
        var candidates = floors.Where(c => c.rarity == topRarity)
                               .GroupBy(c => c.cardName).Select(g => g.First()).ToList();
        if (candidates.Count < count)
            candidates = floors.GroupBy(c => c.cardName).Select(g => g.OrderByDescending(c => c.rarity).First()).ToList();

        return candidates.Select(c => new { card = c, score = StarterScore(c, floors) })
                         .OrderByDescending(x => x.score).ThenByDescending(x => x.card.StatTotal)
                         .Take(count).Select(x => x.card).ToList();
    }

    static int StarterScore(Card candidate, List<Card> floors)
    {
        int wins = 0, seed = 7001;
        foreach (var enemy in floors)
        {
            for (int i = 0; i < 3; i++)
            {
                var engine = new BattleEngine(new CardRuntime(candidate, StarterLevel, 0), new CardRuntime(enemy, 1, 0), seed++);
                if (engine.RunToEnd() == BattleState.PlayerWon) wins++;
            }
        }
        return wins;
    }

    static void Validate()
    {
        if (data.cards == null) data.cards = new List<OwnedCard>();
        if (data.team == null) data.team = new List<int>();
        if (data.clearedFloors == null) data.clearedFloors = new List<int>();
        data.team = data.team.Where(uid => data.cards.Any(c => c.uid == uid)).Distinct().Take(TeamSize).ToList();
        foreach (var c in data.cards)
        {
            c.level = Mathf.Clamp(c.level, 1, CardStats.MaxLevel);
            c.evolution = Mathf.Clamp(c.evolution, 0, CardStats.MaxEvolution);
            if (c.uid >= data.nextUid) data.nextUid = c.uid + 1;
        }
        if (data.cards.Count == 0) GiveStarterCards();
        if (data.team.Count == 0)
            foreach (var c in data.cards.Take(TeamSize)) data.team.Add(c.uid);
    }

    // ------------------------------------------------------------------ collection

    public static Card Resolve(OwnedCard owned)
    {
        if (owned == null) return null;
        var db = CardDatabase.Instance;
        return db != null ? db.FindByKey(owned.key) : null;
    }

    public static OwnedCard AddCard(Card card, int level = 1)
    {
        var d = data ?? Data;
        var owned = new OwnedCard
        {
            uid = d.nextUid++,
            key = card.Key,
            level = Mathf.Clamp(level, 1, CardStats.MaxLevel)
        };
        d.cards.Add(owned);
        return owned;
    }

    public static OwnedCard FindCard(int uid) => Data.cards.FirstOrDefault(c => c.uid == uid);

    public static void RemoveCard(OwnedCard owned)
    {
        if (owned == null) return;
        Data.cards.Remove(owned);
        Data.team.Remove(owned.uid);
    }

    public static bool IsInTeam(int uid) => Data.team.Contains(uid);

    /// <summary>Team members in slot order (only cards that still resolve to a Card asset).</summary>
    public static List<OwnedCard> GetTeam()
    {
        var list = new List<OwnedCard>();
        foreach (int uid in Data.team)
        {
            var oc = FindCard(uid);
            if (oc != null && Resolve(oc) != null) list.Add(oc);
        }
        return list;
    }

    public static OwnedCard GetTeamSlot(int slot)
    {
        return slot >= 0 && slot < Data.team.Count ? FindCard(Data.team[slot]) : null;
    }

    /// <summary>Puts a card in a team slot (0-2). If it is already in another slot the two swap.</summary>
    public static void SetTeamSlot(int slot, int uid)
    {
        slot = Mathf.Clamp(slot, 0, TeamSize - 1);
        var team = Data.team;
        int existing = team.IndexOf(uid);
        if (slot >= team.Count)
        {
            if (existing >= 0) team.RemoveAt(existing);
            team.Add(uid);
        }
        else if (existing >= 0)
        {
            int other = team[slot];
            team[slot] = uid;
            team[existing] = other;
        }
        else
        {
            team[slot] = uid;
        }
        Save();
    }

    public static void RemoveFromTeam(int uid)
    {
        if (Data.team.Count <= 1) return; // always keep at least one fighter
        Data.team.Remove(uid);
        Save();
    }

    // ------------------------------------------------------------------ stamina

    public static int MaxStamina => Mathf.Min(MaxStaminaCap, 100 + (Data.playerLevel - 1) * 2);

    public static void UpdateStamina()
    {
        var d = data;
        if (d == null) return;
        long now = Now;
        if (d.staminaTimestamp <= 0 || d.staminaTimestamp > now) d.staminaTimestamp = now;
        if (d.stamina >= MaxStamina)
        {
            d.staminaTimestamp = now;
            return;
        }
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

    // ------------------------------------------------------------------ campaign progress

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

    /// <summary>LOC 1 is always open; every other location opens once the previous one is fully cleared.</summary>
    public static bool IsLocationUnlocked(int location)
    {
        if (location <= 1) return true;
        return IsLocationCleared(location - 1);
    }

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

    /// <summary>The first floor the player has not cleared yet (or the last floor if all are cleared).</summary>
    public static int NextFloor(int location)
    {
        var db = CardDatabase.Instance;
        int floors = db != null ? db.FloorCount(location) : 1;
        return Mathf.Clamp(ClearedFloors(location) + 1, 1, Mathf.Max(1, floors));
    }

    // ------------------------------------------------------------------ rewards & levels

    public static int PlayerXpToNext(int level) => 100 + level * 50;

    public static BattleRewards ApplyVictory(int location, int floor, List<OwnedCard> participants)
    {
        var r = new BattleRewards { victory = true };
        var db = CardDatabase.Instance;
        Card enemy = db != null ? db.GetFloorCard(location, floor) : null;

        r.firstClear = !IsFloorCleared(location, floor);
        r.gold = 40 + location * 12 + floor * 3;
        if (r.firstClear) r.gold *= 2;
        r.playerXp = 10 + location;
        r.cardXp = 25 + location * 6;

        if (r.firstClear)
        {
            SetClearedFloors(location, Mathf.Max(ClearedFloors(location), floor));
            if (enemy != null) r.newCard = enemy; // first clear always recruits the floor card
            if (IsLocationCleared(location))
            {
                r.locationCleared = true;
                r.gold += 500 + location * 50;
                r.diamonds += 5;
            }
        }
        else if (enemy != null && UnityEngine.Random.value < RepeatDropChance)
        {
            r.newCard = enemy;
        }

        Data.gold += r.gold;
        Data.diamonds += r.diamonds;
        Data.battlesWon++;
        r.lines.Add($"+{r.gold} Gold");
        if (r.diamonds > 0) r.lines.Add($"+{r.diamonds} Diamonds");

        r.playerLevelUp = AddPlayerXp(r.playerXp);
        r.lines.Add($"+{r.playerXp} Player XP{(r.playerLevelUp ? $"  (LEVEL UP! Lv {Data.playerLevel})" : "")}");

        if (participants != null)
        {
            foreach (var p in participants)
            {
                int before = p.level;
                AddCardXp(p, r.cardXp);
                var card = Resolve(p);
                string name = card != null ? card.DisplayName : p.key;
                r.lines.Add(p.level > before
                    ? $"{name} +{r.cardXp} XP  (Lv {before} -> {p.level})"
                    : $"{name} +{r.cardXp} XP");
            }
        }

        if (r.newCard != null)
        {
            AddCard(r.newCard, 1);
            r.lines.Add($"NEW CARD: {r.newCard.DisplayName} ({CardStats.RarityName(r.newCard.rarity)})");
        }
        if (r.locationCleared) r.lines.Add($"LOC {location} CLEARED! LOC {location + 1} unlocked");

        Save();
        return r;
    }

    public static void ApplyDefeat()
    {
        Data.battlesLost++;
        Save();
    }

    /// <summary>Adds player XP; returns true on level up (level ups refill stamina, like Anigame).</summary>
    public static bool AddPlayerXp(int xp)
    {
        bool leveled = false;
        Data.playerXp += xp;
        while (Data.playerXp >= PlayerXpToNext(Data.playerLevel))
        {
            Data.playerXp -= PlayerXpToNext(Data.playerLevel);
            Data.playerLevel++;
            leveled = true;
        }
        if (leveled)
        {
            Data.stamina = Mathf.Max(Data.stamina, MaxStamina);
            Data.staminaTimestamp = Now;
        }
        return leveled;
    }

    /// <summary>Adds XP to a card and levels it up. Returns the number of levels gained.</summary>
    public static int AddCardXp(OwnedCard card, int xp)
    {
        if (card == null || xp <= 0) return 0;
        int gained = 0;
        card.xp += xp;
        while (card.level < CardStats.MaxLevel && card.xp >= CardStats.XpToNext(card.level))
        {
            card.xp -= CardStats.XpToNext(card.level);
            card.level++;
            gained++;
        }
        if (card.level >= CardStats.MaxLevel) card.xp = 0;
        return gained;
    }

    /// <summary>Cards that can be fed to <paramref name="target"/>: not in the team, not favourite, cheapest first.</summary>
    public static List<OwnedCard> FodderCandidates(OwnedCard target)
    {
        return Data.cards
            .Where(c => c != target && !IsInTeam(c.uid) && !c.favorite && Resolve(c) != null)
            .OrderBy(c => (int)Resolve(c).rarity)
            .ThenBy(c => c.level)
            .ToList();
    }

    public static int FodderXpValue(OwnedCard fodder)
    {
        var card = Resolve(fodder);
        if (card == null) return 0;
        return CardStats.FodderXp(card.rarity) + (fodder.level - 1) * 20;
    }

    /// <summary>Consumes <paramref name="fodder"/> and gives its XP to <paramref name="target"/>. Returns XP given.</summary>
    public static int Enhance(OwnedCard target, OwnedCard fodder)
    {
        if (target == null || fodder == null || target == fodder) return 0;
        if (target.level >= CardStats.MaxLevel) return 0;
        int xp = FodderXpValue(fodder);
        RemoveCard(fodder);
        AddCardXp(target, xp);
        Save();
        return xp;
    }

    /// <summary>Returns null if <paramref name="target"/> can evolve, otherwise the reason it cannot.</summary>
    public static string CanEvolve(OwnedCard target, out OwnedCard partner)
    {
        partner = null;
        var card = Resolve(target);
        if (card == null) return "Unknown card";
        if (target.evolution >= CardStats.MaxEvolution) return "Already at max evolution";
        if (target.level < CardStats.MaxLevel) return $"Reach Lv {CardStats.MaxLevel} first";
        partner = Data.cards.FirstOrDefault(c => c != target && c.key == target.key && !IsInTeam(c.uid) && !c.favorite);
        if (partner == null) return "Needs a second copy of this card";
        int cost = CardStats.EvolveCost(card.rarity);
        if (Data.gold < cost) return $"Needs {cost} Gold";
        return null;
    }

    public static bool Evolve(OwnedCard target)
    {
        if (CanEvolve(target, out OwnedCard partner) != null) return false;
        var card = Resolve(target);
        Data.gold -= CardStats.EvolveCost(card.rarity);
        RemoveCard(partner);
        target.evolution++;
        target.level = 1;
        target.xp = 0;
        Save();
        return true;
    }
}
