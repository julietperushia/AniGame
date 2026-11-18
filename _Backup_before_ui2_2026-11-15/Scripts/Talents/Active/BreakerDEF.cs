using UnityEngine;

/// <summary>Anigame: "Decrease the DEF of all enemy familiars by 20%".</summary>
[CreateAssetMenu(menuName = "Talents/Active/BreakerDEF")]
public class BreakerDEF : ActiveTalent
{
    public int defReduction = 20;

    public override string DefaultName => "Breaker DEF";
    public override string DefaultDescription => $"Decrease the enemy's DEF by {defReduction}% of its base DEF.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int reductionAmount = Mathf.Max(1, enemy.baseDefense * Scaled(defReduction, self) / 100);
        enemy.defense -= reductionAmount;
        BattleLog.Info($"<color=#FF6B6B>[Breaker DEF] {enemy.DisplayName} DEF -{reductionAmount} (now {enemy.TotalDefense})</color>");
    }
}
