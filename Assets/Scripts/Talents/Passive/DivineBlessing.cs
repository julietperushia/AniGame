using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Divine Blessing")]
public class DivineBlessing : PassiveTalent
{
    [Tooltip("ATK bonus (%).")]
    public int atkBonus = 30;
    [Tooltip("DEF bonus (%).")]
    public int defBonus = 30;
    [Tooltip("Turns the blessing lasts.")]
    public int duration = 5;

    public override string DefaultName => "Divine Blessing";
    public override string DefaultDescription => $"Start the battle blessed: +{Pct(atkBonus)} ATK and +{Pct(defBonus)} DEF for your first {N(duration, "turn")}.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.AddTimedBuff(atkBonusPercent: Scaled(atkBonus, self), defBonusPercent: Scaled(defBonus, self), turns: duration);
        BattleLog.Passive(self, $"<color=#FFD54F>[Divine Blessing] +{atkBonus}% ATK / +{defBonus}% DEF for {duration} turns</color>");
    }
}
