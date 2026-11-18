using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Dominance")]
public class Dominance : PassiveTalent
{
    public int atkBonus = 17;

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        if (self.healthPercent >= enemy.healthPercent)
        {
                self.attack += self.attack * atkBonus / 100;
        }
    }
}
