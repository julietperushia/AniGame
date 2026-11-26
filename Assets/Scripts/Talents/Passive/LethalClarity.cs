using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Lethal Clarity")]
public class LethalClarity : PassiveTalent
{
    [Tooltip("Crit rate gained (% points).")]
    public int critBonus = 12;
    [Tooltip("ATK gained, in % of base ATK.")]
    public int atkBonus = 12;
    [Tooltip("Heal after landing a critical hit, in % of the damage dealt.")]
    public int healPercent = 45;

    public override string DefaultName => "Lethal Clarity";
    public override string DefaultDescription =>
        $"+{Pct(critBonus)} crit rate and +{Pct(atkBonus)} ATK. Whenever you land a critical hit, heal {Pct(healPercent)} of the damage dealt.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.critRate += Scaled(critBonus, self);
        self.attack += self.baseAttack * Scaled(atkBonus, self) / 100;
        BattleLog.Passive(self, $"<color=#FFB23D>[Lethal Clarity] Crit {self.critRate}%, ATK {self.TotalAttack}</color>");
    }

    public override void OnAfterAttack(CardRuntime self, CardRuntime enemy)
    {
        if (self.lastAttackWasCrit && self.lastDamageDealt > 0)
            self.Heal(self.lastDamageDealt * Scaled(healPercent, self) / 100);
    }
}
