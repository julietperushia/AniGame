using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Protector")]
public class Protector : PassiveTalent
{
    [Tooltip("Triggers when your HP is below this at the start of your turn (0.3 = 30%).")]
    public float triggerHp = 0.3f;
    [Tooltip("Heal, in % of your max HP.")]
    public int healPercent = 20;
    [Tooltip("DEF gained, in % of base DEF.")]
    public int defBonusPercent = 35;
    [Tooltip("Less damage taken for the rest of the battle (%).")]
    public int damageTakenReductionPercent = 5;

    public override string DefaultName => "Protector";
    public override string DefaultDescription =>
        $"The first time your HP is below {P(triggerHp)} at the start of your turn: heal {Pct(healPercent)} of your max HP, raise your DEF by {Pct(defBonusPercent)} of base DEF " +
        $"and take {Pct(damageTakenReductionPercent)} less damage for the rest of the battle. Once per battle.";

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
