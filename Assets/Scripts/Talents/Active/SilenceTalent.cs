using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Silence")]
public class SilenceTalent : ActiveTalent
{
    [Tooltip("Turns of Silence.")]
    public int turns = 3;
    [Tooltip("The silenced enemy's Mana fills this many times as fast while silenced (2 = twice as fast).")]
    public float manaFillMultiplier = 2f;

    public override string DefaultName => "Silence";
    public override string DefaultDescription =>
        $"The enemy can't use its active talent for {N(turns, "turn")}. In exchange, its Mana fills {manaFillMultiplier:0.##}x as fast while silenced, so it casts as soon as the silence ends.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        StatusEffects.ApplySilence(enemy, turns, false);
        if (enemy.silenceTurns > 0) enemy.silenceManaMultiplier = manaFillMultiplier;
    }
}
