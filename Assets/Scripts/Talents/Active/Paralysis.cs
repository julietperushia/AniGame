using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Paralysis")]
public class Paralysis : ActiveTalent
{
    [Tooltip("Chance (%) to Stun the enemy each cast.")]
    public int stunChance = 40;
    [Tooltip("DEF the stunned enemy loses for good, in % of its base DEF.")]
    public int defLossPercent = 10;

    public override string DefaultName => "Paralysis";
    public override string DefaultDescription =>
        $"{Pct(stunChance)} chance to Stun the enemy: it skips its next turn and permanently loses {defLossPercent}% of its base DEF.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        if (BattleRandom.Chance(Scaled(stunChance, self)))
            StatusEffects.ApplyStun(enemy, defLossPercent);
        else
            BattleLog.Info($"<color=#FFE14D>[Paralysis] The stun failed ({stunChance}% chance)</color>");
    }
}
