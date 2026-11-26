using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Bloodthirster")]
public class Bloodthirster : PassiveTalent
{
    [Tooltip("Lifesteal gained on your first turn (%).")]
    public int startLifesteal = 18;
    [Tooltip("Healing received bonus gained on your first turn (0.23 = +23%).")]
    public float healingBonus = 0.23f;
    [Tooltip("Healing received bonus can't go above this (0.5 = +50%).")]
    public float maxHealingBonus = 0.5f;
    [Tooltip("Lifesteal gained every turn after the first (%).")]
    public int lifestealPerTurn = 5;
    [Tooltip("Lifesteal can't go above this (%).")]
    public int maxLifesteal = 100;

    public override string DefaultName => "Bloodthirster";
    public override string DefaultDescription =>
        $"On your first turn gain {Pct(startLifesteal)} Lifesteal and +{PctF(healingBonus, maxHealingBonus)} healing received (max +{P(maxHealingBonus)}). " +
        $"Every turn after that, gain +{Pct(lifestealPerTurn)} Lifesteal (max {maxLifesteal}%). Lifesteal heals you that % of the damage your normal attacks deal.";

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
