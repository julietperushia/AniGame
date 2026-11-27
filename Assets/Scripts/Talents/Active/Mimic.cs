using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Copies a talent from the enemy side. Priority:
///  1. Nothingness, if any enemy card has it (a trap: copying it costs HP, set on the Nothingness asset);
///  2. an active talent of the strongest enemy card (Mimic casts it itself);
///  3. a passive talent of the strongest enemy card (kept for the rest of the battle).
/// Once Mimic has learned a passive, every later cast heals instead.
/// </summary>
[CreateAssetMenu(menuName = "Talents/Active/Mimic")]
public class Mimic : ActiveTalent
{
    [Tooltip("Heal per cast once Mimic has learned a passive, in % of your max HP (0.10 = 10%).")]
    public float healPercent = 0.10f;

    const string Key = "MimicLearned";

    public override string DefaultName => "Mimic";
    public override string DefaultDescription =>
        "Copy one enemy talent per cast. First choice: Nothingness, if any enemy has it (a trap that costs you HP). " +
        "Next: an active talent of the strongest enemy card, which you cast yourself at once. " +
        "Otherwise: a passive talent of the strongest enemy card, which you keep for the rest of the battle. " +
        $"Once Mimic has learned a passive, every later cast heals you {PctF(healPercent)} of your max HP instead.";

    static int Strength(CardRuntime c) =>
        c.maxHealth / Mathf.Max(1, GameRules.HpMultiplier) + c.TotalAttack + c.TotalDefense + c.TotalSpeed;

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        if (self.GetTalentState(Key, false)) { Heal(self); return; }

        var engine = BattleLog.Current;
        List<CardRuntime> enemies = engine != null ? engine.EnemiesOf(self) : new List<CardRuntime> { enemy };
        enemies = enemies.Where(e => e != null && e.IsAlive).OrderByDescending(Strength).ToList();

        // 1. Nothingness first (from any enemy card)
        var nothing = enemies.SelectMany(e => e.Passives()).FirstOrDefault(p => p is Nothingness);
        if (nothing != null) { Learn(self, enemy, nothing); return; }

        // 2. An active talent of the strongest enemy that has one
        foreach (var e in enemies)
        {
            foreach (var a in e.Actives())
            {
                if (a is Mimic) continue;
                bool can = true;
                try { can = a.CanActivate(self, e); } catch { can = false; }
                if (!can) continue;
                BattleLog.Info($"<color=#CE93D8>[Mimic] {self.DisplayName} copies {e.DisplayName}'s {a.DisplayName}!</color>");
                a.Activate(self, e);
                return;
            }
        }

        // 3. A passive talent of the strongest enemy
        foreach (var e in enemies)
        {
            var p = e.Passives().FirstOrDefault(x => !self.HasTalent(x));
            if (p != null) { Learn(self, enemy, p); return; }
        }
        Heal(self);
    }

    void Learn(CardRuntime self, CardRuntime enemy, Talent passive)
    {
        self.SetTalentState(Key, true);
        self.AddTalent(passive);
        BattleLog.Info($"<color=#CE93D8>[Mimic] {self.DisplayName} learns {passive.DisplayName} for the rest of the battle!</color>");
        BattleLog.Current?.InitTalent(self, enemy, passive); // Nothingness takes its price there (OnLearnedInBattle)
    }

    void Heal(CardRuntime self)
    {
        BattleLog.Info($"<color=#CE93D8>[Mimic] Nothing new to copy: {self.DisplayName} heals instead</color>");
        self.Heal(Mathf.RoundToInt(self.maxHealth * Scaled(healPercent, self)));
    }
}
