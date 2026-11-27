using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[Serializable]
public class FloorSimSettings
{
    public CardRarity rarity = CardRarity.God;
    public CardElement element = CardElement.Neutral;
    /// <summary>Roll on every stat (0 = the 1v1 matrix card; the max Roll is GameRules.MaxDeviation).</summary>
    public int roll;
    public int godOrbs;
    public int fromLoc = 60, toLoc = 100;
    public int battlesPerFloor = 6;
    public int seed = 1;
    public int maxLogChars = 7000;
    public string CardText => $"{CardStats.RarityName(rarity)}, Roll {(roll >= 0 ? "+" : "")}{roll}, {godOrbs} God Orbs, {ElementSystem.DisplayName(element)}, HP x{GameRules.HpMultiplier}, no buffs";
}

/// <summary>One campaign floor in the floor simulator.</summary>
public class FloorSimFloor
{
    public int loc, floor;
    public string enemy, enemyTalents, enemyElement;
    public int hp, atk, def, spd;
    public string Name => $"LOC {loc} F{floor}";
}

/// <summary>One talent's run through the floors.</summary>
public class FloorSimTalent
{
    public Talent talent;
    public int[] wins;          // per floor (same order as FloorSimResult.floors)
    public int battles, totalWins, totalRounds;
    public float hpLeftOnWins;  // sum of HP fraction left after wins
    public string worstLog = "";
    public int worstFloor = -1;
    public float Rate => battles == 0 ? 0f : totalWins / (float)battles;
}

/// <summary>
/// Floor simulator: every talent (alone, on the 1v1 matrix card by default) fights every campaign floor of a LOC range,
/// several times each. Shows which talents can't clear the late floors and which floors stop everyone.
/// Headless: no save changes. Bridge command floorsim, CSVs in Logs/AniGameBridge/FloorSim.
/// </summary>
public class FloorSimResult
{
    public FloorSimSettings settings;
    public readonly List<FloorSimFloor> floors = new List<FloorSimFloor>();
    public readonly List<FloorSimTalent> talents = new List<FloorSimTalent>();
    public int battles;
    public float seconds;
    public int cardHp, cardStat;

    public float FloorRate(FloorSimTalent t, int f) => t.wins[f] / (float)Mathf.Max(1, settings.battlesPerFloor);
    public int Cleared(FloorSimTalent t) => Enumerable.Range(0, floors.Count).Count(f => FloorRate(t, f) >= 0.5f);
    public int NeverWon(FloorSimTalent t) => Enumerable.Range(0, floors.Count).Count(f => t.wins[f] == 0);
    public int FirstLost(FloorSimTalent t) { for (int f = 0; f < floors.Count; f++) if (FloorRate(t, f) < 0.5f) return f; return -1; }
    public int StreakFromStart(FloorSimTalent t) { int f = 0; while (f < floors.Count && FloorRate(t, f) >= 0.5f) f++; return f; }

    public float LocRate(FloorSimTalent t, int loc)
    {
        int w = 0, n = 0;
        for (int f = 0; f < floors.Count; f++) if (floors[f].loc == loc) { w += t.wins[f]; n += settings.battlesPerFloor; }
        return n == 0 ? 0f : w / (float)n;
    }

    public List<FloorSimTalent> Ranking() => talents.OrderByDescending(Cleared).ThenByDescending(t => t.Rate).ToList();

