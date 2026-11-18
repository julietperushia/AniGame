using UnityEngine;

/// <summary>Anigame: "Increases CRIT of allied familiars by 16% and CRIT DMG by 25%".</summary>
[CreateAssetMenu(menuName = "Talents/Active/Precision")]
public class Precision : ActiveTalent
{
    [Tooltip("Crit rate percentage points per cast.")]
    public int critRateBonus = 20;
    [Tooltip("Crit damage percentage points per cast.")]
    public int critDamageBonus = 20;
    public int maxCritRate = 100;

    public override string DefaultName => "Precision";
    public override string DefaultDescription => $"Gain +{critRateBonus}% crit rate and +{critDamageBonus}% crit damage (stacks).";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.critRate = Mathf.Min(maxCritRate, self.critRate + critRateBonus);
        self.critDamage += critDamageBonus;
        BattleLog.Info($"<color=#6FD3FF>[Precision] Crit {self.critRate}% / Crit DMG {self.critDamage}%</color>");
    }
}
