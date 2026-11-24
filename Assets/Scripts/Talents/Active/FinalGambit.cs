using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/FinalGambit")]
public class FinalGambit : ActiveTalent
{
    public float hpPercent = 0.25f;
    public float multiplier = 2f;

    public override string DefaultName => "Final Gambit";
    public override string DefaultDescription => $"Sacrifice {hpPercent * 100:0}% of your current HP, then deal true damage equal to {multiplier:0.#}x the HP you lost.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int lose = Mathf.Max(1, Mathf.RoundToInt(self.health * hpPercent));
        if (lose >= self.health) lose = self.health - 1;
        if (lose <= 0) return;
        int lost = self.TakeTrueDamage(lose, DamageKind.Recoil);
        enemy.TakeTrueDamage(Mathf.RoundToInt(lost * Scaled(multiplier, self)), DamageKind.True);
    }
}
