using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/AmplifierATK")]
public class AmplifierATK : ActiveTalent
{
    [Tooltip("ATK gained per cast, in % of base ATK.")]
    public int atkBonus = 14;

    public override string DefaultName => "Amplifier ATK";
    public override string DefaultDescription => $"Permanently raise your ATK by {Pct(atkBonus)} of your base ATK. Every cast stacks.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int bonusAtk = Mathf.Max(1, self.baseAttack * Scaled(atkBonus, self) / 100);
        self.attack += bonusAtk;
        BattleLog.Info($"<color=#6FD3FF>[Amplifier ATK] {self.DisplayName} +{bonusAtk} ATK (now {self.TotalAttack})</color>");
    }
}
