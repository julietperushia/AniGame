using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Regeneration")]
public class Regeneration : ActiveTalent
{
    [Tooltip("Heal now and per stack each turn, in % of your max HP (0.075 = 7.5%).")]
    public float healPercent = 0.075f;
    [Tooltip("Most Regeneration stacks (uncapped stacks caused most timed-out draws in the balance simulation).")]
    public int maxStacks = 3;

    public override string DefaultName => "Regeneration";
    public override string DefaultDescription =>
        $"Heal {PctF(healPercent)} of your max HP now and gain a Regeneration stack: at the start of each of your turns you heal {PctF(healPercent)} of your max HP " +
        $"per stack (max {maxStacks} stacks = {PctF(healPercent * maxStacks)} per turn).";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.regenPercent = Scaled(healPercent, self);
        self.regenerationStacks = Mathf.Min(maxStacks, self.regenerationStacks + 1);
        BattleLog.Status(self, $"<color=#5DFFB0>[Regeneration] {self.DisplayName} gains a Regen stack (x{self.regenerationStacks})</color>");
        self.Heal(Mathf.RoundToInt(self.maxHealth * self.regenPercent));
    }
}
