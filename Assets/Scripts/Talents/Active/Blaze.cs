using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Blaze")]
public class Blaze : ActiveTalent
{
    [Tooltip("Burn stacks added per cast.")]
    public int stacksPerCast = 1;
    [Tooltip("True damage per turn per stack, in % of the target's max HP (0.05 = 5%).")]
    public float burnPercent = 0.05f;
    [Tooltip("Most Burn stacks a target can have.")]
    public int maxStacks = 4;
    [Tooltip("Healing the burning target receives is reduced by this much (0.75 = -75%).")]
    public float healingCut = 0.75f;
    [Tooltip("Chance (%) a Burn tick misses with one stack. A miss puts the fire out: every stack is removed.")]
    public int missBase = 15;
    [Tooltip("Miss chance (%) added for every stack after the first.")]
    public int missPerStack = 5;

    public override string DefaultName => "Blaze";
    public override string DefaultDescription =>
        $"Inflict {(stacksPerCast == 1 ? "a stack" : $"{stacksPerCast} stacks")} of Burn. At the start of each of its turns a burning enemy takes true damage equal to " +
        $"{PctF(burnPercent)} of its max HP per stack (max {maxStacks} stacks = {PctF(burnPercent * maxStacks)}), and all healing it receives is reduced by {P(healingCut)}. " +
        $"Each tick can miss: {missBase}% with one stack, +{missPerStack}% for every extra stack (max {Mathf.Clamp(missBase + missPerStack * (maxStacks - 1), 0, 100)}%). " +
        "A miss puts the fire out and removes every stack.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        StatusEffects.ApplyBurn(enemy, stacksPerCast, Scaled(burnPercent, self), maxStacks, healingCut, missBase, missPerStack);
    }
}
