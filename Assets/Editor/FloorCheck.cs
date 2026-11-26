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
}
