using UnityEngine;

/// <summary>Passive: every time the enemy uses its active talent, its Mana Regeneration stops for a while.</summary>
[CreateAssetMenu(menuName = "Talents/Passive/Silence")]
public class SilenceTalent : PassiveTalent
{
    [Tooltip("Turns after the cast with no Mana Regeneration (the casting turn's Mana is lost too). 3v3: rounds.")]
    public int regenBlockTurns = 2;

    public override string DefaultName => "Silence";
    public override string DefaultDescription =>
        $"Whenever the enemy uses its active talent, it gets no Mana Regeneration for the rest of that turn and its next {N(regenBlockTurns, "turn")}. " +
        "Mana from being hit or evading still counts.";

    public override void OnEnemySkill(CardRuntime self, CardRuntime enemy)
    {
        if (enemy == null || !enemy.IsAlive || regenBlockTurns <= 0) return;
        if (enemy.ResistsDebuff()) return;
        enemy.manaBlockTurns = Mathf.Max(enemy.manaBlockTurns, regenBlockTurns + 1); // +1: the casting turn itself
        BattleLog.Passive(self, $"<color=#C78BFF>[Silence] {enemy.DisplayName}'s Mana Regeneration stops for {regenBlockTurns} turns</color>");
    }
}
