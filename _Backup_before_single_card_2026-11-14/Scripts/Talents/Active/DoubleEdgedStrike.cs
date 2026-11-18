using UnityEngine;

/// <summary>Anigame: "Deals [element] damage equal to 15% of your ATK, and you take 1/4 of the damage dealt."</summary>
[CreateAssetMenu(menuName = "Talents/Active/DoubleEdgedStrike")]
public class DoubleEdgedStrike : ActiveTalent
{
    public float atkPercent = 0.18f;
    public int recoilDivisor = 4;

    public override string DefaultName => "Double Edged Strike";
    public override string DefaultDescription => $"Deal elemental damage equal to {atkPercent * 100:0}% of your ATK. You take 1/{recoilDivisor} of the damage dealt as recoil.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int damage = DamageCalculator.WithElement(DamageCalculator.FromStat(self.TotalAttack, atkPercent), self, enemy);
        int dealt = enemy.TakeDamage(damage, DamageKind.Skill);
        self.lastDamageDealt = dealt;
        int recoil = Mathf.Max(1, dealt / Mathf.Max(1, recoilDivisor));
        self.TakeTrueDamage(recoil, DamageKind.Recoil);
    }
}
