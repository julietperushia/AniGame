using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Hex")]
public class HexTalent : ActiveTalent
{
    public int turns = 2;

    public override string DefaultName => "Hex";
    public override string DefaultDescription => $"Swaps the enemy's ATK and DEF for {turns} turns.";

    public override void Activate(CardRuntime self, CardRuntime enemy) => StatusEffects.ApplyHex(enemy, turns);
}
