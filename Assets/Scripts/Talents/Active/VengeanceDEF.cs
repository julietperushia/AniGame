using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/VengeanceDEF")]
public class VengeanceDEF : ActiveTalent
{
    [Tooltip("True damage in % of the ENEMY's DEF (0.11 = 11%).")]
    public float percent = 0.11f;

    public override string DefaultName => "Vengeance DEF";
    public override string DefaultDescription => $"Deal true damage equal to {PctF(percent)} of the enemy's DEF: the tankier it is, the harder this hits.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.lastDamageDealt = enemy.TakeTrueDamage(DamageCalculator.FromStat(enemy.TotalDefense, Scaled(percent, self)), DamageKind.True);
    }
}
