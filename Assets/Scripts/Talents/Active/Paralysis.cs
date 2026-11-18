using UnityEngine;

/// <summary>Anigame: "Gain a 40% chance to inflict a stack of Stun to enemy familiars, disabling their next turn, as well as permanently decreasing their DEF by 10%".</summary>
[CreateAssetMenu(menuName = "Talents/Active/Paralysis")]
public class Paralysis : ActiveTalent
{
    public int stunChance = 40;

    public override string DefaultName => "Paralysis";
    public override string DefaultDescription => $"{stunChance}% chance to Stun the enemy: it skips its next turn and loses 10% DEF permanently.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        if (BattleRandom.Chance(Scaled(stunChance, self)))
        {
            StatusEffects.ApplyStun(enemy);
        }
        else
        {
            BattleLog.Info($"<color=#FFE14D>[Paralysis] The stun failed ({stunChance}% chance)</color>");
        }
    }
}
