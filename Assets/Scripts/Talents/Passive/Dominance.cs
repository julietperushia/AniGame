using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Dominance")]
public class Dominance : PassiveTalent
{
    [Tooltip("ATK bonus while dominant, in % of base ATK.")]
    public int atkBonus = 17;

    const string Key = "DominanceBonus";

    public override string DefaultName => "Dominance";
    public override string DefaultDescription =>
        $"While your HP% is greater than or equal to the enemy's, your ATK is raised by {Pct(atkBonus)} of your base ATK (checked at the start of each of your turns).";

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        int applied = self.GetTalentState(Key, 0);
        bool dominant = self.healthPercent >= enemy.healthPercent;
        if (dominant && applied == 0)
        {
            int bonus = Mathf.Max(1, self.baseAttack * Scaled(atkBonus, self) / 100);
            self.attack += bonus;
            self.SetTalentState(Key, bonus);
            BattleLog.Passive(self, $"<color=#FFB23D>[Dominance] {self.DisplayName} dominates! +{bonus} ATK</color>");
        }
        else if (!dominant && applied > 0)
        {
            self.attack -= applied;
            self.SetTalentState(Key, 0);
            BattleLog.Passive(self, $"[Dominance] {self.DisplayName} lost dominance (-{applied} ATK)");
        }
    }
}
