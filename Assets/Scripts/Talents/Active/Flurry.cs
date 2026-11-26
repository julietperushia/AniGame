using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Flurry")]
public class Flurry : ActiveTalent
{
    [Tooltip("Number of hits.")]
    public int hits = 3;
    [Tooltip("Damage of each hit, as a multiple of a normal attack (0.45 = 45%).")]
    public float atkPercent = 0.45f;

    public override string DefaultName => "Flurry";
    public override string DefaultDescription =>
        $"Strike {N(hits, "time")} for {PctF(atkPercent)} of a normal attack each ({PctF(atkPercent * hits)} in total). Every hit can crit and be evaded on its own, " +
        "and counts as a normal attack (lifesteal and on-hit effects apply).";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        var engine = BattleLog.Current;
        if (engine == null) return;
        for (int i = 0; i < hits && self.IsAlive && enemy.IsAlive; i++)
            engine.PerformAttack(self, enemy, new BattleEngine.AttackOptions { skillHit = true, multiplier = Scaled(atkPercent, self) });
    }
}
