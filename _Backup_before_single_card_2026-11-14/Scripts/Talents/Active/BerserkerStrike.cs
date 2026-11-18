using UnityEngine;

/// <summary>
/// "Berserk Strike" (CSV name: BerserkStrike). No official text was found in the Anigame database,
/// so this is a custom design: an elemental hit that grows stronger the more HP you are missing.
/// </summary>
[CreateAssetMenu(menuName = "Talents/Active/BerserkStrike")]
public class BerserkerStrike : ActiveTalent
{
    public float atkPercent = 0.12f;

    public override string DefaultName => "Berserk Strike";
    public override string DefaultDescription => $"Deal elemental damage equal to {atkPercent * 100:0}% of your ATK, increased by the % of HP you are missing (up to x2).";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        float rage = 1f + Mathf.Clamp01(1f - self.healthPercent);
        int damage = Mathf.RoundToInt(DamageCalculator.FromStat(self.TotalAttack, atkPercent) * rage);
        damage = DamageCalculator.WithElement(damage, self, enemy);
        BattleLog.Info($"<color=#FF8A3D>[Berserk Strike] Rage x{rage:0.00}</color>");
        self.lastDamageDealt = enemy.TakeDamage(damage, DamageKind.Skill);
    }
}
