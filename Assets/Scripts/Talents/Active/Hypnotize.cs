using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Hypnotize")]
public class Hypnotize : ActiveTalent
{
    public override string DefaultName => "Hypnotize";
    public override string DefaultDescription => "Adds a Hypnotize stack to the enemy. Each stack gives it +20% chance to attack itself instead of you (that hit deals 50% damage). After it triggers, the stacks reset and the enemy can't be hypnotized for 2 turns.";

    public override void Activate(CardRuntime self, CardRuntime enemy) => StatusEffects.ApplyHypnotize(enemy);
}
