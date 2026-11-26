using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/ChargeStrike")]
public class ChargeStrike : ActiveTalent
{
    [Tooltip("Damage multiplier of the charged attack (3 = triple damage).")]
    public float damageMultiplier = 3f;
    [Tooltip("The charged attack can't be evaded.")]
    public bool unevadable = true;

    public override string DefaultName => "Charge Strike";
    public override string DefaultDescription =>
        $"Skip your attack this turn to charge up. Your next normal attack deals {damageMultiplier:0.##}x damage{(unevadable ? " and can't be evaded" : "")}.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.skipAttackThisTurn = true;
        self.chargeStrikeReady = true;
        self.chargeStrikeMultiplier = damageMultiplier;
        self.chargeStrikeUnevadable = unevadable;
        BattleLog.Info($"<color=#FFB300>[Charge Strike] {self.DisplayName} gathers power...</color>");
    }
}
