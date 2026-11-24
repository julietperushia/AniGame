using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Greedy")]
public class Greedy : PassiveTalent
{
    public override string DefaultName => "Greedy";
    public override string DefaultDescription => "All of the enemy's healing (talents, regeneration, lifesteal) heals you instead.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy) => self.greedy = true;
}
