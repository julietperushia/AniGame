using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>A prize line of a minigame: reach minScore to win the prize (the best line reached pays). Roulette: one wheel segment, picked by weight.</summary>
[Serializable]
public class PrizeTier
{
    public string label = "";
    public int minScore = 1;
    [Tooltip("Roulette only: how likely this segment is (relative to the others).")]
    public int weight = 1;
    public Prize prize = new Prize();

    public PrizeTier() { }
    public PrizeTier(string label, int minScore, Prize prize, int weight = 1) { this.label = label; this.minScore = minScore; this.prize = prize; this.weight = weight; }
}

[Serializable]
public class MinigameDef
{
    public string id;
    public string title;
    public bool enabled = true;
    public List<PrizeTier> tiers = new List<PrizeTier>();
}

/// <summary>Minigames: shared cooldown and the prizes of every game. Edited in Test Mode > MINIGAME PRIZES.</summary>
[Serializable]
public class MinigameConfig
{
    public const string FileName = "minigames";
    public const string Flappy = "flappy", Quiz = "quiz", Slots = "slots", Roulette = "roulette", HigherLower = "higherlower", Memory = "memory";

    [Tooltip("Minutes to wait after starting any minigame before the next one.")]
    public int cooldownMinutes = 5;
    public List<MinigameDef> games = new List<MinigameDef>();

    static MinigameConfig current;
    public static MinigameConfig Current
    {
        get
        {
            if (current == null) { current = JsonConfig.Load(FileName, Defaults); current.AddMissingGames(); }
            return current;
        }
    }
    public static void Reload() => current = null;
    public static string SaveCurrent() => JsonConfig.Save(FileName, Current);

    public MinigameDef Find(string id) => games.FirstOrDefault(g => g != null && g.id == id);

    /// <summary>The best prize line reached with this score (null = no prize).</summary>
    public PrizeTier TierFor(string id, int score)
    {
        var g = Find(id);
        if (g == null) return null;
        return g.tiers.Where(t => t != null && score >= t.minScore).OrderByDescending(t => t.minScore).FirstOrDefault();
    }

    /// <summary>Roulette: a segment index picked by weight.</summary>
    public int PickWeighted(string id)
    {
        var g = Find(id);
        if (g == null || g.tiers.Count == 0) return -1;
        int total = g.tiers.Sum(t => Mathf.Max(0, t.weight));
        if (total <= 0) return UnityEngine.Random.Range(0, g.tiers.Count);
        int roll = UnityEngine.Random.Range(0, total);
        for (int i = 0; i < g.tiers.Count; i++)
        {
            roll -= Mathf.Max(0, g.tiers[i].weight);
            if (roll < 0) return i;
        }
        return g.tiers.Count - 1;
    }

    void AddMissingGames()
    {
        foreach (var d in Defaults().games)
            if (Find(d.id) == null) games.Add(d);
    }

    static Prize G(int n) => new Prize(PrizeType.Gold, n);
    static Prize D(int n) => new Prize(PrizeType.Diamonds, n);

    public static MinigameConfig Defaults()
    {
        var c = new MinigameConfig();
        c.games.Add(new MinigameDef
        {
            id = Flappy, title = "Flappy Hero",
            tiers = { new PrizeTier("3 pipes", 3, G(500)), new PrizeTier("8 pipes", 8, Prize.Pack("rare")), new PrizeTier("15 pipes", 15, Prize.Pack("superrare")), new PrizeTier("25 pipes", 25, Prize.Pack("ultrarare")) }
        });
        c.games.Add(new MinigameDef
        {
            id = Quiz, title = "Anime Quiz",
            tiers = { new PrizeTier("2 right", 2, G(500)), new PrizeTier("3 right", 3, Prize.Pack("common")), new PrizeTier("4 right", 4, Prize.Pack("rare")), new PrizeTier("5 right", 5, Prize.Pack("superrare")) }
        });
        c.games.Add(new MinigameDef
        {
            id = Slots, title = "Lucky 777",
            tiers = { new PrizeTier("Pair", 1, G(300)), new PrizeTier("Three of a kind", 2, Prize.Pack("rare")), new PrizeTier("777 JACKPOT", 3, Prize.Pack("ultrarare")) }
        });
        c.games.Add(new MinigameDef
        {
            id = Roulette, title = "Roulette",
            tiers =
            {
                new PrizeTier("200 Gold", 0, G(200), 6), new PrizeTier("500 Gold", 0, G(500), 6), new PrizeTier("1,000 Gold", 0, G(1000), 4),
                new PrizeTier("5 Diamonds", 0, D(5), 4), new PrizeTier("3 Stamina", 0, new Prize(PrizeType.Stamina, 3), 4),
                new PrizeTier("Common Pack", 0, Prize.Pack("common"), 4), new PrizeTier("Rare Pack", 0, Prize.Pack("rare"), 2),
                new PrizeTier("ULTRA RARE PACK", 0, Prize.Pack("ultrarare"), 1),
            }
        });
        c.games.Add(new MinigameDef
        {
            id = HigherLower, title = "Higher or Lower",
            tiers = { new PrizeTier("3 in a row", 3, G(500)), new PrizeTier("5 in a row", 5, Prize.Pack("common")), new PrizeTier("7 in a row", 7, Prize.Pack("rare")), new PrizeTier("10 in a row", 10, Prize.Pack("ultrarare")) }
        });
        c.games.Add(new MinigameDef
        {
            id = Memory, title = "Memory Match",
            tiers = { new PrizeTier("2 pairs", 2, G(300)), new PrizeTier("4 pairs", 4, Prize.Pack("common")), new PrizeTier("All 6 pairs", 6, Prize.Pack("superrare")) }
        });
        return c;
    }
}
