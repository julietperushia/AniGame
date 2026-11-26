using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>One of the 6 cards of a 3v3 simulation. Anything left unset is random (rarity unset = God).</summary>
[Serializable]
public class TeamSimSlot
{
    public bool setRarity;
    public CardRarity rarity = CardRarity.God;
    public bool setElement;
    public CardElement element = CardElement.Fire;
    /// <summary>Talent name (God cards: the active). Empty = random.</summary>
    public string talent = "";
    /// <summary>God cards only: the passive. Empty = random.</summary>
    public string talent2 = "";

    public CardRarity Rarity => setRarity ? rarity : CardRarity.God;
    public bool IsGod => GameRules.Tier(Rarity) >= GameRules.MaxTier;
    public bool IsEmpty => !setRarity && !setElement && string.IsNullOrEmpty(talent) && string.IsNullOrEmpty(talent2);
}

[Serializable]
public class TeamSimSettings
{
    public TeamSimSlot[] slots = { new TeamSimSlot(), new TeamSimSlot(), new TeamSimSlot(), new TeamSimSlot(), new TeamSimSlot(), new TeamSimSlot() };
    public int battles = 200;
    /// <summary>true = every stat Roll is 0 (average build), so only rarity / element / talents matter.</summary>
    public bool averageRolls;
    /// <summary>Normal attack damage multiplier in 3v3 (0 = GameBalance.asset value).</summary>
    public float attackScale;
    public int seed = 1;
}

public class TeamSimTalentStat
{
    public string name;
    public bool active;
    public int games, wins, draws, uses;
    public float WinRate => games == 0 ? 0f : (wins + 0.5f * draws) / games;
}

public class TeamSimReport
{
    public int battles, winsA, winsB, draws, maxRounds, minRounds = int.MaxValue, totalRounds, errors;
    public int fasterTeamWins, fasterTeamGames;
    public float hpMultiplier, attackScale;
    public readonly Dictionary<string, TeamSimTalentStat> talents = new Dictionary<string, TeamSimTalentStat>();
    public readonly Dictionary<CardElement, int[]> elements = new Dictionary<CardElement, int[]>(); // games, wins, draws
    public string sampleLog = "";
    public string firstError;
    public float AvgRounds => battles == 0 ? 0f : totalRounds / (float)battles;

    public List<TeamSimTalentStat> SortedTalents(int minGames = 1) =>
        talents.Values.Where(t => t.games >= minGames).OrderByDescending(t => t.WinRate).ThenByDescending(t => t.games).ToList();

    /// <summary>Text summary (results, battle length, talent table, element table).</summary>
    public string Summary(bool includeTalents = true)
    {
        var sb = new StringBuilder();
        float pct(int a, int b) => b == 0 ? 0f : 100f * a / b;
        sb.AppendLine($"3v3 simulation - {battles} battles   (HP x{hpMultiplier:0}, attack scale {attackScale:0.##})");
        sb.AppendLine($"Team A wins {winsA} ({pct(winsA, battles):0.#}%)   Team B wins {winsB} ({pct(winsB, battles):0.#}%)   Draws {draws} ({pct(draws, battles):0.#}%)");
        sb.AppendLine($"Rounds: avg {AvgRounds:0.0}  min {(minRounds == int.MaxValue ? 0 : minRounds)}  max {maxRounds}");
        if (fasterTeamGames > 0) sb.AppendLine($"Faster team (higher total SPD) won {pct(fasterTeamWins, fasterTeamGames):0.#}% of {fasterTeamGames} battles");
        if (errors > 0) sb.AppendLine($"<color=#FF6B6B>{errors} battles threw an error: {firstError}</color>");
        if (includeTalents)
        {
            sb.AppendLine();
            sb.AppendLine("TALENTS  (win rate of teams that had it; draws count half)");
            var list = SortedTalents();
            int minGames = Mathf.Max(3, battles / 50);
            foreach (var t in list)
            {
                string flag = t.games >= minGames ? (t.WinRate >= 0.62f ? "  <color=#FF6B6B>STRONG</color>" : t.WinRate <= 0.38f ? "  <color=#6FA8FF>weak</color>" : "") : "  (few games)";
                string uses = t.active ? $"  used {t.uses / (float)Mathf.Max(1, t.games):0.0}/battle" : "";
                sb.AppendLine($"  {t.WinRate * 100f,5:0.0}%  {t.name} [{(t.active ? "A" : "P")}]  {t.games} games{uses}{flag}");
            }
            if (elements.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("ELEMENTS  (win rate of teams with at least one card of it)");
                foreach (var kv in elements.OrderByDescending(e => e.Value[0] == 0 ? 0 : (e.Value[1] + 0.5f * e.Value[2]) / e.Value[0]))
                    sb.AppendLine($"  {(kv.Value[0] == 0 ? 0 : 100f * (kv.Value[1] + 0.5f * kv.Value[2]) / kv.Value[0]),5:0.0}%  {kv.Key}  {kv.Value[0]} games");
            }
        }
        return sb.ToString();
    }
}

