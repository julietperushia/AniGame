using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Momentum")]
public class Momentum : PassiveTalent
{
    [Tooltip("Attack damage bonus per stack (0.06 = +6%).")]
    public float atkPerStack = 0.06f;
    [Tooltip("Most stacks.")]
    public int maxStacks = 5;

    public override string DefaultName => "Momentum";
    public override string DefaultDescription =>
        $"Every normal attack that deals damage gives a stack of +{PctF(atkPerStack)} attack damage, up to {maxStacks} stacks (+{PctF(atkPerStack * maxStacks)}). " +
        "Missing or getting evaded resets all stacks.";

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
