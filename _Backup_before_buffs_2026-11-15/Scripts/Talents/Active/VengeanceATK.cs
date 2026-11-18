using UnityEngine;

/// <summary>Anigame: "Deals True damage to all enemy familiars equal to 11% of their ATK".</summary>
[CreateAssetMenu(menuName = "Talents/Active/VengeanceATK")]
public class VengeanceATK : ActiveTalent
{
    public float percent = 0.11f;

    public override string DefaultName => "Vengeance ATK";
    public override string DefaultDescription => $"Deal true damage equal to {percent * 100:0}% of the enemy's ATK.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.lastDamageDealt = enemy.TakeTrueDamage(DamageCalculator.FromStat(enemy.TotalAttack, Scaled(percent, self)), DamageKind.True);
    }
}
