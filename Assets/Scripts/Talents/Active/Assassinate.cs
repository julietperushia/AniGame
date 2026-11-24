using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Assassinate")]
public class Assassinate : ActiveTalent
{
    public float atkPercent = 1.3f;

    public override string DefaultName => "Assassinate";
    public override string DefaultDescription => $"A guaranteed critical hit for {atkPercent * 100:0}% of a normal attack that can't be evaded. It uses your crit damage.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        BattleLog.Current?.PerformAttack(self, enemy, new BattleEngine.AttackOptions { skillHit = true, forceCrit = true, unevadable = true, multiplier = Scaled(atkPercent, self) });
    }
}
