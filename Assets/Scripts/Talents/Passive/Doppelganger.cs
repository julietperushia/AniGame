using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Doppelganger")]
public class Doppelganger : PassiveTalent
{
    public override string DefaultName => "Doppelganger";
    public override string DefaultDescription => "Whenever the enemy raises its ATK, DEF, SPD or max HP, or heals, you gain 30% of that amount too.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy) => self.doppelganger = true;
}