/// <summary>
/// Headless 3v3 battles for balance testing (Test Mode screen and Tools > AniGame > 3v3 Simulator).
/// Each battle rolls new cards for every slot that isn't fully set.
/// </summary>
public static class TeamSimulator
{
    static readonly CardElement[] Elements =
        { CardElement.Fire, CardElement.Water, CardElement.Tree, CardElement.Lightning, CardElement.Light, CardElement.Dark, CardElement.Rock };

    public static List<Talent> AllTalents()
    {
        var db = CardDatabase.Instance;
        return db == null ? new List<Talent>() : db.talents.Where(t => t != null).OrderBy(t => t.DisplayName).ToList();
    }

    public static List<Talent> Actives() => AllTalents().Where(t => t.IsActive).ToList();
    public static List<Talent> Passives() => AllTalents().Where(t => !t.IsActive).ToList();

    static List<string> characterNames;

    static string RandomName(System.Random rng)
    {
        if (characterNames == null)
        {
            characterNames = new List<string>();
            var db = CardDatabase.Instance;
            if (db != null)
                foreach (var loc in db.locations)
                    if (loc != null && loc.floors != null)
                        foreach (var c in loc.floors)
                            if (c != null && !string.IsNullOrEmpty(c.cardName) && !characterNames.Contains(c.cardName)) characterNames.Add(c.cardName);
        }
        return characterNames.Count == 0 ? "Card" : characterNames[rng.Next(characterNames.Count)];
    }

    static Talent Find(string name) => string.IsNullOrEmpty(name) || CardDatabase.Instance == null ? null : CardDatabase.Instance.FindTalent(name);

    static int Roll(System.Random rng, int tier, bool average)
    {
        if (average) return 0;
        int dev = GameRules.MaxDeviation(tier);
        return rng.Next(-dev, dev + 1);
    }

    /// <summary>Builds one card from a slot (random parts rolled with rng).</summary>
    public static TeamMember MakeMember(TeamSimSlot slot, System.Random rng, bool averageRolls, List<Talent> actives, List<Talent> passives, List<Talent> all)
    {
        var rarity = slot.Rarity;
        int tier = GameRules.Tier(rarity);
        var b = new BuildData
        {
            hp = Roll(rng, tier, averageRolls), atk = Roll(rng, tier, averageRolls),
            def = Roll(rng, tier, averageRolls), spd = Roll(rng, tier, averageRolls),
            element = slot.setElement ? slot.element : Elements[rng.Next(Elements.Length)],
        };
        var m = new TeamMember
        {
            name = RandomName(rng),
            rarity = rarity,
            element = b.element,
            hp = CardStats.HpOf(rarity, b),
            atk = CardStats.AtkOf(rarity, b),
            def = CardStats.DefOf(rarity, b),
            baseSpeed = CardStats.SpdOf(rarity, b),
        };
        if (slot.IsGod)
        {
            // God: 1 active + 1 passive
            var a = Find(slot.talent);
            var p = Find(slot.talent2);
            if (a == null && actives.Count > 0) a = actives[rng.Next(actives.Count)];
            if (p == null && passives.Count > 0) p = passives[rng.Next(passives.Count)];
            if (a != null) m.talents.Add(a);
            if (p != null && p != a) m.talents.Add(p);
        }
        else
        {
            var t = Find(slot.talent);
            if (t == null && all.Count > 0) t = all[rng.Next(all.Count)];
            if (t != null) m.talents.Add(t);
        }
        return m;
    }

    /// <summary>Runs every battle at once (editor window, bridge).</summary>
    public static TeamSimReport Run(TeamSimSettings s, Action<int, int> progress = null)
    {
        var e = RunSteps(s, progress, null);
        while (e.MoveNext()) { }
        return (TeamSimReport)e.Current;
    }

    /// <summary>Coroutine version for the in-game screen: yields about every 15 ms so the game keeps drawing.</summary>
    public static IEnumerator RunAsync(TeamSimSettings s, Action<int, int> progress, Action<TeamSimReport> done)
    {
        var e = RunSteps(s, progress, 0.015f);
        TeamSimReport report = null;
        while (e.MoveNext())
        {
            if (e.Current is TeamSimReport r) { report = r; break; }
            yield return null;
        }
        done?.Invoke(report);
    }

    static IEnumerator RunSteps(TeamSimSettings s, Action<int, int> progress, float? frameBudget)
    {
        var report = new TeamSimReport();
        int n = Mathf.Clamp(s.battles, 1, 100000);
        float prevScale = GameRules.TeamAttackScaleOverride;
        GameRules.TeamAttackScaleOverride = s.attackScale > 0f ? s.attackScale : -1f;
        report.hpMultiplier = GameRules.HpMultiplier;
        report.attackScale = GameRules.TeamAttackScale;
        var all = AllTalents();
        var actives = all.Where(t => t.IsActive).ToList();
        var passives = all.Where(t => !t.IsActive).ToList();
        var rng = new System.Random(s.seed);
        float t0 = Time.realtimeSinceStartup;
        try
        {
            for (int i = 0; i < n; i++)
            {
                var a = new List<TeamMember>();
                var b = new List<TeamMember>();
                for (int k = 0; k < 3; k++) a.Add(MakeMember(s.slots[k], rng, s.averageRolls, actives, passives, all));
                for (int k = 3; k < 6; k++) b.Add(MakeMember(s.slots[k], rng, s.averageRolls, actives, passives, all));
                RunOne(a, b, s.seed * 7919 + i, report, i == 0);
                progress?.Invoke(i + 1, n);
                if (frameBudget.HasValue && Time.realtimeSinceStartup - t0 > frameBudget.Value)
                {
                    yield return null;
                    t0 = Time.realtimeSinceStartup;
                }
            }
        }
        finally
        {
            GameRules.TeamAttackScaleOverride = prevScale;
        }
        yield return report;
    }

