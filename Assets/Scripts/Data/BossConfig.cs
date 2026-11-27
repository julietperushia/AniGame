using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>One gate of the Boss location: one boss (a character from the game with its own stats) and its rewards.</summary>
[Serializable]
public class BossGate
{
    [Tooltip("Card key of the character whose name and picture the boss uses (campaign L#F# or Hidden H#F#S#).")]
    public string cardKey = "";
    [Tooltip("Own picture (empty = the character's): gallery:KEY or res:ID (Assets/Resources/Config/Pictures).")]
    public string picture = "";
    public string name = "Boss";
    public CardRarity rarity = CardRarity.God;
    public CardElement element = CardElement.Neutral;
    public string talent = "";
    public string talent2 = "";
    [Tooltip("Stats in % of the best stat a card of this rarity can have. The boss fights a whole team alone, so 300% = three max cards.")]
    public int hpPercent = 450;
    public int atkPercent = 150;
    public int defPercent = 150;
    public int spdPercent = 105;
    [Tooltip("Turns per round (at full, 2/3 and 1/3 of its SPD when 3).")]
    public int actionsPerRound = 3;
    public int critRate = 10;
    public int evasion = 5;
    [Header("Rewards (first clear; replays give the replay %)")]
    public int godOrbs = 1;
    public int materialTier = 8;
    public int materials = 2;
    public int gold;

    public BossGate Clone() => (BossGate)MemberwiseClone();
}

/// <summary>A character made in the boss editor (not in the card database): a name and a picture. Gates using it copy the picture.</summary>
[Serializable]
public class BossCharacter
{
    public string key = "";
    public string name = "New boss";
    public string anime = "";
    public CardElement element = CardElement.Neutral;
    public string picture = "";
    public string GateKey => BossConfig.CustomPrefix + key;
}

/// <summary>The Boss location: one location with many gates, fought in order. Edited in Test Mode > BOSS EDITOR.</summary>
[Serializable]
public class BossConfig
{
    public const string FileName = "bosses";
    public int staminaCost = 10;
    [Tooltip("Replaying a cleared gate gives this % of its rewards (a leftover fraction is a chance for one more).")]
    public int replayRewardPercent = 25;
    public List<BossGate> gates = new List<BossGate>();
    [Tooltip("Characters added in the boss editor (ADD CHARACTER), listed first in the character picker.")]
    public List<BossCharacter> characters = new List<BossCharacter>();
    public const string CustomPrefix = "custom:";

    public BossCharacter FindCharacter(string gateKey)
    {
        if (string.IsNullOrEmpty(gateKey) || !gateKey.StartsWith(CustomPrefix) || characters == null) return null;
        string k = gateKey.Substring(CustomPrefix.Length);
        return characters.FirstOrDefault(c => c != null && c.key == k);
    }

    static BossConfig current;
    public static BossConfig Current
    {
        get
        {
            if (current == null) { current = JsonConfig.Load(FileName, Defaults); current.FixTalentSlots(); }
            return current;
        }
    }
    public static void Reload() => current = null;
    public static string SaveCurrent() => JsonConfig.Save(FileName, Current);

    public BossGate Gate(int number) => number >= 1 && number <= gates.Count ? gates[number - 1] : null;

    static string ActiveOf(Card c) => c == null ? "" : c.talent != null && c.talent.IsActive ? c.talent.DisplayName : c.talent2 != null && c.talent2.IsActive ? c.talent2.DisplayName : "";
    static string PassiveOf(Card c) => c == null ? "" : c.talent2 != null && !c.talent2.IsActive ? c.talent2.DisplayName : c.talent != null && !c.talent.IsActive ? c.talent.DisplayName : "";

    /// <summary>Puts each talent in its slot (active / passive), e.g. after a talent changed type (Silence became a passive).</summary>
    public void FixTalentSlots()
    {
        var db = CardDatabase.Instance;
        if (db == null) return;
        foreach (var g in gates)
        {
            var a = db.FindTalent(g.talent);
            var p = db.FindTalent(g.talent2);
            if (a != null && !a.IsActive && (p == null || p.IsActive)) { g.talent = p != null ? p.DisplayName : ""; g.talent2 = a.DisplayName; }
            else if (p != null && p.IsActive && string.IsNullOrEmpty(g.talent)) { g.talent = p.DisplayName; g.talent2 = ""; }
        }
    }

    /// <summary>Starting gates: the strongest character of every Mythic and God Hidden Location anime, getting stronger gate by gate.</summary>
    public static BossConfig Defaults()
    {
        var cfg = new BossConfig();
        var db = CardDatabase.Instance;
        var picks = new List<Card>();
        if (db != null)
            foreach (var loc in db.hiddenLocations.Where(l => l != null && l.rarity >= CardRarity.Mythic).OrderBy(l => l.number))
            {
                var top = loc.floors.Count > 0 ? loc.floors[loc.floors.Count - 1].enemies.FirstOrDefault(c => c != null) : null;
                if (top != null) picks.Add(top);
            }
        for (int i = 0; i < Mathf.Max(1, picks.Count); i++)
        {
            var c = i < picks.Count ? picks[i] : null;
            cfg.gates.Add(new BossGate
            {
                cardKey = c != null ? c.Key : "",
                name = c != null ? c.DisplayName : "Boss",
                rarity = c != null ? c.rarity : CardRarity.God,
                element = c != null ? c.element : CardElement.Neutral,
                talent = ActiveOf(c),
                talent2 = PassiveOf(c),
                // tuned with the bosssim bridge command (HP x20): max-stat God teams win ~100% of the Mythic gates and ~60-95% of the God gates
                hpPercent = 400 + 50 * i,
                atkPercent = 130 + 10 * i,
                defPercent = 130 + 10 * i,
                spdPercent = 100 + 4 * i,
                actionsPerRound = 3,
                godOrbs = 1 + i / 2,
                materialTier = 8,
                materials = 1 + i,
                gold = 5000 * (i + 1),
            });
        }
        return cfg;
    }
}
