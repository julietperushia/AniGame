using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Endurance")]
public class Endurance : ActiveTalent
{
    [Tooltip("Turns Endurance lasts.")]
    public int duration = 3;
    [Tooltip("Normal attack damage blocked (0.65 = 65% less damage).")]
    public float reduction = 0.65f;
    [Tooltip("Part of the blocked damage sent back to the attacker as true damage (0.35 = 35%).")]
    public float reflect = 0.35f;

    public override string DefaultName => "Endurance";
    public override string DefaultDescription =>
        $"For {N(duration, "turn")}, take {P(reduction)} less damage from normal attacks, and {PctF(reflect)} of the damage blocked goes back to the attacker as true damage. " +
        "Piercing Edge weakens the reduction.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        StatusEffects.ApplyEndurance(self, duration, Scaled(reflect, self), reduction);
    }
}