    static void RunOne(List<TeamMember> a, List<TeamMember> b, int seed, TeamSimReport report, bool keepLog)
    {
        var teamA = CardRuntime.ForTeam("Team A", a);
        var teamB = CardRuntime.ForTeam("Team B", b);
        var engine = new BattleEngine(teamA, teamB, seed);
        BattleState st;
        try { st = engine.RunToEnd(); }
        catch (Exception ex)
        {
            report.errors++;
            if (report.firstError == null) report.firstError = ex.GetType().Name + ": " + ex.Message;
            Debug.LogException(ex);
            return;
        }
        report.battles++;
        if (st == BattleState.PlayerWon) report.winsA++;
        else if (st == BattleState.EnemyWon) report.winsB++;
        else report.draws++;
        report.totalRounds += engine.Round;
        report.maxRounds = Mathf.Max(report.maxRounds, engine.Round);
        report.minRounds = Mathf.Min(report.minRounds, engine.Round);

        int spdA = a.Sum(m => m.baseSpeed), spdB = b.Sum(m => m.baseSpeed);
        if (spdA != spdB && st != BattleState.Draw)
        {
            report.fasterTeamGames++;
            if ((spdA > spdB) == (st == BattleState.PlayerWon)) report.fasterTeamWins++;
        }

        // Skill uses per talent name and team
        var uses = new Dictionary<(CardRuntime, string), int>();
        foreach (var ev in engine.Events)
            if (ev.type == BattleEventType.SkillActivated && ev.skill != null && ev.actor != null)
            {
                var key = (ev.actor, ev.skill.DisplayName);
                uses.TryGetValue(key, out int u);
                uses[key] = u + 1;
            }

        void Count(CardRuntime team, List<TeamMember> cards, bool won, bool draw)
        {
            var seen = new HashSet<string>();
            foreach (var m in cards)
                foreach (var t in m.talents)
                {
                    if (t == null || !seen.Add(t.DisplayName)) continue;
                    if (!report.talents.TryGetValue(t.DisplayName, out var stat))
                        report.talents[t.DisplayName] = stat = new TeamSimTalentStat { name = t.DisplayName, active = t.IsActive };
                    stat.games++;
                    if (won) stat.wins++;
                    if (draw) stat.draws++;
                    if (uses.TryGetValue((team, t.DisplayName), out int u)) stat.uses += u;
                }
            var els = new HashSet<CardElement>(cards.Select(m => m.element));
            foreach (var el in els)
            {
                if (!report.elements.TryGetValue(el, out var arr)) report.elements[el] = arr = new int[3];
                arr[0]++;
                if (won) arr[1]++;
                if (draw) arr[2]++;
            }
        }
        bool isDraw = st == BattleState.Draw;
        Count(teamA, a, st == BattleState.PlayerWon, isDraw);
        Count(teamB, b, st == BattleState.EnemyWon, isDraw);

        if (keepLog) report.sampleLog = SampleLog(engine, a, b);
    }

    static string Describe(TeamMember m) =>
        $"{m.name} - {CardStats.RarityName(m.rarity)} {m.element}  HP {m.hp} ATK {m.atk} DEF {m.def} SPD {m.baseSpeed}  [{m.TalentNames}]";

    static string SampleLog(BattleEngine engine, List<TeamMember> a, List<TeamMember> b)
    {
        var sb = new StringBuilder();
        sb.AppendLine("FIRST BATTLE");
        sb.AppendLine("Team A:");
        foreach (var m in a) sb.AppendLine("  " + Describe(m));
        sb.AppendLine("Team B:");
        foreach (var m in b) sb.AppendLine("  " + Describe(m));
        sb.AppendLine($"Result: {engine.State} after {engine.Round} rounds  (Team A HP {Mathf.Max(0, engine.Player.health)}/{engine.Player.maxHealth}, Team B HP {Mathf.Max(0, engine.Enemy.health)}/{engine.Enemy.maxHealth})");
        sb.AppendLine();
        int lines = 0;
        foreach (var ev in engine.Events)
        {
            if (string.IsNullOrEmpty(ev.text)) continue;
            string line = Regex.Replace(ev.text, "<.*?>", "");
            if (ev.type == BattleEventType.TurnStart) line = "> " + line;
            else if (ev.type != BattleEventType.RoundStart) line = "    " + line;
            sb.AppendLine(line);
            if (++lines >= 600) { sb.AppendLine("... (log cut)"); break; }
        }
        return sb.ToString();
    }
}
