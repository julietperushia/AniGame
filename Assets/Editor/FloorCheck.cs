using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Dev tool (bridge command floorcheck[:Talent1,Talent2]): runs the player's slot cards against the campaign floors,
/// headless (no save changes), and reports how often each active talent fires and every turn where the Mana bar
/// was full but no talent was used. With talent names, the slot cards fight with those talents instead.
/// </summary>
public static class FloorCheck
{
    // ---------------------------------------------------------------- floorsim (background job)
    static System.Collections.IEnumerator floorJob;
    static int floorJobDone, floorJobTotal, floorJobReported;

    /// <summary>floorsim[:battlesPerFloor][|fromLoc-toLoc][|max]: starts the floor simulator in the background (the editor stays usable).</summary>
    public static string StartFloorSim(string arg)
    {
        if (floorJob != null) return $"floorsim: already running ({floorJobDone}/{floorJobTotal} talents)";
        var s = new FloorSimSettings();
        var parts = (arg ?? "").Split('|');
        if (parts.Length > 0 && int.TryParse(parts[0], out int per)) s.battlesPerFloor = per;
        if (parts.Length > 1)
        {
            var range = parts[1].Split('-');
            if (range.Length == 2 && int.TryParse(range[0], out int a) && int.TryParse(range[1], out int b)) { s.fromLoc = a; s.toLoc = b; }
        }
        if (parts.Length > 2 && parts[2].Trim().ToLowerInvariant() == "max")
        {
            s.roll = GameRules.MaxDeviation(GameRules.Tier(s.rarity));
            s.godOrbs = GameRules.Tier(s.rarity) >= GameRules.MaxTier ? GameRules.GodOrbMaxStacks : 0;
        }
        floorJobDone = 0; floorJobTotal = 0; floorJobReported = 0;
        floorJob = FloorSim.Steps(s, (d, n) => { floorJobDone = d; floorJobTotal = n; }, 0.2f);
        UnityEditor.EditorApplication.update -= StepFloorSim;
        UnityEditor.EditorApplication.update += StepFloorSim;
        return $"floorsim: started - LOC {s.fromLoc}-{s.toLoc}, {s.battlesPerFloor} battles per floor, card {s.CardText}";
    }

    static void StepFloorSim()
    {
        if (floorJob == null) { UnityEditor.EditorApplication.update -= StepFloorSim; return; }
        bool more;
        try { more = floorJob.MoveNext(); }
        catch (System.Exception ex) { Debug.LogException(ex); AniGameDevBridge.Append("floorsim: failed - " + ex.Message); floorJob = null; return; }
        if (floorJobDone >= floorJobReported + 10) { floorJobReported = floorJobDone; AniGameDevBridge.Append($"floorsim: {floorJobDone}/{floorJobTotal} talents done"); }
        if (more && floorJob.Current is FloorSimResult r)
        {
            string folder = FloorSim.Export(r);
            AniGameDevBridge.Append(r.SummaryText(12) + "  CSVs: " + folder);
            floorJob = null;
            UnityEditor.EditorApplication.update -= StepFloorSim;
        }
        else if (!more) { floorJob = null; UnityEditor.EditorApplication.update -= StepFloorSim; }
    }

