using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/BloodSurge")]
public class BloodSurge : PassiveTalent
{
    public float threshold = 0.3f;
    public int lifestealBonus = 45;

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        bool applied = self.GetTalentState("BloodSurgeApplied", false);
        
        if (!applied && self.healthPercent <= threshold)
        {
            self.lifesteal += lifestealBonus;
            self.SetTalentState("BloodSurgeApplied", true);
        }
    }
}
