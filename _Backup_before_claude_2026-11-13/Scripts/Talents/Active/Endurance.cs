using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Endurance")]
public class Endurance : ActiveTalent
{
    public int duration = 3;

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        StatusEffects.ApplyEndurance(self, duration);
        Debug.Log($"<color=cyan>[Endurance] Taking 65% less damage from normal attacks for {duration} turns. 35% of reduced damage reflected!</color>");
    }
}
