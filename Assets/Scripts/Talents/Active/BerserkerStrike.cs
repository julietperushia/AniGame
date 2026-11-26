using UnityEngine;

/// <summary>"Berserk Strike" (CSV name: BerserkStrike): an elemental hit that grows stronger the more HP you are missing.</summary>
[CreateAssetMenu(menuName = "Talents/Active/BerserkStrike")]
public class BerserkerStrike : ActiveTalent
{
    [Tooltip("Damage in % of your ATK (0.12 = 12%).")]
    public float atkPercent = 0.12f;
    [Tooltip("Extra damage at 0 HP (1 = +100%). It grows in step with the HP you are missing.")]
    public float maxRageBonus = 1f;

    public override string DefaultName => "Berserk Strike";
    public override string DefaultDescription =>
        $"Deal damage of your element equal to {PctF(atkPercent)} of your ATK. It grows with the HP you are missing: +{P(maxRageBonus * 0.5f)} at half HP, " +
        $"up to +{P(maxRageBonus)} near 0 HP. Element advantage applies.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        float rage = 1f + Mathf.Clamp01(1f - self.healthPercent) * Mathf.Max(0f, maxRageBonus);
        int damage = Mathf.RoundToInt(DamageCalculator.FromStat(self.TotalAttack, Scaled(atkPercent, self)) * rage);
        damage = DamageCalculator.WithElement(damage, self, enemy);
        BattleLog.Info($"<color=#FF8A3D>[Berserk Strike] Rage x{rage:0.00}</color>");
        self.lastDamageDealt = enemy.TakeDamage(damage, DamageKind.Skill);
    }
}
