using UnityEngine;

/// <summary>Asset text: "When your health drops below 30%, restore the HP of all allied familiars by 20%, raise their DEF by 35%,
/// and decrease all damage taken by 5%."</summary>
[CreateAssetMenu(menuName = "Talents/Passive/Protector")]
public class Protector : PassiveTalent
{
    public float triggerHp = 0.3f;
    public int healPercent = 20;
    public int defBonusPercent = 35;
    public int damageTakenReductionPercent = 5;

    public override string DefaultName => "Protector";
    public override string DefaultDescription => $"When your health drops below {triggerHp * 100:0}%, restore {healPercent}% HP, raise DEF by {defBonusPercent}% and take {damageTakenReductionPercent}% less damage. Once per battle.";

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        bool triggered = self.GetTalentState("ProtectorTriggered", false);
        if (triggered || self.healthPercent >= triggerHp) return;
        self.SetTalentState("ProtectorTriggered", true);

        int defBonus = self.baseDefense * Scaled(defBonusPercent, self) / 100;
        self.defense += defBonus;
        self.damageTakenReduction += Scaled(damageTakenReductionPercent, self) / 100f;
        BattleLog.Passive(self, $"<color=#6FD3FF>[Protector] {self.DisplayName} DEF +{defBonus}, damage taken -{damageTakenReductionPercent}%</color>");
        self.Heal(self.maxHealth * Scaled(healPercent, self) / 100);
    }
}
