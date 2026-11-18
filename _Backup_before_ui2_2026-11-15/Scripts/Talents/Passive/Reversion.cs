using UnityEngine;

/// <summary>Asset text: "Once your health drops below 20%, revert the stats and HP of all allied and enemy familiars back to their base stats.
/// Your allied familiars also increase their DEF/SPD by 12% after the reversion."
/// Fixed: "used" was a field on the shared ScriptableObject, so it only ever worked once per play session.</summary>
[CreateAssetMenu(menuName = "Talents/Passive/Reversion")]
public class Reversion : PassiveTalent
{
    public float triggerHp = 0.2f;
    public int bonusPercent = 12;

    public override string DefaultName => "Reversion";
    public override string DefaultDescription => $"Once your health drops below {triggerHp * 100:0}%, revert the stats and HP of both fighters to base, then gain +{bonusPercent}% DEF/SPD. Once per battle.";

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        if (self.GetTalentState("ReversionUsed", false) || self.healthPercent >= triggerHp) return;
        self.SetTalentState("ReversionUsed", true);

        BattleLog.Passive(self, $"<color=#C78BFF>[Reversion] Time rewinds! Both fighters revert to base stats</color>");
        self.ResetToBaseStats();
        enemy.ResetToBaseStats();
        self.defense += self.baseDefense * Scaled(bonusPercent, self) / 100;
        self.speed += self.baseSpeed * Scaled(bonusPercent, self) / 100;
    }
}
