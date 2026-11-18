using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Lethal Clarity")]
public class LethalClarity : PassiveTalent
{
    public int critBonus = 12;
    public int atkBonus = 12;
    public int healPercent = 45;

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.critRate += critBonus;
        self.attack += self.attack * atkBonus / 100;
    }

    public override void OnAfterAttack(CardRuntime self, CardRuntime enemy)
    {
        if (self.lastAttackWasCrit)
        {
            self.Heal(self.lastDamageDealt * healPercent / 100);
        }
    }
}
