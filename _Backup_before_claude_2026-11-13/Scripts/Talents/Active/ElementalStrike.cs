using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/ElementalStrike")]
public class ElementalStrike : ActiveTalent
{
    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        float multiplier = ElementSystem.GetElementMultiplier(self.baseCard.element, enemy.baseCard.element);
        
        int damage = Mathf.RoundToInt(self.TotalAttack * 0.12f * multiplier);
        
        string elementAdvantageSuffix = "";
        if (multiplier > 1.0f)
            elementAdvantageSuffix = " (Advantage!)";
        else if (multiplier < 0.751f && multiplier > 0.749f)
            elementAdvantageSuffix = " (Same Element Resilience)";
        else if (multiplier < 1.0f)
            elementAdvantageSuffix = " (Disadvantage)";

        Debug.Log($"<color=purple>[ElementalStrike] {self.baseCard.element} Strike: {damage} damage to {enemy.baseCard.element} target{elementAdvantageSuffix}</color>");
        
        enemy.TakeDamage(damage);
        self.lastDamageDealt = damage;
    }
}
