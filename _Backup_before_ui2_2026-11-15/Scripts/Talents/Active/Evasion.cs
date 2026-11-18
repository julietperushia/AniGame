using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Evasion")]
public class Evasion : ActiveTalent
{
    [Tooltip("Evasion percentage points gained per cast.")]
    public int evasionBonus = 17;
    public int maxEvasion = 60;

    public override string DefaultName => "Evasion";
    public override string DefaultDescription => $"Gain +{evasionBonus}% evasion (stacks, max {maxEvasion}%).";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.evasion = Mathf.Min(maxEvasion, self.evasion + Scaled(evasionBonus, self));
        BattleLog.Info($"<color=#6FD3FF>[Evasion] {self.DisplayName} evasion now {self.evasion}%</color>");
    }
}
