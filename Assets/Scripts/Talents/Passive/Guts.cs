using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Guts")]
public class Guts : PassiveTalent
{
    public override string DefaultName => "Guts";
    public override string DefaultDescription => "Once per battle, you survive a hit that would knock you out with 1 HP, then gain +30% ATK for 3 turns.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy) => self.gutsReady = true;
}
