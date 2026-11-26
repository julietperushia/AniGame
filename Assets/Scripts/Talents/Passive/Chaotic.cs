using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Chaotic")]
public class Chaotic : PassiveTalent
{
    public override string DefaultName => "Chaotic";
    public override string DefaultDescription =>
        "While this card is in battle, stat changes are reversed for both sides: anything that would raise ATK, DEF, SPD or max HP lowers it instead, " +
        "and anything that would lower it raises it.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy) =>
        BattleLog.Passive(self, "<color=#FF4081>[Chaotic] All stat changes are reversed!</color>");
}
