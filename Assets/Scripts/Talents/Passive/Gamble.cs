using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Gamble")]
public class Gamble : PassiveTalent
{
    [Tooltip("Strength of the drawn talents (0.6 = 60%).")]
    public float strength = 0.6f;
    [Tooltip("Random active talents drawn.")]
    public int activeCount = 1;
    [Tooltip("Random passive talents drawn.")]
    public int passiveCount = 1;

    public override string DefaultName => "Gamble";
    public override string DefaultDescription =>
        $"At the start of the battle you draw {activeCount} random active and {passiveCount} random passive talent{(activeCount + passiveCount == 1 ? "" : "s")} " +
        $"that you don't have yet. They work at {P(strength)} strength for this battle.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        var db = CardDatabase.Instance;
        if (db == null) return;
        var pool = db.talents.Where(t => t != null && !(t is Gamble) && !self.HasTalent(t)).ToList();
        var drawn = new System.Collections.Generic.List<Talent>();
        for (int i = 0; i < activeCount; i++) Draw(pool.Where(t => t.IsActive && !drawn.Contains(t)).ToList(), drawn);
        for (int i = 0; i < passiveCount; i++) Draw(pool.Where(t => !t.IsActive && !drawn.Contains(t)).ToList(), drawn);
        foreach (var t in drawn) self.AddTalent(t, strength);
        BattleLog.Passive(self, $"<color=#66BB6A>[Gamble] {self.DisplayName} draws {(drawn.Count == 0 ? "nothing" : string.Join(" + ", drawn.Select(t => t.DisplayName)))} ({strength * 100:0}%)</color>");
        foreach (var t in drawn.Where(t => !t.IsActive)) BattleLog.Current?.InitTalent(self, enemy, t);
        foreach (var t in drawn.Where(t => t.IsActive)) BattleLog.Current?.InitTalent(self, enemy, t);
    }

    static void Draw(System.Collections.Generic.List<Talent> from, System.Collections.Generic.List<Talent> into)
    {
        if (from.Count > 0) into.Add(from[BattleRandom.Range(0, from.Count)]);
    }
}
