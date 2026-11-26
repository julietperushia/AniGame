using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Poison")]
public class Poison : ActiveTalent
{
    [Tooltip("Poison stacks added per cast.")]
    public int stacksPerCast = 1;
    [Tooltip("True damage per turn per stack, in % of the target's max HP (0.05 = 5%).")]
    public float percentPerStack = 0.05f;
    [Tooltip("Flat true damage added to every tick.")]
    public int flatDamage = 5;
    [Tooltip("Chance (%) a Poison tick misses with one stack. A miss cures it: every stack is removed.")]
    public int missBase = 15;
    [Tooltip("Miss chance (%) added for every stack after the first.")]
    public int missPerStack = 5;

    public override string DefaultName => "Poison";
    public override string DefaultDescription =>
        $"Inflict {(stacksPerCast == 1 ? "a stack" : $"{stacksPerCast} stacks")} of Poison. At the start of each of its turns a poisoned enemy takes {flatDamage} true damage plus " +
        $"{PctF(percentPerStack)} of its max HP per stack. Each tick can miss: {missBase}% with one stack, +{missPerStack}% for every extra stack. " +
        "A miss cures the poison and removes every stack.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        StatusEffects.ApplyPoison(enemy, stacksPerCast, Scaled(percentPerStack, self), flatDamage, missBase, missPerStack);
    }
}
