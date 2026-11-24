using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Nothingness")]
public class Nothingness : PassiveTalent
{
    public override string DefaultName => "Nothingness";
    public override string DefaultDescription => "This talent does nothing... unless the enemy copies it (Mimic): then the copier takes true damage equal to 30% of its max HP.";
}
