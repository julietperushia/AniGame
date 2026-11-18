using UnityEngine;

/// <summary>Same as Overload ATK, with DEF.</summary>
[CreateAssetMenu(menuName = "Talents/Passive/OverloadDEF")]
public class OverloadDEF : PassiveTalent
{
    public int startBonus = 60;
    public int decayPerTurn = 8;

    public override string DefaultName => "Overload DEF";
    public override string DefaultDescription => $"When the battle starts, increase your DEF by {startBonus}%. Your DEF decreases by {decayPerTurn}% every turn after that (down to base).";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        int bonus = self.baseDefense * startBonus / 100;
        self.defense += bonus;
        BattleLog.Passive(self, $"<color=#FFB23D>[Overload DEF] +{bonus} DEF</color>");
    }

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        if (self.defense <= self.baseDefense) return;
        int decay = Mathf.Max(1, self.baseDefense * decayPerTurn / 100);
        self.defense = Mathf.Max(self.baseDefense, self.defense - decay);
    }
}
