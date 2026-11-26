using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/BreakerDEF")]
public class BreakerDEF : ActiveTalent
{
    [Tooltip("Enemy DEF lost per cast, in % of its base DEF.")]
    public int defReduction = 20;

    public override string DefaultName => "Breaker DEF";
    public override string DefaultDescription => $"Lower the enemy's DEF by {Pct(defReduction)} of its base DEF. Every cast stacks.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int reductionAmount = Mathf.Max(1, enemy.baseDefense * Scaled(defReduction, self) / 100);
        enemy.defense -= reductionAmount;
        BattleLog.Info($"<color=#FF6B6B>[Breaker DEF] {enemy.DisplayName} DEF -{reductionAmount} (now {enemy.TotalDefense})</color>");
    }
}
