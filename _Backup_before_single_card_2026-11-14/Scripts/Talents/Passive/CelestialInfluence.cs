using UnityEngine;

/// <summary>Asset text: "Start off the battle with 40% increased Mana Regen, while decreasing enemy familiars' Mana Regen by 14%."
/// Fixed: the old version had a constructor with parameters (ScriptableObjects need a parameterless one) and did nothing.</summary>
[CreateAssetMenu(menuName = "Talents/Passive/CelestialInfluence")]
public class CelestialInfluence : PassiveTalent
{
    public int regenBonusPercent = 40;
    public int penaltyPercent = 14;

    public override string DefaultName => "Celestial Influence";
    public override string DefaultDescription => $"Start the battle with +{regenBonusPercent}% Mana Regen, while the enemy's Mana Regen is reduced by {penaltyPercent}%.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.manaRegen += regenBonusPercent;
        enemy.manaRegen = Mathf.Max(0, enemy.manaRegen - penaltyPercent);
        BattleLog.Passive(self, $"<color=#C78BFF>[Celestial Influence] Mana Regen {self.manaRegen}% vs {enemy.manaRegen}%</color>");
    }
}
