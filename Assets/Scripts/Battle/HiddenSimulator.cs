using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

[Serializable]
public class HiddenSimSettings
{
    /// <summary>Battles per floor; every battle uses a new random player team.</summary>
    public int battlesPerFloor = 100;
    public int seed = 1;
}

public class HiddenSimFloor
{
    public int loc, floor, enemyStat, battles, wins, draws, totalRounds;
    public string anime;
    public CardRarity rarity;
    public string enemies;
    public long hpLeftSum; // % of team HP left on wins
    public string winTeam, loseTeam, winLog, loseLog;
    public float WinRate => battles == 0 ? 0f : wins / (float)battles;
    public float AvgRounds => battles == 0 ? 0f : totalRounds / (float)battles;
    public float AvgHpLeft => wins == 0 ? 0f : hpLeftSum / (float)wins;
    public string Verdict => WinRate >= 0.8f ? "Easy" : WinRate >= 0.5f ? "Fair" : WinRate >= 0.2f ? "Hard" : WinRate > 0f ? "Very hard" : "Wall";
}

public class HiddenSimResult
{
    public HiddenSimSettings settings;
    public readonly List<HiddenSimFloor> floors = new List<HiddenSimFloor>();
    public readonly Dictionary<string, int[]> talents = new Dictionary<string, int[]>(); // games, wins
    public int playerStat, hpMultiplier, battles;
    public float seconds;

    static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
    static string Csv(string s) { s = s ?? ""; return s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s; }

    public string FloorsCsv()
    {
        var sb = new StringBuilder("LOC,Floor,Anime,Rarity,Enemy stat,Enemy team,Battles,Player win %,Draws,Avg rounds,Avg HP left on wins %,Verdict\n");
        foreach (var f in floors)
            sb.Append(f.loc).Append(',').Append(f.floor).Append(',').Append(Csv(f.anime)).Append(',').Append(CardStats.RarityName(f.rarity)).Append(',')
              .Append(f.enemyStat).Append(',').Append(Csv(f.enemies)).Append(',').Append(f.battles).Append(',').Append((f.WinRate * 100f).ToString("0.#", Inv)).Append(',')
              .Append(f.draws).Append(',').Append(f.AvgRounds.ToString("0.0", Inv)).Append(',').Append(f.AvgHpLeft.ToString("0", Inv)).Append(',').Append(f.Verdict).Append('\n');
        return sb.ToString();
    }

    public string TalentsCsv()
    {
        var sb = new StringBuilder("Talent,Type,Battles with it,Win %\n");
        var db = CardDatabase.Instance;
        foreach (var kv in talents.OrderByDescending(k => k.Value[0] == 0 ? 0 : k.Value[1] / (float)k.Value[0]))
        {
            var t = db != null ? db.FindTalent(kv.Key) : null;
            sb.Append(Csv(kv.Key)).Append(',').Append(t != null && t.IsActive ? "Active" : "Passive").Append(',').Append(kv.Value[0]).Append(',')
              .Append((kv.Value[0] == 0 ? 0 : 100f * kv.Value[1] / kv.Value[0]).ToString("0.#", Inv)).Append('\n');
        }
        return sb.ToString();
    }

    public string SamplesCsv()
    {
        var sb = new StringBuilder("LOC,Floor,Winning team,Winning battle log,Losing team,Losing battle log\n");
        foreach (var f in floors)
            sb.Append(f.loc).Append(',').Append(f.floor).Append(',').Append(Csv(f.winTeam)).Append(',').Append(Csv(f.winLog)).Append(',')
              .Append(Csv(f.loseTeam)).Append(',').Append(Csv(f.loseLog)).Append('\n');
        return sb.ToString();
    }

    public string SummaryText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Hidden Location simulation: {battles:N0} battles ({settings.battlesPerFloor} per floor), player cards God with every stat {playerStat} (max Roll + 20 God Orbs), HP x{hpMultiplier}, {seconds:0}s");
        int lastPassable = 0;
        foreach (var f in floors)
        {
            sb.AppendLine($"  LOC {f.loc,2}-{f.floor}  {f.WinRate * 100f,5:0.0}%  {f.Verdict,-9} rounds {f.AvgRounds,4:0.0}  {CardStats.RarityName(f.rarity)} stat {f.enemyStat}  {f.anime}");
            if (f.WinRate >= 0.5f) lastPassable = PlayerProfile.HiddenIndex(f.loc, f.floor);
        }
        return sb.ToString();
    }
}

/// <summary>
/// Runs every Hidden Location floor against random teams of 3 God cards with the best stats a player can reach
/// (Test Mode > HIDDEN LOCATION SIM, bridge "hiddensim"). Each battle draws a new team: random elements,
/// a random active + passive talent on every card.
/// </summary>
public static class HiddenSimulator
{
    static readonly CardElement[] Elements =
        { CardElement.Fire, CardElement.Water, CardElement.Tree, CardElement.Lightning, CardElement.Light, CardElement.Dark, CardElement.Rock };

    public static HiddenSimResult Run(HiddenSimSettings s, Action<int, int> progress = null)
    {
        var e = Steps(s, progress, null);
        object last = null;
        while (e.MoveNext()) last = e.Current;
        return last as HiddenSimResult;
    }

