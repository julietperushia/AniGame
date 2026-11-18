using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Regeneration")]
public class Regeneration : ActiveTalent
{
    public float healPercent = 0.075f; // 7.5%

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int healAmount = Mathf.RoundToInt(self.maxHealth * healPercent);
        self.Heal(healAmount);
        
        self.regenerationStacks++;
        
        Debug.Log($"<color=green>[Regeneration] +{healAmount} HP, +1 Regen stack (Total: {self.regenerationStacks} stacks)</color>");
    }
}
