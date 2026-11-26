using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/ElementalStrike")]
public class ElementalStrike : ActiveTalent
{
    [Tooltip("Damage in % of your ATK (0.12 = 12%).")]
    public float atkPercent = 0.12f;

    public override string DefaultName => "Elemental Strike";
    public override string DefaultDescription =>
        $"Deal damage of your element equal to {PctF(atkPercent)} of your ATK. Element advantage applies (more damage against a weak element, less against a strong one).";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        float multiplier = DamageCalculator.ElementMul(self, enemy);
        int damage = Mathf.Max(1, Mathf.RoundToInt(DamageCalculator.FromStat(self.TotalAttack, Scaled(atkPercent, self)) * multiplier));
        BattleLog.Info($"<color=#C78BFF>[Elemental Strike] {self.Element} vs {enemy.Element}: {ElementSystem.Describe(multiplier)}</color>");
        self.lastDamageDealt = enemy.TakeDamage(damage, DamageKind.Skill);
    }
}
