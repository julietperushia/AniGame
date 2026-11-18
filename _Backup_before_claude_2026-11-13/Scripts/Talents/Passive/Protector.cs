using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Protector")]
public class Protector : PassiveTalent
{
    public float triggerHp = 0.3f;

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        bool triggered = self.GetTalentState("ProtectorTriggered", false);
        
        if (triggered || self.healthPercent > triggerHp) return;

        self.Heal(self.maxHealth * 20 / 100);
        self.defense += self.defense * 35 / 100;
        self.damageReduction += 5;
        self.SetTalentState("ProtectorTriggered", true);
    }
}
