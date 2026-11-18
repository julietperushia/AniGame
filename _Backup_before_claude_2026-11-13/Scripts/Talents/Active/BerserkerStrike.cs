using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/DoubleEdgedStrike")]
public class DoubleEdgedStrike : ActiveTalent
{
    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int damage = Mathf.RoundToInt(self.TotalAttack * 0.15f);
        
        enemy.TakeDamage(damage);
        self.lastDamageDealt = damage;
        
        int recoil = damage / 4;
        self.TakeDamage(recoil);
        
        Debug.Log($"<color=orange>[DoubleEdgedStrike] {damage} damage dealt, {recoil} recoil damage taken</color>");
    }
}
