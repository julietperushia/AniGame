using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Silence")]
public class SilenceTalent : ActiveTalent
{
    public int turns = 3;

    public override string DefaultName => "Silence";
    public override string DefaultDescription => $"The enemy can't use its active talent for {turns} turns. In exchange its mana fills twice as fast while silenced, so it casts as soon as the silence ends.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        StatusEffects.ApplySilence(enemy, turns, false);
        if (enemy.silenceTurns > 0) enemy.silenceManaBoost = true;
    }
}
