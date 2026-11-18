using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Precision")]
public class Precision : ActiveTalent
{
    public float critRateBonus = 1.16f;
    public float critDamageBonus = 1.25f;

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.critRate = Mathf.FloorToInt(self.critRate * critRateBonus);
        self.critDamage = Mathf.FloorToInt(self.critDamage * critDamageBonus);
        Debug.Log($"<color=cyan>[Precision] +{Mathf.RoundToInt((critRateBonus - 1f) * 100f)}% crit rate, +{Mathf.RoundToInt((critDamageBonus - 1f) * 100f)}% crit damage</color>. Current - Crit Rate: {self.critRate}%, Crit Damage: {self.critDamage}%");
    }
}
