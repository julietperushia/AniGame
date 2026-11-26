using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Precision")]
public class Precision : ActiveTalent
{
    [Tooltip("Crit rate gained per cast (% points).")]
    public int critRateBonus = 16;
    [Tooltip("Crit damage gained per cast (% points).")]
    public int critDamageBonus = 25;
    [Tooltip("Crit rate can't go above this (%).")]
    public int maxCritRate = 100;

    public override string DefaultName => "Precision";
    public override string DefaultDescription =>
        $"Gain +{Pct(critRateBonus)} crit rate (max {maxCritRate}%) and +{Pct(critDamageBonus)} crit damage. Every cast stacks. From the first cast on, your critical hits can't be evaded.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.critRate = Mathf.Min(maxCritRate, self.critRate + Scaled(critRateBonus, self));
        self.critDamage += Scaled(critDamageBonus, self);
        self.critIgnoresEvasion = true;
        BattleLog.Info($"<color=#6FD3FF>[Precision] Crit {self.critRate}% / Crit DMG {self.critDamage}% - crits can't be evaded</color>");
    }
}
