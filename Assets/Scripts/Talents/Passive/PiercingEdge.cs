using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/PiercingEdge")]
public class PiercingEdge : PassiveTalent
{
    public float ignoreDef = 0.30f;

    public override string DefaultName => "Piercing Edge";
    public override string DefaultDescription => $"Your attacks ignore {ignoreDef * 100:0}% of the enemy's DEF, and its damage reduction (Endurance, Protector) only works half as well against you.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.defIgnore = Mathf.Min(0.9f, self.defIgnore + Scaled(ignoreDef, self));
        self.reductionPierce = true;
    }
}