    public static string Run(string arg)
    {
        var db = CardDatabase.Instance;
        if (db == null) return "floorcheck: no CardDatabase";
        var sb = new StringBuilder();
        var data = PlayerProfile.Data;
        int highest = PlayerProfile.HighestUnlockedLocation;
        sb.AppendLine($"floorcheck: highest LOC {highest}, HP x{GameRules.HpMultiplier}");
        string firstStuckLog = null;
        var enemyStats = new Dictionary<string, int[]>();
        var enemyLogs = new Dictionary<string, string>();
        string focus = "Celestial";
        int longestRounds = 0; string longestLog = null;
        for (int i = 0; i < data.slots.Count; i++)
        {
            var s = data.slots[i];
            if (!s.unlocked || !s.HasCard) continue;
            var card = PlayerProfile.FindCard(s.cardUid);
            sb.AppendLine($"slot {i + 1}: {card?.rarity} {s.build.element} talents [{s.build.talent}] [{s.build.talent2}]");
            int battles = 0, stuckBattles = 0, stuckTurns = 0;
            var casts = new Dictionary<string, int>();
            for (int loc = 1; loc <= Mathf.Max(1, highest); loc++)
            {
                int floors = db.FloorCount(loc);
                for (int f = 1; f <= floors; f++)
                {
                    var enemyCard = db.GetFloorCard(loc, f);
                    if (enemyCard == null) continue;
                    for (int k = 0; k < 2; k++)
                    {
                        var p = PlayerProfile.SlotRuntime(i);
                        if (p == null) continue;
                        if (!string.IsNullOrEmpty(arg))
                        {
                            p.talents.Clear();
                            p.talent = null;
                            foreach (var n in arg.Split(',')) p.AddTalent(db.FindTalent(n.Trim()));
                        }
                        var e = new CardRuntime(enemyCard);
                        var engine = new BattleEngine(p, e, 1000 + loc * 31 + f * 7 + k);
                        engine.RunToEnd();
                        battles++;
                        int stuck = CountStuck(engine, p, casts);
                        var ecasts = new Dictionary<string, int>();
                        int estuck = CountStuck(engine, e, ecasts);
                        string key = string.Join(" + ", e.talents.Where(t => t != null).Select(t => t.DisplayName));
                        if (!enemyStats.TryGetValue(key, out var es)) enemyStats[key] = es = new int[3];
                        es[0]++; es[1] += estuck; es[2] += ecasts.Values.Sum();
                        if (key.Contains("Celestial Blessing") && engine.Round > longestRounds) { longestRounds = engine.Round; longestLog = $"LOC {loc} F{f} enemy {enemyCard.DisplayName} [{key}] casts {ecasts.Values.Sum()}\n" + TalentMatrix.Log(engine, 12000); }
                        if (estuck > 0 && key.Contains(focus) && !enemyLogs.ContainsKey(key))
                            enemyLogs[key] = $"LOC {loc} F{f} enemy {enemyCard.DisplayName} [{key}]\n" + TalentMatrix.Log(engine, 7000);
                        if (stuck > 0)
                        {
                            stuckBattles++;
                            stuckTurns += stuck;
                            if (firstStuckLog == null)
                                firstStuckLog = $"LOC {loc} F{f} vs {enemyCard.DisplayName} [{string.Join(", ", e.talents.Where(t => t != null).Select(t => t.DisplayName))}]\n" + TalentMatrix.Log(engine, 9000);
                        }
                    }
                }
            }
            sb.AppendLine($"  {battles} battles, casts: {string.Join(", ", casts.Select(kv => $"{kv.Key} {kv.Value}"))}");
            sb.AppendLine($"  full Mana bar but no talent used: {stuckTurns} turns in {stuckBattles} battles");
        }
        sb.AppendLine("Enemy talent sets with full-bar turns and no talent (battles, stuck turns, casts):");
        foreach (var kv in enemyStats.Where(kv => kv.Value[1] > 0).OrderByDescending(kv => kv.Value[1] / (float)kv.Value[0]).Take(25))
            sb.AppendLine($"  {kv.Key}: {kv.Value[0]} battles, {kv.Value[1]} stuck turns, {kv.Value[2]} casts");
        foreach (var kv in enemyStats.Where(kv => kv.Key.Contains(focus)))
            sb.AppendLine($"  [{focus}] {kv.Key}: {kv.Value[0]} battles, {kv.Value[1]} stuck turns, {kv.Value[2]} casts");
        if (longestLog != null) sb.AppendLine("Longest battle vs a Celestial Blessing enemy:\n" + longestLog);
        foreach (var kv in enemyLogs.Take(2)) sb.AppendLine("Enemy stuck log:\n" + kv.Value);
        if (firstStuckLog != null && string.IsNullOrEmpty(arg) == false) sb.AppendLine("First battle with a stuck turn:\n" + firstStuckLog);
        return sb.ToString();
    }

