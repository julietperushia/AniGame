using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Poison")]
public class Poison : ActiveTalent
{
    public override string DefaultName => "Poison";
    public override string DefaultDescription => "Inflict a stack of Poison: 5 + 5% max HP true damage per turn (10% miss chance, +5% each turn).";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        StatusEffects.ApplyPoison(enemy, 1, Scaled(0.05f, self));
    }
}
