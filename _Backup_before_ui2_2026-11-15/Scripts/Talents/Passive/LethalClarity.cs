using UnityEngine;

/// <summary>Asset text: "Increase your CRIT by 18% and ATK by 12%. Whenever you inflict a CRIT, heal for 50% of the damage dealt."</summary>
[CreateAssetMenu(menuName = "Talents/Passive/Lethal Clarity")]
public class LethalClarity : PassiveTalent
{
    public int critBonus = 12;
    public int atkBonus = 12;
    public int healPercent = 45;

    public override string DefaultName => "Lethal Clarity";
    public override string DefaultDescription => $"+{critBonus}% crit rate and +{atkBonus}% ATK. Whenever you land a critical hit, heal for {healPercent}% of the damage dealt.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.critRate += Scaled(critBonus, self);
        self.attack += self.baseAttack * Scaled(atkBonus, self) / 100;
        BattleLog.Passive(self, $"<color=#FFB23D>[Lethal Clarity] Crit {self.critRate}%, ATK {self.TotalAttack}</color>");
    }

    public override void OnAfterAttack(CardRuntime self, CardRuntime enemy)
    {
        if (self.lastAttackWasCrit && self.lastDamageDealt > 0)
        {
            self.Heal(self.lastDamageDealt * Scaled(healPercent, self) / 100);
        }
    }
}
