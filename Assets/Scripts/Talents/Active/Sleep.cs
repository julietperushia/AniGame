using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Sleep")]
public class Sleep : ActiveTalent
{
    public int turns = 2;

    public override string DefaultName => "Sleep";
    public override string DefaultDescription => $"Puts the enemy to sleep for up to {turns} turns; it skips them. A sleeping card can't evade, and the normal attack that wakes it deals +40% damage.";

    public override void Activate(CardRuntime self, CardRuntime enemy) => StatusEffects.ApplySleep(enemy, turns);
}
