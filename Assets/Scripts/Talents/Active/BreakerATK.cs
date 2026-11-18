using UnityEngine;

/// <summary>Anigame: "Decrease the ATK of all enemy familiars by 20%".</summary>
[CreateAssetMenu(menuName = "Talents/Active/BreakerATK")]
public class BreakerATK : ActiveTalent
{
    public int atkReduction = 20;

    public override string DefaultName => "Breaker ATK";
    public override string DefaultDescription => $"Decrease the enemy's ATK by {atkReduction}% of its base ATK.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int reductionAmount = Mathf.Max(1, enemy.baseAttack * Scaled(atkReduction, self) / 100);
        enemy.attack -= reductionAmount;
        BattleLog.Info($"<color=#FF6B6B>[Breaker ATK] {enemy.DisplayName} ATK -{reductionAmount} (now {enemy.TotalAttack})</color>");
    }
}
