using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Hypnotize")]
public class Hypnotize : ActiveTalent
{
    [Tooltip("Hypnotize stacks added per cast.")]
    public int stacksPerCast = 1;
    [Tooltip("Chance (%) per stack that the enemy attacks itself instead.")]
    public int chancePerStack = 20;
    [Tooltip("Damage of the self-hit, as a part of its normal attack (0.5 = 50%).")]
    public float selfDamage = 0.5f;
    [Tooltip("Turns the enemy can't be hypnotized after it hit itself.")]
    public int immuneTurns = 2;

    public override string DefaultName => "Hypnotize";
    public override string DefaultDescription =>
        $"Add {(stacksPerCast == 1 ? "a Hypnotize stack" : $"{stacksPerCast} Hypnotize stacks")} to the enemy. Each stack gives it a {chancePerStack}% chance to attack itself " +
        $"instead of you (that hit deals {P(selfDamage)} of its normal damage). When it hits itself, the stacks reset and it can't be hypnotized for {N(immuneTurns, "turn")}.";

    public override void Activate(CardRuntime self, CardRuntime enemy) => StatusEffects.ApplyHypnotize(enemy, stacksPerCast, chancePerStack, selfDamage, immuneTurns);
}
