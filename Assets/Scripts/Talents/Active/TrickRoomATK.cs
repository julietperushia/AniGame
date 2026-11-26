using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/TrickRoomATK")]
public class TrickRoomATK : ActiveTalent
{
    [Tooltip("Part of the ATK difference you take (%).")]
    public int swapPercent = 60;

    public override string DefaultName => "Trick Room ATK";
    public override string DefaultDescription =>
        $"If the enemy's ATK is higher than yours, take {Pct(swapPercent)} of the difference: you gain that much ATK and the enemy loses it. No effect if your ATK is already higher.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        if (enemy.TotalAttack > self.TotalAttack)
        {
            int swapAmount = Mathf.RoundToInt((enemy.TotalAttack - self.TotalAttack) * Scaled(swapPercent, self) / 100f);
            self.attack += swapAmount;
            enemy.attack -= swapAmount;
            BattleLog.Info($"<color=#6FD3FF>[Trick Room ATK] Stole {swapAmount} ATK ({self.TotalAttack} vs {enemy.TotalAttack})</color>");
        }
        else BattleLog.Info("<color=#9AA4B5>[Trick Room ATK] No effect - your ATK is already higher</color>");
    }
}
