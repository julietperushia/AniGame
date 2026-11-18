using UnityEngine;

/// <summary>Anigame: "If the enemy familiars' ATK is higher than yours, your allies gain ATK equal to 60% of the difference between the two ATK's, and simultaneously reduce their ATK by the same amount".</summary>
[CreateAssetMenu(menuName = "Talents/Active/TrickRoomATK")]
public class TrickRoomATK : ActiveTalent
{
    public int swapPercent = 60;

    public override string DefaultName => "Trick Room ATK";
    public override string DefaultDescription => $"If the enemy has more ATK than you, steal {swapPercent}% of the difference.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        if (enemy.TotalAttack > self.TotalAttack)
        {
            int swapAmount = Mathf.RoundToInt((enemy.TotalAttack - self.TotalAttack) * Scaled(swapPercent, self) / 100f);
            self.attack += swapAmount;
            enemy.attack -= swapAmount;
            BattleLog.Info($"<color=#6FD3FF>[Trick Room ATK] Stole {swapAmount} ATK ({self.TotalAttack} vs {enemy.TotalAttack})</color>");
        }
        else
        {
            BattleLog.Info("<color=#9AA4B5>[Trick Room ATK] No effect - your ATK is already higher</color>");
        }
    }
}