    /// <summary>Player turns that started with a full Mana bar, weren't skipped, weren't silenced, and used no talent.</summary>
    static int CountStuck(BattleEngine engine, CardRuntime p, Dictionary<string, int> casts)
    {
        int stuck = 0;
        var ev = engine.Events;
        bool silent = false;
        string onSilence = p.DisplayName + " is silenced", offSilence = p.DisplayName + " is no longer silenced";
        for (int i = 0; i < ev.Count; i++)
        {
            var e = ev[i];
            if (e.text != null)
            {
                if (e.text.Contains(onSilence)) silent = true;
                if (e.text.Contains(offSilence)) silent = false;
            }
            if (e.type == BattleEventType.SkillActivated && e.actor == p && e.skill != null)
            {
                casts.TryGetValue(e.skill.DisplayName, out int c);
                casts[e.skill.DisplayName] = c + 1;
            }
            if (e.type != BattleEventType.TurnStart || e.actor != p) continue;
            bool full = p.isPlayer ? e.playerMana >= Mathf.Max(100, e.playerMaxMana) : e.enemyMana >= Mathf.Max(100, e.enemyMaxMana);
            if (!full || !p.Actives().Any()) continue;
            bool used = false, skipped = false, silentNow = silent;
            for (int j = i + 1; j < ev.Count && ev[j].type != BattleEventType.TurnStart && ev[j].type != BattleEventType.RoundStart; j++)
            {
                if (ev[j].type == BattleEventType.SkillActivated && ev[j].actor == p) used = true;
                if (ev[j].type == BattleEventType.TurnSkipped && ev[j].actor == p) skipped = true;
                if (ev[j].text != null && ev[j].text.Contains(offSilence)) silentNow = false;
            }
            if (!used && !skipped && !silentNow) stuck++;
        }
        return stuck;
    }

    /// <summary>
    /// Bridge command matrixtry:Talent.field=value;Talent.field=value - runs the 1v1 talent matrix with those numbers changed
    /// in memory only (the assets are not touched or saved; the old values are put back afterwards).
    /// </summary>
    public static string MatrixTry(string arg, int battlesPerPair = 50)
    {
        var db = CardDatabase.Instance;
        if (db == null || string.IsNullOrEmpty(arg)) return "matrixtry: usage matrixtry:Talent.field=value;...";
        var undo = new List<System.Action>();
        var touched = new List<Talent>();
        var sb = new StringBuilder("matrixtry: " + arg + "\n");
        try
        {
            foreach (var part in arg.Split(';'))
            {
                int eq = part.IndexOf('='), dot = part.LastIndexOf('.', eq < 0 ? part.Length - 1 : eq);
                if (eq < 0 || dot < 0) { sb.AppendLine("  bad: " + part); continue; }
                var t = db.FindTalent(part.Substring(0, dot).Trim());
                string fieldName = part.Substring(dot + 1, eq - dot - 1).Trim(), value = part.Substring(eq + 1).Trim();
                var f = t != null ? t.GetType().GetField(fieldName) : null;
                if (f == null) { sb.AppendLine("  not found: " + part); continue; }
                object old = f.GetValue(t);
                object nv = f.FieldType == typeof(bool) ? (object)bool.Parse(value)
                          : f.FieldType == typeof(int) ? (object)int.Parse(value)
                          : (object)float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                f.SetValue(t, nv);
                undo.Add(() => f.SetValue(t, old));
                if (!touched.Contains(t)) touched.Add(t);
                sb.AppendLine($"  {t.DisplayName}.{fieldName}: {old} -> {nv}");
            }
            var r = TalentMatrix.Run(new TalentMatrixSettings { battlesPerPair = battlesPerPair });
            TalentMatrix.Export(r, System.IO.Path.Combine("Logs", "AniGameBridge", "TalentMatrixTry"));
            var rank = r.Ranking();
            foreach (var t in touched)
            {
                int i = r.talents.IndexOf(t);
                if (i >= 0) sb.AppendLine($"  -> {t.DisplayName}: {r.Overall(i) * 100f:0.0}% (rank {rank.IndexOf(i) + 1})");
            }
            sb.Append(r.SummaryText(12));
        }
        finally
        {
            for (int i = undo.Count - 1; i >= 0; i--) undo[i]();
        }
        return sb.ToString();
    }

