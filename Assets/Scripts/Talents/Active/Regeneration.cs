using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Regeneration")]
public class Regeneration : ActiveTalent
{
    [Tooltip("Heal now and per stack each turn, in % of your max HP (0.075 = 7.5%).")]
    public float healPercent = 0.075f;
    [Tooltip("Most Regeneration stacks (uncapped stacks caused most timed-out draws in the balance simulation).")]
    public int maxStacks = 3;
    [Tooltip("Chance (%) a Regeneration tick fails with one stack. A failed tick heals nothing and removes one stack.")]
    public int missBase = 10;
    [Tooltip("Fail chance (%) added for every stack after the first.")]
    public int missPerStack = 10;

    public override string DefaultName => "Regeneration";
    public override string DefaultDescription =>
        $"Heal {PctF(healPercent)} of your max HP now and gain a Regeneration stack: at the start of each of your turns you heal {PctF(healPercent)} of your max HP " +
        $"per stack (max {maxStacks} stacks = {PctF(healPercent * maxStacks)} per turn). " +
        $"Each turn's heal can fail: {missBase}% with one stack, +{missPerStack}% for every extra stack (max {Mathf.Clamp(missBase + missPerStack * (maxStacks - 1), 0, 100)}%). " +
        "A failed turn heals nothing and loses one stack.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.regenPercent = Scaled(healPercent, self);
        self.regenMissBase = missBase;
        self.regenMissPerStack = missPerStack;
        self.regenerationStacks = Mathf.Min(maxStacks, self.regenerationStacks + 1);
        BattleLog.Status(self, $"<color=#5DFFB0>[Regeneration] {self.DisplayName} gains a Regen stack (x{self.regenerationStacks})</color>");
        self.Heal(Mathf.RoundToInt(self.maxHealth * self.regenPercent));
    }
}
