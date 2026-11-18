using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/BreakerDEF")]
public class BreakerDEF : ActiveTalent
{
    public int defReduction = 20;

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int reductionAmount = enemy.baseDefense * defReduction / 100;
        enemy.defense -= reductionAmount;
        Debug.Log($"<color=red>[Breaker DEF] Reduced enemy DEF by {reductionAmount} (Enemy DEF: {enemy.defense})</color>");
    }
}
