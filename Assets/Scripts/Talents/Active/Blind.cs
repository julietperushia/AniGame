using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Blind")]
public class Blind : ActiveTalent
{
    public int turns = 2;

    public override string DefaultName => "Blind";
    public override string DefaultDescription => $"Blinds the enemy for {turns} turns: each of its normal attacks has a 50% chance to miss.";

    public override void Activate(CardRuntime self, CardRuntime enemy) => StatusEffects.ApplyBlind(enemy, turns);
}
