using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Hidden Location (3v3): unlock, team (the 3 build slots), progress and rewards.</summary>
public static partial class PlayerProfile
{
    /// <summary>Cards of Mythic rarity or higher the player owns.</summary>
    public static int HighRarityCards => Data.cards.Count(c => c != null && c.rarity >= GameRules.HiddenUnlockRarity);

    public static bool HiddenUnlocked => HighRarityCards >= GameRules.HiddenUnlockCards || HiddenTestMode;

    const string HiddenTestKey = "AniGame.HiddenTestMode";

    /// <summary>Test Mode only (editor / development builds): Hidden Location open, every floor open, missing slots filled
    /// with max-stat God test cards, no stamina cost, and wins give no rewards or progress. Kept in PlayerPrefs, not in the save.</summary>
    public static bool HiddenTestMode
    {
        get => TestModeAvailable && PlayerPrefs.GetInt(HiddenTestKey, 0) == 1;
        set { PlayerPrefs.SetInt(HiddenTestKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>Global index of a Hidden Location floor: LOC 1 floor 1 = 1 ... LOC 20 floor 3 = 60.</summary>
    public static int HiddenIndex(int location, int floor) => (location - 1) * GameRules.HiddenFloorsPerLocation + floor;

    public static bool IsHiddenCleared(int location, int floor) => Data.hiddenCleared >= HiddenIndex(location, floor);

    /// <summary>Floors are fought in order: the next one after the last cleared floor (or any cleared floor) is open.</summary>
    public static bool IsHiddenOpen(int location, int floor) => HiddenTestMode || HiddenIndex(location, floor) <= Data.hiddenCleared + 1;

    /// <summary>The next floor to beat (LOC, floor), or the last one when everything is cleared.</summary>
    public static (int loc, int floor) NextHiddenFloor()
    {
        int max = GameRules.HiddenLocationCount * GameRules.HiddenFloorsPerLocation;
        int next = Mathf.Clamp(Data.hiddenCleared + 1, 1, max) - 1;
        return (next / GameRules.HiddenFloorsPerLocation + 1, next % GameRules.HiddenFloorsPerLocation + 1);
    }

    /// <summary>Why the team can't fight (null = ready). The team is the 3 build slots.</summary>
    public static string HiddenTeamProblem()
    {
        if (!HiddenUnlocked) return $"Own {GameRules.HiddenUnlockCards} cards of Mythic rarity or higher to enter (you have {HighRarityCards}).";
        if (HiddenTestMode) return null; // missing slots are filled with test cards
        return TeamSlotsProblem();
    }

    /// <summary>Why the 3 build slots can't form a team (null = ready).</summary>
    public static string TeamSlotsProblem()
    {
        for (int i = 0; i < 3; i++)
        {
            var slot = GetSlot(i);
            if (slot == null || !slot.unlocked) return $"Build slot {i + 1} is locked. Unlock all 3 build slots in Collection: they are your team.";
            if (!slot.HasCard || FindCard(slot.cardUid) == null) return $"Build slot {i + 1} is empty. Save a card to every build slot in Collection: they are your team.";
        }
        return null;
    }

    /// <summary>One card of the player's team, built from a build slot.</summary>
    public static TeamMember SlotMember(int index)
    {
        var slot = GetSlot(index);
        if (slot == null || !slot.unlocked || !slot.HasCard) return null;
        var card = FindCard(slot.cardUid);
        if (card == null) return null;
        var rt = CardRuntime.ForBuild(card, slot.build);
        var m = new TeamMember
        {
            name = CardName(card, slot.build),
            rarity = card.rarity,
            element = slot.build.element,
            hp = rt.maxHealth,
            atk = rt.baseAttack,
            def = rt.baseDefense,
            baseSpeed = rt.baseSpeed,
            critRate = rt.critRate,
            critDamage = rt.critDamage,
            evasion = rt.evasion,
            art = Art(card, slot.build),
        };
        foreach (var t in rt.talents) if (t != null) m.talents.Add(t);
        return m;
    }

    /// <summary>The player's 3v3 team (build slots 1-3) with the combat buffs, or null if a slot is missing.</summary>
    public static CardRuntime HiddenTeamRuntime() => TeamRuntime(HiddenTestMode);

    /// <summary>The 3 build slots as a team; with fillWithTestCards, empty slots get max-stat God test cards.</summary>
    public static CardRuntime TeamRuntime(bool fillWithTestCards)
    {
        var members = new List<TeamMember>();
        var rng = new System.Random();
        var all = TeamSimulator.AllTalents();
        for (int i = 0; i < 3; i++)
        {
            var m = SlotMember(i);
            if (m == null && fillWithTestCards)
            {
                m = HiddenSimulator.MaxGod(rng, all.Where(t => t.IsActive).ToList(), all.Where(t => !t.IsActive).ToList(), i);
                m.name = $"Test God {i + 1}";
                m.art = CharacterCard;
            }
            if (m == null) return null;
            members.Add(m);
        }
        var team = CardRuntime.ForTeam("Your team", members);
        ApplyCombatBuffs(team);
        return team;
    }

    /// <summary>An enemy card of the Hidden Location as a team member.</summary>
    public static TeamMember EnemyMember(Card c)
    {
        var rt = new CardRuntime(c);
        var m = new TeamMember
        {
            name = c.DisplayName,
            rarity = c.rarity,
            element = c.element,
            hp = rt.maxHealth,
            atk = rt.baseAttack,
            def = rt.baseDefense,
            baseSpeed = rt.baseSpeed,
            critRate = rt.critRate,
            critDamage = rt.critDamage,
            evasion = rt.evasion,
            art = c,
        };
        foreach (var t in rt.talents) if (t != null) m.talents.Add(t);
        return m;
    }

    public static CardRuntime HiddenEnemyRuntime(int location, int floor)
    {
        var db = CardDatabase.Instance;
        var cards = db != null ? db.GetHiddenFloor(location, floor) : null;
        if (cards == null || cards.Count == 0) return null;
        return CardRuntime.ForTeam("Enemy team", cards.Where(c => c != null).Select(EnemyMember).ToList());
    }

    /// <summary>Win in the Hidden Location: gold (and diamonds on a first clear), progress and stamina.</summary>
    public static BattleRewards ApplyHiddenVictory(int location, int floor)
    {
        var r = new BattleRewards { victory = true };
        if (HiddenTestMode) { r.notes.Add("Test Mode fight: no rewards and no progress."); return r; }
        r.firstClear = !IsHiddenCleared(location, floor);
        if (r.firstClear) Data.hiddenCleared = Mathf.Max(Data.hiddenCleared, HiddenIndex(location, floor));
        r.locationCleared = r.firstClear && floor >= GameRules.HiddenFloorsPerLocation;
        r.gold = EarnGold(GameRules.HiddenGold(location, floor, r.firstClear));
        r.lines.Add($"+{r.gold:N0} Gold");
        if (r.firstClear)
        {
            r.diamonds = GameRules.HiddenDiamonds(location, floor);
            Data.diamonds += r.diamonds;
            r.lines.Add($"+{r.diamonds} Diamonds (first clear)");
            r.stamina += AddStamina(GameRules.StaminaNewFloor);
        }
        else r.notes.Add($"Replay: {GameBalance.Current.hiddenReplayGoldPercent}% gold, no diamonds");
        if (r.locationCleared)
        {
            int before = Data.stamina;
            Data.stamina = Mathf.Max(Data.stamina, MaxStamina);
            Data.staminaTimestamp = Now;
            r.stamina += Data.stamina - before;
            r.notes.Add(location < GameRules.HiddenLocationCount ? $"HIDDEN LOC {location} CLEARED! LOC {location + 1} is open and stamina refilled" : "THE WHOLE HIDDEN LOCATION IS CLEARED!");
        }
        if (r.stamina > 0) r.lines.Add($"+{r.stamina} Stamina");
        Data.battlesWon++;
        Data.hiddenWins++;
        Save();
        return r;
    }
}
