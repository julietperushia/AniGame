using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/CelestialInfluence")]
public class CelestialInfluence : PassiveTalent
{
    [Tooltip("Your Mana Regeneration bonus (% points).")]
    public int regenBonusPercent = 40;
    [Tooltip("The enemy's Mana Regeneration loss (% points).")]
    public int penaltyPercent = 14;

    public override string DefaultName => "Celestial Influence";
    public override string DefaultDescription =>
        $"Start the battle with +{Pct(regenBonusPercent)} Mana Regeneration, while the enemy's Mana Regeneration is reduced by {Pct(penaltyPercent)}.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.manaRegen += Scaled(regenBonusPercent, self);
        enemy.manaRegen = Mathf.Max(0, enemy.manaRegen - Scaled(penaltyPercent, self));
        BattleLog.Passive(self, $"<color=#C78BFF>[Celestial Influence] Mana Regen {self.manaRegen}% vs {enemy.manaRegen}%</color>");
    }
}
