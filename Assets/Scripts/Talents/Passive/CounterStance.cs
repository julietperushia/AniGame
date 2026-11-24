using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/CounterStance")]
public class CounterStance : PassiveTalent
{
    public int evasionBonus = 5;
    public float counterDamage = 0.4f;

    public override string DefaultName => "Counter Stance";
    public override string DefaultDescription => $"+{evasionBonus} evasion rating. Every time you evade, you strike back at once for {counterDamage * 100:0}% of a normal attack (gives no mana).";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.evasion += Scaled(evasionBonus, self);
        self.counterStance = true;
        self.counterDamage = Scaled(counterDamage, self);
    }
}
