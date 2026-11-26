using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Nothingness")]
public class Nothingness : PassiveTalent
{
    [Tooltip("True damage the copier takes, in % of its max HP (0.3 = 30%).")]
    public float copyPenalty = 0.3f;

    public override string DefaultName => "Nothingness";
    public override string DefaultDescription =>
        $"Does nothing by itself... but an enemy that copies it (Mimic) instantly takes true damage equal to {P(copyPenalty)} of its max HP.";
}
