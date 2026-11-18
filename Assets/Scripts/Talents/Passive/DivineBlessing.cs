using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Divine Blessing")]
public class DivineBlessing : PassiveTalent
{
    public int atkBonus = 30;
    public int defBonus = 30;
    public int duration = 5;

    public override string DefaultName => "Divine Blessing";
    public override string DefaultDescription => $"Start the battle with Divine Blessing: +{atkBonus}% ATK and +{defBonus}% DEF for {duration} turns.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.AddTimedBuff(atkBonusPercent: Scaled(atkBonus, self), defBonusPercent: Scaled(defBonus, self), turns: duration);
        BattleLog.Passive(self, $"<color=#FFD54F>[Divine Blessing] +{atkBonus}% ATK / +{defBonus}% DEF for {duration} turns</color>");
    }
}
