using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Blackhole")]
public class Blackhole : PassiveTalent
{
    public override string DefaultName => "Blackhole";
    public override string DefaultDescription => "While this card is in battle, neither fighter can raise its ATK, DEF, SPD or max HP. Every stat gain is swallowed (healing still works).";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy) =>
        BattleLog.Passive(self, "<color=#9E9E9E>[Blackhole] Stat gains are disabled for both sides</color>");
}
