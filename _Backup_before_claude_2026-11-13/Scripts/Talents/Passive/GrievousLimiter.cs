using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Grievous Limiter")]
public class GrievousLimiter : PassiveTalent
{
    public int startReduction = 40;
    public int recoveryPerTurn = 5;

    int turnsPassed = 0;

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        enemy.attack -= enemy.attack * startReduction / 100;
    }

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        turnsPassed++;
        enemy.attack += enemy.baseAttack * recoveryPerTurn / 100;

    }
}
