using UnityEngine;

/// <summary>Like Vengeance ATK, but scales with the enemy's DEF.</summary>
[CreateAssetMenu(menuName = "Talents/Active/VengeanceDEF")]
public class VengeanceDEF : ActiveTalent
{
    public float percent = 0.11f;

    public override string DefaultName => "Vengeance DEF";
    public override string DefaultDescription => $"Deal true damage equal to {percent * 100:0}% of the enemy's DEF.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.lastDamageDealt = enemy.TakeTrueDamage(DamageCalculator.FromStat(enemy.TotalDefense, Scaled(percent, self)), DamageKind.True);
    }
}
