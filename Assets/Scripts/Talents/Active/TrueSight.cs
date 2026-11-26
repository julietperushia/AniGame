using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/TrueSight")]
public class TrueSight : ActiveTalent
{
    [Tooltip("Part of the enemy's evasion rating removed (0.30 = 30%).")]
    public float stealPercent = 0.30f;
    [Tooltip("Part of the removed rating you gain (0.30 = 30%).")]
    public float gainPercent = 0.30f;
    [Tooltip("Most of the enemy's rating one cast can remove, even with rarity bonus (0.95 = 95%).")]
    public float maxSteal = 0.95f;

    public override string DefaultName => "True Sight";
    public override string DefaultDescription =>
        $"The enemy loses {PctF(stealPercent, maxSteal)} of its evasion rating, and you gain {P(gainPercent)} of the rating it lost.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int lost = Mathf.RoundToInt(enemy.evasion * Mathf.Min(maxSteal, Scaled(stealPercent, self)));
        enemy.evasion = Mathf.Max(0, enemy.evasion - lost);
        int gain = Mathf.RoundToInt(lost * gainPercent);
        self.evasion += gain;
        BattleLog.Info($"<color=#64FFDA>[True Sight] {enemy.DisplayName} evasion -{lost}, {self.DisplayName} +{gain} ({GameRules.EvasionLabel(self.evasion)})</color>");
    }
}
