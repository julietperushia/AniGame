using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Endurance")]
public class Endurance : ActiveTalent
{
    public int duration = 3;

    public override string DefaultName => "Endurance";
    public override string DefaultDescription => $"For {duration} turns take 65% less damage from normal attacks and reflect 35% of the blocked damage as true damage.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        StatusEffects.ApplyEndurance(self, duration);
    }
}
