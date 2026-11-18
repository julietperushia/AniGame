using UnityEngine;

/// <summary>Anigame: "Inflict a stack of Burn to enemy familiars, dealing 10% True damage per turn, as well as reducing all healing effects on them by 75%."</summary>
[CreateAssetMenu(menuName = "Talents/Active/Blaze")]
public class Blaze : ActiveTalent
{
    public override string DefaultName => "Blaze";
    public override string DefaultDescription => "Inflict a stack of Burn: 5% max HP true damage per turn per stack (max 4 stacks) and -75% healing received.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        StatusEffects.ApplyBurn(enemy, 1);
    }
}
