using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/ChargeStrike")]
public class ChargeStrike : ActiveTalent
{
    public override string DefaultName => "Charge Strike";
    public override string DefaultDescription => "Skips your attack this turn to charge up. Your next normal attack deals triple damage and can't be evaded.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.skipAttackThisTurn = true;
        self.chargeStrikeReady = true;
        BattleLog.Info($"<color=#FFB300>[Charge Strike] {self.DisplayName} gathers power...</color>");
    }
}
