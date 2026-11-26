using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Hex")]
public class HexTalent : ActiveTalent
{
    [Tooltip("Turns the enemy's ATK and DEF stay swapped.")]
    public int turns = 2;

    public override string DefaultName => "Hex";
    public override string DefaultDescription => $"Swap the enemy's ATK and DEF for {N(turns, "turn")}. They swap back when the Hex ends.";

    public override void Activate(CardRuntime self, CardRuntime enemy) => StatusEffects.ApplyHex(enemy, turns);
}
