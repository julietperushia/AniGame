using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/VengeanceATK")]
public class VengeanceATK : ActiveTalent
{
    [Tooltip("True damage in % of the ENEMY's ATK (0.11 = 11%).")]
    public float percent = 0.11f;

    public override string DefaultName => "Vengeance ATK";
    public override string DefaultDescription => $"Deal true damage equal to {PctF(percent)} of the enemy's ATK: the stronger its ATK, the harder this hits.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.lastDamageDealt = enemy.TakeTrueDamage(DamageCalculator.FromStat(enemy.TotalAttack, Scaled(percent, self)), DamageKind.True);
    }
}
