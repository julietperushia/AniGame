using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/DoubleEdgedStrike")]
public class DoubleEdgedStrike : ActiveTalent
{
    [Tooltip("Damage in % of your ATK (0.18 = 18%).")]
    public float atkPercent = 0.18f;
    [Tooltip("You take 1/this of the damage dealt as recoil (4 = a quarter).")]
    public int recoilDivisor = 4;

    public override string DefaultName => "Double Edged Strike";
    public override string DefaultDescription =>
        $"Deal damage of your element equal to {PctF(atkPercent)} of your ATK. You take 1/{Mathf.Max(1, recoilDivisor)} of the damage dealt as recoil (true damage). Element advantage applies.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int damage = DamageCalculator.WithElement(DamageCalculator.FromStat(self.TotalAttack, Scaled(atkPercent, self)), self, enemy);
        int dealt = enemy.TakeDamage(damage, DamageKind.Skill);
        self.lastDamageDealt = dealt;
        int recoil = Mathf.Max(1, dealt / Mathf.Max(1, recoilDivisor));
        self.TakeTrueDamage(recoil, DamageKind.Recoil);
    }
}