    public static IEnumerator RunAsync(HiddenSimSettings s, Action<int, int> progress, Action<HiddenSimResult> done)
    {
        var e = Steps(s, progress, 0.015f);
        HiddenSimResult result = null;
        while (e.MoveNext())
        {
            if (e.Current is HiddenSimResult r) { result = r; break; }
            yield return null;
        }
        done?.Invoke(result);
    }

    /// <summary>A God card with max Roll on every stat and every God Orb, random element and talents.</summary>
    public static TeamMember MaxGod(System.Random rng, List<Talent> actives, List<Talent> passives, int index)
    {
        var b = new BuildData { hp = GameRules.MaxDeviation(GameRules.MaxTier), atk = GameRules.MaxDeviation(GameRules.MaxTier), def = GameRules.MaxDeviation(GameRules.MaxTier), spd = GameRules.MaxDeviation(GameRules.MaxTier) };
        int g = GameRules.GodOrbMaxStacks;
        var m = new TeamMember
        {
            name = $"God {index + 1}",
            rarity = CardRarity.God,
            element = Elements[rng.Next(Elements.Length)],
            hp = CardStats.HpOf(CardRarity.God, b, g),
            atk = CardStats.AtkOf(CardRarity.God, b, g),
            def = CardStats.DefOf(CardRarity.God, b, g),
            baseSpeed = CardStats.SpdOf(CardRarity.God, b, g),
        };
        if (actives.Count > 0) m.talents.Add(actives[rng.Next(actives.Count)]);
        if (passives.Count > 0) m.talents.Add(passives[rng.Next(passives.Count)]);
        return m;
    }

    static string Describe(List<TeamMember> team) => string.Join(" | ", team.Select(m => $"{m.element} {m.TalentNames}"));

    static IEnumerator Steps(HiddenSimSettings s, Action<int, int> progress, float? frameBudget)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = new HiddenSimResult { settings = s, playerStat = GameRules.MaxStat(GameRules.MaxTier), hpMultiplier = GameRules.HpMultiplier };
        var db = CardDatabase.Instance;
        if (db == null || db.hiddenLocations.Count == 0) { yield return result; yield break; }
        var all = TeamSimulator.AllTalents();
        var actives = all.Where(t => t.IsActive).ToList();
        var passives = all.Where(t => !t.IsActive).ToList();
        var rng = new System.Random(s.seed);
        int per = Mathf.Clamp(s.battlesPerFloor, 1, 10000);
        int totalFloors = db.hiddenLocations.Sum(l => l.floors.Count), doneFloors = 0;
        float t0 = Time.realtimeSinceStartup;
        foreach (var loc in db.hiddenLocations)
            for (int f = 1; f <= loc.floors.Count; f++)
            {
                var cards = loc.floors[f - 1].enemies.Where(c => c != null).ToList();
                var fl = new HiddenSimFloor
                {
                    loc = loc.number, floor = f, anime = loc.anime, rarity = loc.rarity,
                    enemyStat = GameRules.HiddenEnemyStat(loc.rarity, loc.number),
                    enemies = string.Join(" | ", cards.Select(c => $"{c.DisplayName} ({c.element}, {(c.talent != null ? c.talent.DisplayName : "-")}{(c.talent2 != null ? " + " + c.talent2.DisplayName : "")})"))
                };
                for (int k = 0; k < per; k++)
                {
                    var team = new List<TeamMember> { MaxGod(rng, actives, passives, 0), MaxGod(rng, actives, passives, 1), MaxGod(rng, actives, passives, 2) };
                    var p = CardRuntime.ForTeam("Your team", team);
                    var e = CardRuntime.ForTeam("Enemy team", cards.Select(PlayerProfile.EnemyMember).ToList());
                    var engine = new BattleEngine(p, e, s.seed * 7919 + PlayerProfile.HiddenIndex(loc.number, f) * 1000 + k);
                    BattleState st;
                    try { st = engine.RunToEnd(); }
                    catch (Exception ex) { Debug.LogException(ex); continue; }
                    bool win = st == BattleState.PlayerWon;
                    fl.battles++;
                    result.battles++;
                    fl.totalRounds += engine.Round;
                    if (st == BattleState.Draw) fl.draws++;
                    if (win)
                    {
                        fl.wins++;
                        fl.hpLeftSum += Mathf.RoundToInt(100f * Mathf.Max(0, p.health) / Mathf.Max(1, p.maxHealth));
                        if (fl.winTeam == null) { fl.winTeam = Describe(team); fl.winLog = TalentMatrix.Log(engine, 5000); }
                    }
                    else if (fl.loseTeam == null) { fl.loseTeam = Describe(team); fl.loseLog = TalentMatrix.Log(engine, 5000); }
                    foreach (var name in team.SelectMany(m => m.talents).Where(t => t != null).Select(t => t.DisplayName).Distinct())
                    {
                        if (!result.talents.TryGetValue(name, out var arr)) result.talents[name] = arr = new int[2];
                        arr[0]++;
                        if (win) arr[1]++;
                    }
                }
                result.floors.Add(fl);
                progress?.Invoke(++doneFloors, totalFloors);
                if (frameBudget.HasValue && Time.realtimeSinceStartup - t0 > frameBudget.Value) { yield return null; t0 = Time.realtimeSinceStartup; }
            }
        result.seconds = (float)watch.Elapsed.TotalSeconds;
        yield return result;
    }
}
