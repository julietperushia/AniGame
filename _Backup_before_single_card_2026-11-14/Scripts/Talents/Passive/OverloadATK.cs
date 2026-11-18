using UnityEngine;

/// <summary>Anigame: "When the battle starts, increase the ATK by 55-60% of all allied familiars. Your ATK decreases by 8% every turn after that."
/// The decay stops once ATK is back to its base value.</summary>
[CreateAssetMenu(menuName = "Talents/Passive/OverloadATK")]
public class OverloadATK : PassiveTalent
{
    public int startBonus = 60;
    public int decayPerTurn = 8;

    public override string DefaultName => "Overload ATK";
    public override string DefaultDescription => $"When the battle starts, increase your ATK by {startBonus}%. Your ATK decreases by {decayPerTurn}% every turn after that (down to base).";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        int bonus = self.baseAttack * startBonus / 100;
        self.attack += bonus;
        BattleLog.Passive(self, $"<color=#FFB23D>[Overload ATK] +{bonus} ATK</color>");
    }

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        if (self.attack <= self.baseAttack) return;
        int decay = Mathf.Max(1, self.baseAttack * decayPerTurn / 100);
        self.attack = Mathf.Max(self.baseAttack, self.attack - decay);
    }
}
