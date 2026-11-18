using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Regeneration")]
public class Regeneration : ActiveTalent
{
    public float healPercent = 0.075f; // 7.5%
    [Tooltip("Regeneration stacks are capped (uncapped stacks caused most timed-out draws in the balance simulation).")]
    public int maxStacks = 3;

    public override string DefaultName => "Regeneration";
    public override string DefaultDescription => $"Heal {healPercent * 100:0.#}% max HP now and gain a Regeneration stack (heals {healPercent * 100:0.#}% max HP every turn per stack, max {maxStacks} stacks).";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.regenPercent = Scaled(healPercent, self);
        self.regenerationStacks = Mathf.Min(maxStacks, self.regenerationStacks + 1);
        BattleLog.Status(self, $"<color=#5DFFB0>[Regeneration] {self.DisplayName} gains a Regen stack (x{self.regenerationStacks})</color>");
        self.Heal(Mathf.RoundToInt(self.maxHealth * self.regenPercent));
    }
}
