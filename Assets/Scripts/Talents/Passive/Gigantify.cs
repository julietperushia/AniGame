using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Gigantify")]
public class Gigantify : PassiveTalent
{
    public int hpBonus = 18;
    public float resist = 0.15f;

    public override string DefaultName => "Gigantify";
    public override string DefaultDescription => $"+{hpBonus}% max HP, and a {resist * 100:0}% chance to shrug off each debuff (statuses and stat drops).";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        int gain = Mathf.Max(1, self.baseMaxHealth * Scaled(hpBonus, self) / 100);
        self.maxHealth += gain;
        self.health += gain;
        self.debuffResistChance = Mathf.Min(0.9f, self.debuffResistChance + Scaled(resist, self));
        BattleLog.Passive(self, $"<color=#A1887F>[Gigantify] {self.DisplayName} grows! +{gain} max HP</color>");
    }
}
