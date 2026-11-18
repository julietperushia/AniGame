using UnityEngine;

/// <summary>Anigame: "Deals [element] damage based on 10% of your ATK".</summary>
[CreateAssetMenu(menuName = "Talents/Active/ElementalStrike")]
public class ElementalStrike : ActiveTalent
{
    public float atkPercent = 0.12f;

    public override string DefaultName => "Elemental Strike";
    public override string DefaultDescription => $"Deal damage of your element equal to {atkPercent * 100:0}% of your ATK (element advantage applies).";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        float multiplier = ElementSystem.GetElementMultiplier(self.Element, enemy.Element);
        int damage = Mathf.Max(1, Mathf.RoundToInt(DamageCalculator.FromStat(self.TotalAttack, Scaled(atkPercent, self)) * multiplier));
        BattleLog.Info($"<color=#C78BFF>[Elemental Strike] {self.Element} vs {enemy.Element}: {ElementSystem.Describe(multiplier)}</color>");
        self.lastDamageDealt = enemy.TakeDamage(damage, DamageKind.Skill);
    }
}
