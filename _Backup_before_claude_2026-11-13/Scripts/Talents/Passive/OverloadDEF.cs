using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/OverloadDEF")]
public class OverloadDEF : PassiveTalent
{
    public int startBonus = 60;
    public int decayPerTurn = 8;

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.defense += self.defense * startBonus / 100;
    }

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        self.defense -= self.baseDefense * decayPerTurn / 100;
    }
}
