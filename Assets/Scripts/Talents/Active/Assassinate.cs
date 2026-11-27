using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Assassinate")]
public class Assassinate : ActiveTalent
{
    [Tooltip("Damage of the hit, as a multiple of a normal attack (1.3 = 130%).")]
    public float atkPercent = 1.3f;

    public override string DefaultName => "Assassinate";
    public override string DefaultDescription =>
        $"Strike once for {PctF(atkPercent)} of a normal attack. The hit is always a critical hit (your crit damage applies) and can't be evaded. " +
        "It is your normal attack for this turn (you don't attack again), so lifesteal and on-hit effects work.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        BattleLog.Current?.PerformAttack(self, enemy, new BattleEngine.AttackOptions { skillHit = true, forceCrit = true, unevadable = true, multiplier = Scaled(atkPercent, self) });
        self.skipAttackThisTurn = true; // the strike was this turn's attack
        self.skipAttackText = "already struck with Assassinate this turn";
    }
}
