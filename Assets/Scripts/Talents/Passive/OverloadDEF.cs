using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/OverloadDEF")]
public class OverloadDEF : PassiveTalent
{
    [Tooltip("DEF bonus when the battle starts, in % of base DEF.")]
    public int startBonus = 60;
    [Tooltip("DEF lost at the start of each of your turns, in % of base DEF. It keeps dropping below base.")]
    public int decayPerTurn = 8;
    [Tooltip("Lowest the decay can take your DEF, in % of base DEF (0 = it can drop all the way to 0).")]
    public int floorPercent = 0;

    public override string DefaultName => "Overload DEF";
    public override string DefaultDescription =>
        $"When the battle starts, raise your DEF by {Pct(startBonus)} of your base DEF. At the start of each of your turns it drops by {decayPerTurn}% of your base DEF, " +
        (floorPercent > 0 ? $"and keeps dropping below base until it reaches {floorPercent}% of your base DEF." : "and it keeps dropping below base: the longer the battle, the lower it gets.");

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        int bonus = self.baseDefense * Scaled(startBonus, self) / 100;
        self.defense += bonus;
        BattleLog.Passive(self, $"<color=#FFB23D>[Overload DEF] +{bonus} DEF</color>");
    }

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        int floor = Mathf.Max(0, self.baseDefense * floorPercent / 100);
        if (self.defense <= floor) return;
        int decay = Mathf.Max(1, self.baseDefense * decayPerTurn / 100);
        self.defense = Mathf.Max(floor, self.defense - decay);
    }
}
