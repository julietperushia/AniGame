using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Blind")]
public class Blind : ActiveTalent
{
    [Tooltip("Turns the enemy stays blinded.")]
    public int turns = 2;
    [Tooltip("Chance (%) that each of the blinded enemy's normal attacks misses.")]
    public int missChance = 50;

    public override string DefaultName => "Blind";
    public override string DefaultDescription => $"Blind the enemy for {N(turns, "turn")}: each of its normal attacks has a {missChance}% chance to miss.";

    public override void Activate(CardRuntime self, CardRuntime enemy) => StatusEffects.ApplyBlind(enemy, turns, missChance);
}
