using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/PiercingEdge")]
public class PiercingEdge : PassiveTalent
{
    [Tooltip("Part of the enemy's DEF your attacks ignore (0.3 = 30%).")]
    public float ignoreDef = 0.30f;
    [Tooltip("DEF ignored can't go above this, even with rarity bonus (0.9 = 90%).")]
    public float maxIgnore = 0.9f;
    [Tooltip("Part of the enemy's damage reduction (Endurance, Protector) that doesn't work against you (0.5 = half).")]
    public float reductionPierce = 0.5f;

    public override string DefaultName => "Piercing Edge";
    public override string DefaultDescription =>
        $"Your normal attacks ignore {PctF(ignoreDef, maxIgnore)} of the enemy's DEF, and the enemy's damage reduction (Endurance, Protector) loses {P(reductionPierce)} of its effect against you.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.defIgnore = Mathf.Min(maxIgnore, self.defIgnore + Scaled(ignoreDef, self));
        self.reductionPierce = Mathf.Max(self.reductionPierce, Mathf.Clamp01(reductionPierce));
    }
}
