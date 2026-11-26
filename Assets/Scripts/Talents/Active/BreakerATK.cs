using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/BreakerATK")]
public class BreakerATK : ActiveTalent
{
    [Tooltip("Enemy ATK lost per cast, in % of its base ATK.")]
    public int atkReduction = 20;

    public override string DefaultName => "Breaker ATK";
    public override string DefaultDescription => $"Lower the enemy's ATK by {Pct(atkReduction)} of its base ATK. Every cast stacks.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int reductionAmount = Mathf.Max(1, enemy.baseAttack * Scaled(atkReduction, self) / 100);
        enemy.attack -= reductionAmount;
        BattleLog.Info($"<color=#FF6B6B>[Breaker ATK] {enemy.DisplayName} ATK -{reductionAmount} (now {enemy.TotalAttack})</color>");
    }
}
