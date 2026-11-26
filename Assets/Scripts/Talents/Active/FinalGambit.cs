using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/FinalGambit")]
public class FinalGambit : ActiveTalent
{
    [Tooltip("Part of your CURRENT HP you sacrifice (0.25 = 25%).")]
    public float hpPercent = 0.25f;
    [Tooltip("True damage = the HP you lost x this.")]
    public float multiplier = 2f;

    public override string DefaultName => "Final Gambit";
    public override string DefaultDescription =>
        $"Sacrifice {P(hpPercent)} of your current HP (it never drops you below 1 HP), then deal true damage equal to {Mul(multiplier)} the HP you lost.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int lose = Mathf.Max(1, Mathf.RoundToInt(self.health * hpPercent));
        if (lose >= self.health) lose = self.health - 1;
        if (lose <= 0) return;
        int lost = self.TakeTrueDamage(lose, DamageKind.Recoil);
        enemy.TakeTrueDamage(Mathf.RoundToInt(lost * Scaled(multiplier, self)), DamageKind.True);
    }
}
