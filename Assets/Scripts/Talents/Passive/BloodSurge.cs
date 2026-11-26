using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/BloodSurge")]
public class BloodSurge : PassiveTalent
{
    [Tooltip("Triggers when your HP is at or below this at the start of your turn (0.3 = 30%).")]
    public float threshold = 0.3f;
    [Tooltip("Lifesteal gained (% of your normal attack damage healed back).")]
    public int lifestealBonus = 45;

    const string Key = "BloodSurgeApplied";

    public override string DefaultName => "Blood Surge";
    public override string DefaultDescription =>
        $"The first time your HP is at or below {P(threshold)} at the start of your turn, gain +{Pct(lifestealBonus)} Lifesteal for the rest of the battle " +
        "(you heal that % of the damage your normal attacks deal).";

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        if (self.GetTalentState(Key, false) || self.healthPercent > threshold) return;
        self.lifesteal += Scaled(lifestealBonus, self);
        self.SetTalentState(Key, true);
        BattleLog.Passive(self, $"<color=#FF6B8B>[Blood Surge] {self.DisplayName} Lifesteal +{lifestealBonus}%</color>");
    }
}
