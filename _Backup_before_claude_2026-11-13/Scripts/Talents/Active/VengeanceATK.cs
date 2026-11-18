using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/VengeanceATK")]
public class VengeanceATK : ActiveTalent
{
    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int damage = Mathf.RoundToInt(enemy.TotalAttack * 0.11f);
        
        enemy.TakeDamage(damage);
        self.lastDamageDealt = damage;
        
        Debug.Log($"<color=red>[Vengeance ATK] {damage} damage (11% of enemy's {enemy.TotalAttack} ATK)</color>");
    }
}
