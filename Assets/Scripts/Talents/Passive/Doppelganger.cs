using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Doppelganger")]
public class Doppelganger : PassiveTalent
{
    [Tooltip("Part of every enemy stat gain and heal you copy (0.3 = 30%).")]
    public float copyPercent = 0.3f;

    public override string DefaultName => "Doppelganger";
    public override string DefaultDescription =>
        $"Whenever the enemy raises its ATK, DEF, SPD or max HP, or heals, you gain {P(copyPercent)} of that amount too.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy) => self.doppelgangerPercent = copyPercent;
}
