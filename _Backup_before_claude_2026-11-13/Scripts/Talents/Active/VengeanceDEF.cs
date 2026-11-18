using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/VengeanceDEF")]
public class VengeanceDEF : ActiveTalent
{
    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int damage = Mathf.RoundToInt(enemy.TotalDefense * 0.11f);
        
        enemy.TakeDamage(damage);
        self.lastDamageDealt = damage;
        
        Debug.Log($"<color=red>[Vengeance DEF] {damage} damage (11% of enemy's {enemy.TotalDefense} DEF)</color>");
    }
}
