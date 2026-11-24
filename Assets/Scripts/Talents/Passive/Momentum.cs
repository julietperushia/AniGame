using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Momentum")]
public class Momentum : PassiveTalent
{
    public float atkPerStack = 0.06f;
    public int maxStacks = 5;

    public override string DefaultName => "Momentum";
    public override string DefaultDescription => $"Every attack that lands gives +{atkPerStack * 100:0}% ATK, up to {maxStacks} stacks. Getting evaded (or missing) resets it.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.momentumPerStack = Scaled(atkPerStack, self);
        self.momentumMax = maxStacks;
    }

    public override void OnAfterAttack(CardRuntime self, CardRuntime enemy)
    {
        if (self.lastDamageDealt > 0 && self.momentumStacks < self.momentumMax) self.momentumStacks++;
    }

    public override void OnAttackMissed(CardRuntime self, CardRuntime enemy)
    {
        if (self.momentumStacks > 0) BattleLog.Passive(self, $"[Momentum] {self.DisplayName} loses momentum");
        self.momentumStacks = 0;
    }
}
