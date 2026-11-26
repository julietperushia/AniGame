using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Gigantify")]
public class Gigantify : PassiveTalent
{
    [Tooltip("Max HP bonus, in % of base max HP.")]
    public int hpBonus = 18;
    [Tooltip("Chance to shrug off each debuff (0.15 = 15%).")]
    public float resist = 0.15f;
    [Tooltip("The resist chance can't go above this (0.9 = 90%).")]
    public float maxResist = 0.9f;

    public override string DefaultName => "Gigantify";
    public override string DefaultDescription =>
        $"+{Pct(hpBonus)} max HP, and a {PctF(resist, maxResist)} chance to shrug off each debuff (statuses and stat drops from the enemy).";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        int gain = Mathf.Max(1, self.baseMaxHealth * Scaled(hpBonus, self) / 100);
        self.maxHealth += gain;
        self.health += gain;
        self.debuffResistChance = Mathf.Min(maxResist, self.debuffResistChance + Scaled(resist, self));
        BattleLog.Passive(self, $"<color=#A1887F>[Gigantify] {self.DisplayName} grows! +{gain} max HP</color>");
    }
}
