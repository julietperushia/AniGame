using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/BalancingStrike")]
public class BalancingStrike : ActiveTalent
{
    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int damage;
        
        if (self.healthPercent < enemy.healthPercent)
        {
            damage = Mathf.RoundToInt(self.maxHealth * 0.15f);
            Debug.Log($"<color=yellow>[BalancingStrike] HP% lower - Dealing {damage} true damage!</color>");
        }
        else
        {
            damage = Mathf.RoundToInt(self.maxHealth * 0.05f);
            Debug.Log($"<color=yellow>[BalancingStrike] HP% higher - Dealing {damage} true damage</color>");
        }
        
        enemy.TakeTrueDamage(damage);
        self.lastDamageDealt = damage;
    }
}
