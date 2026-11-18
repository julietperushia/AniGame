using UnityEngine;

/// <summary>Same as Trick Room ATK but for DEF.</summary>
[CreateAssetMenu(menuName = "Talents/Active/TrickRoomDEF")]
public class TrickRoomDEF : ActiveTalent
{
    public int swapPercent = 60;

    public override string DefaultName => "Trick Room DEF";
    public override string DefaultDescription => $"If the enemy has more DEF than you, steal {swapPercent}% of the difference.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        if (enemy.TotalDefense > self.TotalDefense)
        {
            int swapAmount = Mathf.RoundToInt((enemy.TotalDefense - self.TotalDefense) * Scaled(swapPercent, self) / 100f);
            self.defense += swapAmount;
            enemy.defense -= swapAmount;
            BattleLog.Info($"<color=#6FD3FF>[Trick Room DEF] Stole {swapAmount} DEF ({self.TotalDefense} vs {enemy.TotalDefense})</color>");
        }
        else
        {
            BattleLog.Info("<color=#9AA4B5>[Trick Room DEF] No effect - your DEF is already higher</color>");
        }
    }
}
