using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/AmplifierDEF")]
public class AmplifierDEF : ActiveTalent
{
    [Tooltip("DEF gained per cast, in % of base DEF.")]
    public int defBonus = 14;

    public override string DefaultName => "Amplifier DEF";
    public override string DefaultDescription => $"Permanently raise your DEF by {Pct(defBonus)} of your base DEF. Every cast stacks.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int bonusDef = Mathf.Max(1, self.baseDefense * Scaled(defBonus, self) / 100);
        self.defense += bonusDef;
        BattleLog.Info($"<color=#6FD3FF>[Amplifier DEF] {self.DisplayName} +{bonusDef} DEF (now {self.TotalDefense})</color>");
    }
}