    static string Q(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
    static string Pct(float r) => (r * 100f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

    public string SummaryCsv()
    {
        var sb = new StringBuilder("Rank,Talent,Type,Win %,Floors cleared (>=50%),Floors lost (<50%),Floors never won,Floors,Cleared in a row from LOC " + settings.fromLoc + ",First lost floor,Avg HP left on wins %,Avg rounds,Worst LOC,Worst LOC win %\n");
        int rank = 0;
        foreach (var t in Ranking())
        {
            rank++;
            int first = FirstLost(t), cleared = Cleared(t);
            int worstLoc = settings.fromLoc; float worst = 2f;
            for (int l = settings.fromLoc; l <= settings.toLoc; l++) { float r = LocRate(t, l); if (r < worst) { worst = r; worstLoc = l; } }
            sb.Append($"{rank},{Q(t.talent.DisplayName)},{(t.talent.IsActive ? "Active" : "Passive")},{Pct(t.Rate)},{cleared},{floors.Count - cleared},{NeverWon(t)},{floors.Count},{StreakFromStart(t)},");
            sb.Append($"{Q(first < 0 ? "none" : floors[first].Name)},{Pct(t.totalWins == 0 ? 0f : t.hpLeftOnWins / t.totalWins)},{(t.battles == 0 ? 0 : t.totalRounds / (float)t.battles).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)},{worstLoc},{Pct(worst)}\n");
        }
        return sb.ToString();
    }

    public string ByLocCsv()
    {
        var sb = new StringBuilder("Talent,Type,All");
        for (int l = settings.fromLoc; l <= settings.toLoc; l++) sb.Append(",LOC " + l);
        sb.Append('\n');
        foreach (var t in Ranking())
        {
            sb.Append($"{Q(t.talent.DisplayName)},{(t.talent.IsActive ? "Active" : "Passive")},{Pct(t.Rate)}");
            for (int l = settings.fromLoc; l <= settings.toLoc; l++) sb.Append(',').Append(Pct(LocRate(t, l)));
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public string FloorsCsv()
    {
        var sb = new StringBuilder("LOC,Floor,Enemy,Enemy talents,Element,HP,ATK,DEF,SPD,Avg win % (all talents),Talents that clear (>=50%),Best talent,Best win %\n");
        for (int f = 0; f < floors.Count; f++)
        {
            var fl = floors[f];
            float sum = 0f; int clear = 0; FloorSimTalent best = null; float bestR = -1f;
            foreach (var t in talents) { float r = FloorRate(t, f); sum += r; if (r >= 0.5f) clear++; if (r > bestR) { bestR = r; best = t; } }
            sb.Append($"{fl.loc},{fl.floor},{Q(fl.enemy)},{Q(fl.enemyTalents)},{fl.enemyElement},{fl.hp},{fl.atk},{fl.def},{fl.spd},{Pct(sum / Mathf.Max(1, talents.Count))},{clear},{Q(best?.talent.DisplayName)},{Pct(Mathf.Max(0f, bestR))}\n");
        }
        return sb.ToString();
    }

    public string LostCsv()
    {
        var sb = new StringBuilder("Talent,LOC,Floor,Enemy,Enemy talents,Win %\n");
        foreach (var t in Ranking())
            for (int f = 0; f < floors.Count; f++)
                if (FloorRate(t, f) < 0.5f)
                    sb.Append($"{Q(t.talent.DisplayName)},{floors[f].loc},{floors[f].floor},{Q(floors[f].enemy)},{Q(floors[f].enemyTalents)},{Pct(FloorRate(t, f))}\n");
        return sb.ToString();
    }

    public string LogsCsv()
    {
        var sb = new StringBuilder("Talent,Floor,Enemy talents,Win %,First battle log\n");
        foreach (var t in Ranking())
        {
            if (t.worstFloor < 0) continue;
            var fl = floors[t.worstFloor];
            sb.Append($"{Q(t.talent.DisplayName)},{Q(fl.Name + " " + fl.enemy)},{Q(fl.enemyTalents)},{Pct(FloorRate(t, t.worstFloor))},{Q(t.worstLog)}\n");
        }
        return sb.ToString();
    }

    public string SummaryText(int top = 10)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"floorsim: {talents.Count} talents x {floors.Count} floors (LOC {settings.fromLoc}-{settings.toLoc}) x {settings.battlesPerFloor} = {battles} battles in {seconds:0.0}s");
        sb.AppendLine($"  card: {settings.CardText} -> HP {cardHp}, ATK/DEF/SPD {cardStat}");
        var rk = Ranking();
        sb.AppendLine("  most floors cleared:");
        foreach (var t in rk.Take(top)) sb.AppendLine($"    {t.talent.DisplayName}: {Cleared(t)}/{floors.Count} cleared, win {Pct(t.Rate)}%, first loss {(FirstLost(t) < 0 ? "none" : floors[FirstLost(t)].Name)}");
        sb.AppendLine("  fewest floors cleared:");
        foreach (var t in rk.Skip(Mathf.Max(0, rk.Count - top))) sb.AppendLine($"    {t.talent.DisplayName}: {Cleared(t)}/{floors.Count} cleared, win {Pct(t.Rate)}%, first loss {(FirstLost(t) < 0 ? "none" : floors[FirstLost(t)].Name)}");
        int nobody = Enumerable.Range(0, floors.Count).Count(f => talents.All(t => FloorRate(t, f) < 0.5f));
        sb.AppendLine($"  floors no talent clears: {nobody}/{floors.Count}");
        return sb.ToString();
    }
}

public static class FloorSim
{
    public static string DefaultFolder =>
        Application.isEditor ? System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Logs/AniGameBridge/FloorSim"))
                             : System.IO.Path.Combine(Application.persistentDataPath, "FloorSim");

    public static string Export(FloorSimResult r, string folder = null)
    {
        folder = folder ?? DefaultFolder;
        System.IO.Directory.CreateDirectory(folder);
        var utf8 = new UTF8Encoding(true);
        void W(string name, string text) => System.IO.File.WriteAllText(System.IO.Path.Combine(folder, name), text, utf8);
        W("floorsim_summary.csv", r.SummaryCsv());
        W("floorsim_by_loc.csv", r.ByLocCsv());
        W("floorsim_floors.csv", r.FloorsCsv());
        W("floorsim_lost_floors.csv", r.LostCsv());
        W("floorsim_logs.csv", r.LogsCsv());
        return folder;
    }

    static CardRuntime Make(FloorSimSettings s, Talent t)
    {
        var inst = new CardInstance
        {
            uid = 1, rarity = s.rarity, godBonus = s.godOrbs,
            build = new BuildData { hp = s.roll, atk = s.roll, def = s.roll, spd = s.roll, element = s.element, name = t != null ? t.DisplayName : "No talent" }
        };
        var rt = CardRuntime.ForBuild(inst, inst.build);
        rt.talents.Clear();
        rt.talent = null;
        if (t != null) { rt.AddTalent(t); rt.talent = t; }
        return rt;
    }

    public static FloorSimResult Run(FloorSimSettings s, Action<int, int> progress = null)
    {
        var e = Steps(s, progress, null);
        object last = null;
        while (e.MoveNext()) last = e.Current;
        return last as FloorSimResult;
    }

    /// <summary>Runs in steps (frameBudget seconds of work per step) and yields the result last.</summary>
    public static IEnumerator Steps(FloorSimSettings s, Action<int, int> progress, float? frameBudget)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var db = CardDatabase.Instance;
        var r = new FloorSimResult { settings = s };
        if (db == null) { yield return r; yield break; }
        for (int loc = Mathf.Max(1, s.fromLoc); loc <= s.toLoc; loc++)
        {
            int n = db.FloorCount(loc);
            for (int f = 1; f <= n; f++)
            {
                var c = db.GetFloorCard(loc, f);
                if (c == null) continue;
                var probe = new CardRuntime(c);
                r.floors.Add(new FloorSimFloor
                {
                    loc = loc, floor = f, enemy = c.DisplayName, enemyElement = ElementSystem.DisplayName(c.element),
                    enemyTalents = string.Join(" + ", probe.talents.Where(t => t != null).Select(t => t.DisplayName)),
                    hp = probe.maxHealth, atk = probe.baseAttack, def = probe.baseDefense, spd = probe.baseSpeed,
                });
            }
        }
        var all = TeamSimulator.AllTalents();
        var sample = Make(s, null);
        r.cardHp = sample.maxHealth; r.cardStat = sample.baseAttack;
        int per = Mathf.Clamp(s.battlesPerFloor, 1, 200);
        float t0 = Time.realtimeSinceStartup;
        for (int i = 0; i < all.Count; i++)
        {
            var st = new FloorSimTalent { talent = all[i], wins = new int[r.floors.Count] };
            float worstRate = 2f;
            for (int f = 0; f < r.floors.Count; f++)
            {
                var fl = r.floors[f];
                var enemyCard = db.GetFloorCard(fl.loc, fl.floor);
                string firstLog = null;
                for (int k = 0; k < per; k++)
                {
                    var p = Make(s, all[i]);
                    var e = new CardRuntime(enemyCard);
                    var engine = new BattleEngine(p, e, s.seed * 7919 + i * 100003 + fl.loc * 1009 + fl.floor * 31 + k);
                    BattleState res;
                    try { res = engine.RunToEnd(); }
                    catch (Exception ex) { Debug.LogException(ex); continue; }
                    st.battles++; r.battles++;
                    st.totalRounds += engine.Round;
                    if (res == BattleState.PlayerWon)
                    {
                        st.wins[f]++; st.totalWins++;
                        st.hpLeftOnWins += p.health / (float)Mathf.Max(1, p.maxHealth);
                    }
                    if (k == 0) firstLog = TalentMatrix.Log(engine, s.maxLogChars);
                }
                float rate = st.wins[f] / (float)per;
                // keep the log of the first floor with the lowest win rate (where the talent struggles most, earliest)
                if (rate < worstRate) { worstRate = rate; st.worstFloor = f; st.worstLog = firstLog ?? ""; }
                if (frameBudget.HasValue && Time.realtimeSinceStartup - t0 > frameBudget.Value)
                {
                    yield return null;
                    t0 = Time.realtimeSinceStartup;
                }
            }
            r.talents.Add(st);
            progress?.Invoke(i + 1, all.Count);
        }
        r.seconds = (float)watch.Elapsed.TotalSeconds;
        yield return r;
    }
}
