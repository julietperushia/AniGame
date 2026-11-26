using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Reversion")]
public class Reversion : PassiveTalent
{
    [Tooltip("Triggers when your HP is below this at the start of your turn (0.2 = 20%).")]
    public float triggerHp = 0.2f;
    [Tooltip("DEF and SPD gained after the reversion, in % of base.")]
    public int bonusPercent = 12;

    public override string DefaultName => "Reversion";
    public override string DefaultDescription =>
        $"The first time your HP is below {P(triggerHp)} at the start of your turn, both sides go back to their base stats and full base HP, " +
        $"then you gain +{Pct(bonusPercent)} DEF and SPD. Once per battle.";

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        if (self.GetTalentState("ReversionUsed", false) || self.healthPercent >= triggerHp) return;
        self.SetTalentState("ReversionUsed", true);
        BattleLog.Passive(self, "<color=#C78BFF>[Reversion] Time rewinds! Both fighters revert to base stats</color>");
        self.ResetToBaseStats();
        enemy.ResetToBaseStats();
        self.defense += self.baseDefense * Scaled(bonusPercent, self) / 100;
        self.speed += self.baseSpeed * Scaled(bonusPercent, self) / 100;
    }
}
