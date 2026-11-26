using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/OverloadATK")]
public class OverloadATK : PassiveTalent
{
    [Tooltip("ATK bonus when the battle starts, in % of base ATK.")]
    public int startBonus = 60;
    [Tooltip("ATK lost at the start of each of your turns, in % of base ATK. It keeps dropping below base.")]
    public int decayPerTurn = 8;
    [Tooltip("Lowest the decay can take your ATK, in % of base ATK (0 = it can drop all the way to 0).")]
    public int floorPercent = 0;

    public override string DefaultName => "Overload ATK";
    public override string DefaultDescription =>
        $"When the battle starts, raise your ATK by {Pct(startBonus)} of your base ATK. At the start of each of your turns it drops by {decayPerTurn}% of your base ATK, " +
        (floorPercent > 0 ? $"and keeps dropping below base until it reaches {floorPercent}% of your base ATK." : "and it keeps dropping below base: the longer the battle, the lower it gets.");

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        int bonus = self.baseAttack * Scaled(startBonus, self) / 100;
        self.attack += bonus;
        BattleLog.Passive(self, $"<color=#FFB23D>[Overload ATK] +{bonus} ATK</color>");
    }

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        int floor = Mathf.Max(0, self.baseAttack * floorPercent / 100);
        if (self.attack <= floor) return;
        int decay = Mathf.Max(1, self.baseAttack * decayPerTurn / 100);
        self.attack = Mathf.Max(floor, self.attack - decay);
    }
}
