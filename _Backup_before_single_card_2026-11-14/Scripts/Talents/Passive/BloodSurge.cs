using UnityEngine;

/// <summary>Anigame: "When your health becomes low, increase the LIFESTEAL of all allies by 45%."</summary>
[CreateAssetMenu(menuName = "Talents/Passive/BloodSurge")]
public class BloodSurge : PassiveTalent
{
    public float threshold = 0.3f;
    public int lifestealBonus = 45;

    public override string DefaultName => "Blood Surge";
    public override string DefaultDescription => $"When your health drops below {threshold * 100:0}%, gain +{lifestealBonus}% Lifesteal for the rest of the battle.";

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        bool applied = self.GetTalentState("BloodSurgeApplied", false);
        if (!applied && self.healthPercent <= threshold)
        {
            self.lifesteal += lifestealBonus;
            self.SetTalentState("BloodSurgeApplied", true);
            BattleLog.Passive(self, $"<color=#FF6B8B>[Blood Surge] {self.DisplayName} Lifesteal +{lifestealBonus}%</color>");
        }
    }
}
