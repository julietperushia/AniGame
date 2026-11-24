using UnityEngine;

/// <summary>Anigame: "Increases CRIT of allied familiars by 16% and CRIT DMG by 25%".</summary>
[CreateAssetMenu(menuName = "Talents/Active/Precision")]
public class Precision : ActiveTalent
{
    [Tooltip("Crit rate percentage points per cast.")]
    public int critRateBonus = 16;
    [Tooltip("Crit damage percentage points per cast.")]
    public int critDamageBonus = 25;
    public int maxCritRate = 100;

    public override string DefaultName => "Precision";
    public override string DefaultDescription => $"Gain +{critRateBonus}% crit rate and +{critDamageBonus}% crit damage (stacks). Once active, your critical hits ignore evasion.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.critRate = Mathf.Min(maxCritRate, self.critRate + Scaled(critRateBonus, self));
        self.critDamage += Scaled(critDamageBonus, self);
        self.critIgnoresEvasion = true;
        BattleLog.Info($"<color=#6FD3FF>[Precision] Crit {self.critRate}% / Crit DMG {self.critDamage}% - crits can't be evaded</color>");
    }
}
