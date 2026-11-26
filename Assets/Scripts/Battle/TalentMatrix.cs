using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

[Serializable]
public class TalentMatrixSettings
{
    public CardRarity rarity = CardRarity.God;
    /// <summary>Battles per pair of talents; sides swap every battle so neither talent always moves first.</summary>
    public int battlesPerPair = 20;
    public CardElement element = CardElement.Neutral;
    public int seed = 1;
    /// <summary>Longest first-battle log kept per pair (characters).</summary>
    public int maxLogChars = 6000;
}

/// <summary>Result of one pair of talents (A = the row talent, B = the column talent).</summary>
public class TalentMatrixPair
{
    public Talent a, b;
    public int aWins, bWins, draws, battles, totalRounds;
    public string firstLog = "";
    /// <summary>A's win rate, draws counting half (0..1).</summary>
    public float RateA => battles == 0 ? 0.5f : (aWins + 0.5f * draws) / battles;
    public float AvgRounds => battles == 0 ? 0f : totalRounds / (float)battles;
}

public class TalentMatrixResult
{
    public TalentMatrixSettings settings;
    public List<Talent> talents = new List<Talent>();
    public readonly Dictionary<(int, int), TalentMatrixPair> pairs = new Dictionary<(int, int), TalentMatrixPair>();
    public int battles;
    public float seconds;
    public int hpMultiplier;

    /// <summary>Win rate of talent i against talent j (0..1), or -1 for i == j.</summary>
    public float Rate(int i, int j)
    {
        if (i == j) return -1f;
        if (pairs.TryGetValue((i, j), out var p)) return p.RateA;
        if (pairs.TryGetValue((j, i), out var q)) return 1f - q.RateA;
        return -1f;
    }

    public TalentMatrixPair Pair(int i, int j) => pairs.TryGetValue((Math.Min(i, j), Math.Max(i, j)), out var p) ? p : null;

    /// <summary>Average win rate of a talent against every other talent.</summary>
    public float Overall(int i)
    {
        float sum = 0f; int n = 0;
        for (int j = 0; j < talents.Count; j++)
        {
            if (j == i) continue;
            float r = Rate(i, j);
            if (r < 0f) continue;
            sum += r; n++;
        }
        return n == 0 ? 0.5f : sum / n;
    }

    public List<int> Ranking() => Enumerable.Range(0, talents.Count).OrderByDescending(Overall).ToList();

