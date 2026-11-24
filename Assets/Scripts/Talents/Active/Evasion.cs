using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Evasion")]
public class Evasion : ActiveTalent
{
    [Tooltip("Evasion rating gained per cast (no limit; the real chance has diminishing returns, max 75%).")]
    public int evasionBonus = 17;

    public override string DefaultName => "Evasion";
    public override string DefaultDescription => $"Gain +{evasionBonus} evasion rating (stacks, no limit). Evade chance has diminishing returns and never passes {GameRules.EvasionCap:0}%.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.evasion += Scaled(evasionBonus, self);
        BattleLog.Info($"<color=#6FD3FF>[Evasion] {self.DisplayName} evasion rating {GameRules.EvasionLabel(self.evasion)}</color>");
    }
}
