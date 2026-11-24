using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Gamble")]
public class Gamble : PassiveTalent
{
    public float strength = 0.6f;

    public override string DefaultName => "Gamble";
    public override string DefaultDescription => $"At the start of battle you gain 2 random talents (1 active and 1 passive) that work at {strength * 100:0}% strength.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        var db = CardDatabase.Instance;
        if (db == null) return;
        var pool = db.talents.Where(t => t != null && !(t is Gamble) && !self.HasTalent(t)).ToList();
        var actives = pool.Where(t => t.IsActive).ToList();
        var passives = pool.Where(t => !t.IsActive).ToList();
        var a = actives.Count > 0 ? actives[BattleRandom.Range(0, actives.Count)] : null;
        var p = passives.Count > 0 ? passives[BattleRandom.Range(0, passives.Count)] : null;
        if (a != null) self.AddTalent(a, strength);
        if (p != null) self.AddTalent(p, strength);
        BattleLog.Passive(self, $"<color=#66BB6A>[Gamble] {self.DisplayName} draws {a?.DisplayName ?? "nothing"} + {p?.DisplayName ?? "nothing"} ({strength * 100:0}%)</color>");
        if (p != null) BattleLog.Current?.InitTalent(self, enemy, p);
        if (a != null) BattleLog.Current?.InitTalent(self, enemy, a);
    }
}
