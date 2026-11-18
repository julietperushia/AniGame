using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Divine Blessing")]
public class DivineBlessing : PassiveTalent
{
    public int atkBonus = 30;
    public int defBonus = 30;
    public int duration = 5;

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.AddTimedBuff(
            atkBonusPercent: atkBonus,
            defBonusPercent: defBonus,
            turns: duration
        );
    }
}
