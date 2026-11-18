using UnityEngine;

/// <summary>Asset text: "At the start of your first turn, increase your LIFESTEAL, restoring HP equal to 18% attack damage your allies deal,
/// as well as increasing all healing effects by 23%. Every turn after that, increase your LIFESTEAL by 5%."
/// (Lifesteal itself is applied by the battle engine after every normal attack.)</summary>
[CreateAssetMenu(menuName = "Talents/Passive/Bloodthirster")]
public class Bloodthirster : PassiveTalent
{
    public int startLifesteal = 18;
    public float healingBonus = 0.23f;
    public float maxHealingBonus = 0.5f;
    public int lifestealPerTurn = 5;
    public int maxLifesteal = 100;

    public override string DefaultName => "Bloodthirster";
    public override string DefaultDescription => $"On your first turn gain {startLifesteal}% Lifesteal and +{healingBonus * 100:0}% healing. Every turn after that, +{lifestealPerTurn}% Lifesteal.";

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        if (!self.GetTalentState("BloodthirsterStarted", false))
        {
            self.SetTalentState("BloodthirsterStarted", true);
            self.lifesteal = Mathf.Min(maxLifesteal, self.lifesteal + Scaled(startLifesteal, self));
            self.healingBonus = Mathf.Min(maxHealingBonus, self.healingBonus + Scaled(healingBonus, self));
            BattleLog.Passive(self, $"<color=#FF6B8B>[Bloodthirster] Lifesteal {self.lifesteal}%, healing +{self.healingBonus * 100:0}%</color>");
        }
        else if (self.lifesteal < maxLifesteal)
        {
            self.lifesteal = Mathf.Min(maxLifesteal, self.lifesteal + Scaled(lifestealPerTurn, self));
        }
    }
}