    /// <summary>Matrix as CSV: row talent's win rate (%) against each column talent.</summary>
    public string MatrixCsv()
    {
        var sb = new StringBuilder();
        sb.Append("Talent (row vs column)");
        foreach (var t in talents) sb.Append(',').Append(Csv(t.DisplayName));
        sb.AppendLine();
        for (int i = 0; i < talents.Count; i++)
        {
            sb.Append(Csv(talents[i].DisplayName));
            for (int j = 0; j < talents.Count; j++)
            {
                float r = Rate(i, j);
                sb.Append(',').Append(r < 0f ? "" : (r * 100f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>One line per pair: both talents, results and the first battle's log.</summary>
    public string RecordsCsv()
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new StringBuilder("Talent A,Type A,Talent B,Type B,A win %,A wins,B wins,Draws,Battles,Avg rounds,First battle log\n");
        foreach (var p in pairs.Values.OrderBy(p => talents.IndexOf(p.a)).ThenBy(p => talents.IndexOf(p.b)))
            sb.Append(Csv(p.a.DisplayName)).Append(',').Append(p.a.IsActive ? "Active" : "Passive").Append(',')
              .Append(Csv(p.b.DisplayName)).Append(',').Append(p.b.IsActive ? "Active" : "Passive").Append(',')
              .Append((p.RateA * 100f).ToString("0.#", inv)).Append(',').Append(p.aWins).Append(',').Append(p.bWins).Append(',')
              .Append(p.draws).Append(',').Append(p.battles).Append(',').Append(p.AvgRounds.ToString("0.0", inv)).Append(',')
              .Append(Csv(p.firstLog)).AppendLine();
        return sb.ToString();
    }

    /// <summary>One line per talent: overall win rate, best and worst matchup.</summary>
    public string SummaryCsv()
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new StringBuilder("Rank,Talent,Type,Win rate %,Wins over 60%,Losses under 40%,Best matchup,Worst matchup\n");
        int rank = 1;
        foreach (int i in Ranking())
        {
            var t = talents[i];
            int best = -1, worst = -1, strong = 0, weak = 0;
            for (int j = 0; j < talents.Count; j++)
            {
                if (j == i) continue;
                float r = Rate(i, j);
                if (r < 0f) continue;
                if (r > 0.6f) strong++;
                if (r < 0.4f) weak++;
                if (best < 0 || r > Rate(i, best)) best = j;
                if (worst < 0 || r < Rate(i, worst)) worst = j;
            }
            sb.Append(rank++).Append(',').Append(Csv(t.DisplayName)).Append(',').Append(t.IsActive ? "Active" : "Passive").Append(',')
              .Append((Overall(i) * 100f).ToString("0.#", inv)).Append(',').Append(strong).Append(',').Append(weak).Append(',')
              .Append(best >= 0 ? Csv($"{talents[best].DisplayName} ({Rate(i, best) * 100f:0}%)") : "").Append(',')
              .Append(worst >= 0 ? Csv($"{talents[worst].DisplayName} ({Rate(i, worst) * 100f:0}%)") : "").AppendLine();
        }
        return sb.ToString();
    }

    public string SummaryText(int top = 15)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"1v1 talent matrix: {talents.Count} talents, {pairs.Count} pairs, {battles} battles ({settings.battlesPerPair} per pair, {CardStats.RarityName(settings.rarity)}, HP x{hpMultiplier}, {seconds:0.0}s)");
        var rank = Ranking();
        sb.AppendLine("STRONGEST");
        foreach (int i in rank.Take(top)) sb.AppendLine($"  {Overall(i) * 100f,5:0.0}%  {talents[i].DisplayName} [{(talents[i].IsActive ? "A" : "P")}]");
        sb.AppendLine("WEAKEST");
        foreach (int i in rank.Skip(Math.Max(0, rank.Count - top))) sb.AppendLine($"  {Overall(i) * 100f,5:0.0}%  {talents[i].DisplayName} [{(talents[i].IsActive ? "A" : "P")}]");
        return sb.ToString();
    }

    static string Csv(string s)
    {
        s = s ?? "";
        return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}

/// <summary>
/// Every talent against every other talent in 1v1 (Test Mode > 1v1 TALENT MATRIX, Tools > AniGame > 1v1 Talent Matrix).
/// Both cards are the same: chosen rarity, every Roll 0, same element, one talent each. Sides swap every battle.
/// </summary>
public static class TalentMatrix
{
    /// <summary>Writes talent_matrix.csv, talent_pairs.csv (with first battle logs) and talent_ranking.csv. Returns the folder.</summary>
    public static string Export(TalentMatrixResult r, string folder)
    {
        System.IO.Directory.CreateDirectory(folder);
        var utf8 = new UTF8Encoding(true);
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "talent_matrix.csv"), r.MatrixCsv(), utf8);
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "talent_pairs.csv"), r.RecordsCsv(), utf8);
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "talent_ranking.csv"), r.SummaryCsv(), utf8);
        return folder;
    }

    /// <summary>Default export folder: the project's Logs folder in the editor, the save folder in a build.</summary>
    public static string DefaultFolder =>
        Application.isEditor ? System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Logs/TalentMatrix"))
                             : System.IO.Path.Combine(Application.persistentDataPath, "TalentMatrix");

    public static TalentMatrixResult Run(TalentMatrixSettings s, Action<int, int> progress = null)
    {
        var e = Steps(s, progress, null);
        object last = null;
        while (e.MoveNext()) last = e.Current;
        return last as TalentMatrixResult;
    }

    public static IEnumerator RunAsync(TalentMatrixSettings s, Action<int, int> progress, Action<TalentMatrixResult> done)
    {
        var e = Steps(s, progress, 0.015f);
        TalentMatrixResult result = null;
        while (e.MoveNext())
        {
            if (e.Current is TalentMatrixResult r) { result = r; break; }
            yield return null;
        }
        done?.Invoke(result);
    }

    static CardRuntime Make(TalentMatrixSettings s, Talent t, string name)
    {
        var inst = new CardInstance { uid = 1, rarity = s.rarity, build = new BuildData { element = s.element, name = name } };
        var rt = CardRuntime.ForBuild(inst, inst.build);
        rt.talents.Clear();
        rt.talent = null;
        rt.AddTalent(t);
        rt.talent = t;
        return rt;
    }

    static IEnumerator Steps(TalentMatrixSettings s, Action<int, int> progress, float? frameBudget)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = new TalentMatrixResult { settings = s, talents = TeamSimulator.AllTalents(), hpMultiplier = GameRules.HpMultiplier };
        int n = result.talents.Count;
        int totalPairs = n * (n - 1) / 2, donePairs = 0;
        int per = Mathf.Clamp(s.battlesPerPair, 1, 1000);
        float t0 = Time.realtimeSinceStartup;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                var a = result.talents[i];
                var b = result.talents[j];
                var pair = new TalentMatrixPair { a = a, b = b };
                for (int k = 0; k < per; k++)
                {
                    bool aFirst = k % 2 == 0; // A is the player side (wins speed ties) every other battle
                    var ra = Make(s, a, a.DisplayName);
                    var rb = Make(s, b, b.DisplayName);
                    var engine = aFirst ? new BattleEngine(ra, rb, s.seed * 100003 + i * 1009 + j * 31 + k)
                                        : new BattleEngine(rb, ra, s.seed * 100003 + i * 1009 + j * 31 + k);
                    BattleState st;
                    try { st = engine.RunToEnd(); }
                    catch (Exception ex) { Debug.LogException(ex); continue; }
                    bool aWon = aFirst ? st == BattleState.PlayerWon : st == BattleState.EnemyWon;
                    bool bWon = aFirst ? st == BattleState.EnemyWon : st == BattleState.PlayerWon;
                    if (aWon) pair.aWins++; else if (bWon) pair.bWins++; else pair.draws++;
                    pair.battles++;
                    pair.totalRounds += engine.Round;
                    result.battles++;
                    if (k == 0) pair.firstLog = Log(engine, s.maxLogChars);
                }
                result.pairs[(i, j)] = pair;
                progress?.Invoke(++donePairs, totalPairs);
                if (frameBudget.HasValue && Time.realtimeSinceStartup - t0 > frameBudget.Value)
                {
                    yield return null;
                    t0 = Time.realtimeSinceStartup;
                }
            }
        result.seconds = (float)watch.Elapsed.TotalSeconds;
        yield return result;
    }

    /// <summary>The battle as plain lines: "R3 Slayer attacks - Rot took 412 damage".</summary>
    public static string Log(BattleEngine engine, int maxChars)
    {
        var sb = new StringBuilder();
        sb.Append($"{engine.Player.DisplayName} (moves first on ties) vs {engine.Enemy.DisplayName}. Result: ");
        sb.Append(engine.State == BattleState.PlayerWon ? $"{engine.Player.DisplayName} won" : engine.State == BattleState.EnemyWon ? $"{engine.Enemy.DisplayName} won" : "draw");
        sb.Append($" after {engine.Round} rounds.\n");
        foreach (var ev in engine.Events)
        {
            if (string.IsNullOrEmpty(ev.text) || ev.type == BattleEventType.TurnStart) continue;
            string line = Regex.Replace(ev.text, "<.*?>", "").Trim();
            if (line.Length == 0) continue;
            if (ev.type == BattleEventType.RoundStart) { sb.Append(line).Append('\n'); continue; }
            sb.Append("  ").Append(line).Append('\n');
            if (sb.Length > maxChars) { sb.Append("... (log cut)"); break; }
        }
        return sb.ToString().TrimEnd();
    }
}
