using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/TrueSight")]
public class TrueSight : ActiveTalent
{
    public float stealPercent = 0.30f;

    public override string DefaultName => "True Sight";
    public override string DefaultDescription => $"The enemy loses {stealPercent * 100:0}% of its evasion rating, and you gain {stealPercent * 100:0}% of what it lost as evasion rating.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int lost = Mathf.RoundToInt(enemy.evasion * Mathf.Min(0.95f, Scaled(stealPercent, self)));
        enemy.evasion = Mathf.Max(0, enemy.evasion - lost);
        int gain = Mathf.RoundToInt(lost * stealPercent);
        self.evasion += gain;
        BattleLog.Info($"<color=#64FFDA>[True Sight] {enemy.DisplayName} evasion -{lost}, {self.DisplayName} +{gain} ({GameRules.EvasionLabel(self.evasion)})</color>");
    }
}
