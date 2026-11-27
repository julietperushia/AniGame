using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Nothingness")]
public class Nothingness : PassiveTalent
{
    [Tooltip("True damage a card takes when it gets Nothingness from another talent (Mimic, Gamble...), in % of its max HP (0.3 = 30%).")]
    public float copyPenalty = 0.3f;

    public override string DefaultName => "Nothingness";
    public override string DefaultDescription =>
        $"Does nothing by itself... but any card that gets it from another talent (copied by Mimic, drawn by Gamble, or any other way) " +
        $"instantly takes true damage equal to {P(copyPenalty)} of its max HP.";

    /// <summary>Every way of getting Nothingness in battle (Mimic, Gamble...) goes through BattleEngine.InitTalent, which calls this.</summary>
    public override void OnLearnedInBattle(CardRuntime self, CardRuntime enemy)
    {
        if (self == null || !self.IsAlive || copyPenalty <= 0f) return;
        BattleLog.Info($"<color=#9E9E9E>[Nothingness] {self.DisplayName} took nothing... and it has a price: {copyPenalty * 100f:0}% max HP!</color>");
        self.TakeTrueDamage(Mathf.Max(1, Mathf.RoundToInt(self.maxHealth * copyPenalty)), DamageKind.True);
    }
}
