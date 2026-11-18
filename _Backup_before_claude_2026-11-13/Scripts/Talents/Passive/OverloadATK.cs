using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/OverloadATK")]
public class OverloadATK : PassiveTalent
{
    public int startBonus = 60;
    public int decayPerTurn = 8;

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
            self.attack += self.attack * startBonus / 100;
    }

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        self.attack -= self.baseAttack * decayPerTurn / 100;
    }
}
