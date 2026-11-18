using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Bloodthirster")]
public class Bloodthirster : PassiveTalent
{
    public override void OnAfterAttack(CardRuntime self, CardRuntime enemy)
    {
        if (self.lifesteal <= 0 || self.lastDamageDealt <= 0)
            return;

        int healAmount = self.lastDamageDealt * self.lifesteal / 100;
        self.Heal(healAmount);
    }
}
