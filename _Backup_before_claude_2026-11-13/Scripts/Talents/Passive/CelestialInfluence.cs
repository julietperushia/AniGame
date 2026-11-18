using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/CelestialInfluence")]
public class CelestialInfluence : PassiveTalent
{
    private int penaltyPercent;

    public CelestialInfluence(int penaltyPercent)
    {
        this.penaltyPercent = penaltyPercent;
    }

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        enemy.manaRegen -= enemy.manaRegen * penaltyPercent / 100;
    }
}
