using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/AmplifierDEF")]
public class AmplifierDEF : ActiveTalent
{
    public int defBonus = 14;

    public override string DefaultName => "Amplifier DEF";
    public override string DefaultDescription => $"Permanently increase your DEF by {defBonus}% of base DEF. Stacks every cast.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int bonusDef = Mathf.Max(1, self.baseDefense * defBonus / 100);
        self.defense += bonusDef;
        BattleLog.Info($"<color=#6FD3FF>[Amplifier DEF] {self.DisplayName} +{bonusDef} DEF (now {self.TotalDefense})</color>");
    }
}
