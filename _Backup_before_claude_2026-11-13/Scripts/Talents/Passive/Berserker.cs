using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Berserker")]
public class Berserker : PassiveTalent
{
    public float triggerHp = 0.3f;
    public int atkBonus = 30;

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        // Use CardRuntime state instead of ScriptableObject instance variable
        bool applied = self.GetTalentState("BerserkerApplied", false);
                
        if (!applied && self.healthPercent <= triggerHp)
        {
            int bonusAtk = self.baseAttack * atkBonus / 100;
            self.attack += bonusAtk;
            self.SetTalentState("BerserkerApplied", true);
        }
    }
}
