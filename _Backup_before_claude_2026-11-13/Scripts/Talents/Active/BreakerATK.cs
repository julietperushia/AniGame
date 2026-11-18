using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/BreakerATK")]
public class BreakerATK : ActiveTalent
{
    public int atkReduction = 20;

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int reductionAmount = enemy.baseAttack * atkReduction / 100;
        enemy.attack -= reductionAmount;
        Debug.Log($"<color=red>[Breaker ATK] Reduced enemy ATK by {reductionAmount} (Enemy ATK: {enemy.attack})</color>");
    }
}
