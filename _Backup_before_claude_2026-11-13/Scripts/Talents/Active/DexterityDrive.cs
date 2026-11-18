using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/DexterityDrive")]
public class DexterityDrive : ActiveTalent
{
    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int baseDamage = Mathf.RoundToInt(self.speed * 0.05f);
        int spdDifference = self.speed - enemy.speed;
        int bonusDamage = Mathf.Max(0, spdDifference);
        int totalDamage = baseDamage + bonusDamage;
        
        enemy.TakeDamage(totalDamage);
        self.lastDamageDealt = totalDamage;
        
        Debug.Log($"<color=cyan>[DexterityDrive] {totalDamage} damage (Base: {baseDamage}, SPD Bonus: {bonusDamage})</color>");
    }
}