    /// <summary>
    /// Bridge command bosssim[:battles]: every boss gate against the player's 3 build slots and against random teams of
    /// 3 max-stat God cards (HiddenSimulator.MaxGod). Win rates per gate, to tune the boss stats.
    /// </summary>
    /// <summary>bosssim:battles|hp0,hpStep,atk0,atkStep,def0,defStep,spd0,spdStep - tries a stat ladder in memory (gate k = base + k x step).</summary>
    public static string BossSim(string arg)
    {
        var parts = (arg ?? "").Split('|');
        int battles = parts.Length > 0 && int.TryParse(parts[0], out int b) ? b : 100;
        if (parts.Length < 2) return BossSim(battles);
        var n = parts[1].Split(',').Select(x => int.TryParse(x, out int v) ? v : 0).ToArray();
        if (n.Length < 8) return "bosssim: ladder needs 8 numbers";
        var cfg = BossConfig.Current;
        for (int k = 0; k < cfg.gates.Count; k++)
        {
            var g = cfg.gates[k];
            g.hpPercent = n[0] + k * n[1]; g.atkPercent = n[2] + k * n[3]; g.defPercent = n[4] + k * n[5]; g.spdPercent = n[6] + k * n[7];
        }
        try { return BossSim(battles); }
        finally { BossConfig.Reload(); }
    }

    public static string BossSim(int battles = 100)
    {
        var cfg = BossConfig.Current;
        var all = TeamSimulator.AllTalents();
        var actives = all.Where(t => t.IsActive).ToList();
        var passives = all.Where(t => !t.IsActive).ToList();
        var rng = new System.Random(7);
        bool userReady = PlayerProfile.TeamSlotsProblem() == null;
        var sb = new StringBuilder($"bosssim: {cfg.gates.Count} gates, {battles} battles each, HP x{GameRules.HpMultiplier}, your team {(userReady ? "ready" : "not ready")}\n");
        for (int gate = 1; gate <= cfg.gates.Count; gate++)
        {
            var g = cfg.Gate(gate);
            int userWins = 0, maxWins = 0, userRounds = 0, maxRounds = 0;
            for (int k = 0; k < battles; k++)
            {
                if (userReady)
                {
                    var e1 = PlayerProfile.BossRuntime(gate);
                    var eng1 = new BattleEngine(PlayerProfile.TeamRuntime(false), e1, 1000 + gate * 97 + k);
                    var st1 = eng1.RunToEnd();
                    if (st1 == BattleState.PlayerWon || (st1 == BattleState.Draw && !e1.IsAlive)) userWins++;
                    userRounds += eng1.Round;
                }
                var members = new List<TeamMember>();
                for (int i = 0; i < 3; i++) members.Add(HiddenSimulator.MaxGod(rng, actives, passives, i));
                var e2 = PlayerProfile.BossRuntime(gate);
                var eng2 = new BattleEngine(CardRuntime.ForTeam("Max", members), e2, 5000 + gate * 131 + k);
                var st2 = eng2.RunToEnd();
                if (st2 == BattleState.PlayerWon || (st2 == BattleState.Draw && !e2.IsAlive)) maxWins++;
                maxRounds += eng2.Round;
            }
            sb.AppendLine($"  Gate {gate} {g.name} ({g.rarity}, HP {g.hpPercent}% ATK {g.atkPercent}% DEF {g.defPercent}% SPD {g.spdPercent}% x{g.actionsPerRound}): " +
                          (userReady ? $"your team {100f * userWins / battles:0}% ({userRounds / (float)battles:0.#} rounds), " : "") +
                          $"max God teams {100f * maxWins / battles:0}% ({maxRounds / (float)battles:0.#} rounds)");
        }
        return sb.ToString();
    }
}
