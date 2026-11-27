using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Blackhole")]
public class Blackhole : PassiveTalent
{
    [Tooltip("Part of YOUR OWN stat gains you keep (0.5 = half).")]
    public float ownGainKept = 0.5f;
    [Tooltip("Part of the ENEMY's stat gains it keeps (0 = every gain is swallowed).")]
    public float enemyGainKept = 0f;
    [Tooltip("Part of what is swallowed from the enemy that goes to you (0.3 = 30%).")]
    public float swallowedToYou = 0.3f;

    public override string DefaultName => "Blackhole";
    public override string DefaultDescription =>
        $"While this card is in battle, the enemy {(enemyGainKept <= 0f ? "can't raise its ATK, DEF, SPD or max HP: every stat gain is swallowed" : $"keeps only {P(enemyGainKept)} of every ATK, DEF, SPD or max HP gain")}" +
        (swallowedToYou > 0f && enemyGainKept < 1f ? $". {P(swallowedToYou)} of what it swallows from the enemy goes to you as permanent stats, " : ", ") +
        $"and you keep {(ownGainKept >= 1f ? "all" : P(ownGainKept))} of your own stat gains. Stat drops and healing still work.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy) =>
        BattleLog.Passive(self, $"<color=#9E9E9E>[Blackhole] Enemy stat gains x{enemyGainKept:0.##}, own stat gains x{ownGainKept:0.##}</color>");

    /// <summary>Part of a stat gain the card keeps: its own Blackhole halves it, the opponent's swallows it (the stricter wins).</summary>
    public static float GainKept(CardRuntime card)
    {
        if (card == null) return 1f;
        float kept = 1f;
        foreach (var t in card.talents) if (t is Blackhole b) kept = Mathf.Min(kept, Mathf.Clamp01(b.ownGainKept));
        if (card.opponent != null)
            foreach (var t in card.opponent.talents) if (t is Blackhole b) kept = Mathf.Min(kept, Mathf.Clamp01(b.enemyGainKept));
        return kept;
    }

    /// <summary>The opponent's Blackhole that swallows this card's gains (null if it has none).</summary>
    public static Blackhole EnemyHoleOf(CardRuntime card)
    {
        if (card == null || card.opponent == null) return null;
        foreach (var t in card.opponent.talents) if (t is Blackhole b) return b;
        return null;
    }

    /// <summary>What a stat gain of <paramref name="gain"/> really becomes for this card (Chaotic reverses it, Blackhole cuts it).</summary>
    public static int EffectiveGain(CardRuntime card, int gain)
    {
        if (card == null || gain <= 0) return gain;
        bool chaotic = card.HasTalent<Chaotic>() || (card.opponent != null && card.opponent.HasTalent<Chaotic>());
        if (chaotic) return -gain;
        return Mathf.RoundToInt(gain * GainKept(card));
    }
}
