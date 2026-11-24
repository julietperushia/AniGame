using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/BurstBarrier")]
public class BurstBarrier : ActiveTalent
{
    public float shieldPercent = 0.15f;
    public int turns = 3;
    public float burstAtkPercent = 0.5f;

    public override string DefaultName => "Burst Barrier";
    public override string DefaultDescription => $"Raises a barrier worth {shieldPercent * 100:0}% of your max HP for {turns} turns; it absorbs all damage first. If it breaks, it explodes for true damage of {burstAtkPercent * 100:0}% of your ATK.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.shield = Mathf.Max(self.shield, Mathf.RoundToInt(self.maxHealth * Scaled(shieldPercent, self)));
        self.shieldTurns = turns;
        self.shieldBurst = DamageCalculator.FromStat(self.TotalAttack, Scaled(burstAtkPercent, self));
        BattleLog.Info($"<color=#6FD3FF>[Burst Barrier] {self.DisplayName} raises a {self.shield} HP barrier</color>");
    }
}
