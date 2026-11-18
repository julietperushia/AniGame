using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/LifeSap")]
public class LifeSap : PassiveTalent
{
    public int damagePercent = 2;
    public int healMultiplier = 175;

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
            int dmg = enemy.maxHealth * damagePercent / 100;
            enemy.TakeDamage(dmg);
            self.Heal(dmg * healMultiplier / 100);
    }
}
