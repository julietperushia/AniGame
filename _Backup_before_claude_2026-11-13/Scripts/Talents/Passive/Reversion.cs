using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Reversion")]
public class Reversion : PassiveTalent
{
    public float triggerHp = 0.2f;
    bool used = false;

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        if (used || self.healthPercent > triggerHp) return;
        self.ResetToBaseStats();
        self.defense += self.baseDefense * 12 / 100;
        self.speed += self.baseSpeed * 12 / 100;
        used = true;
    }
}
