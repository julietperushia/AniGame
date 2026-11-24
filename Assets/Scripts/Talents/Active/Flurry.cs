using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Flurry")]
public class Flurry : ActiveTalent
{
    public int hits = 3;
    public float atkPercent = 0.45f;

    public override string DefaultName => "Flurry";
    public override string DefaultDescription => $"Strikes {hits} times for {atkPercent * 100:0}% of a normal attack each. Every hit can crit on its own and counts as attack damage (evasion, lifesteal and on-hit effects apply).";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        var engine = BattleLog.Current;
        if (engine == null) return;
        for (int i = 0; i < hits && self.IsAlive && enemy.IsAlive; i++)
            engine.PerformAttack(self, enemy, new BattleEngine.AttackOptions { skillHit = true, multiplier = Scaled(atkPercent, self) });
    }
}
