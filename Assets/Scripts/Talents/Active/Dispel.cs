using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Dispel")]
public class Dispel : ActiveTalent
{
    public float atkPercentPerStack = 0.10f;

    public override string DefaultName => "Dispel";
    public override string DefaultDescription => $"Strips every buff and stack from the enemy (stat boosts, Precision, Evasion, Regeneration, timed buffs, Endurance, shields, lifesteal). Deals true damage of {atkPercentPerStack * 100:0}% of your ATK for each one removed.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int n = 0;
        if (enemy.attack > enemy.baseAttack) { enemy.attack = enemy.baseAttack; n++; }
        if (enemy.defense > enemy.baseDefense) { enemy.defense = enemy.baseDefense; n++; }
        if (enemy.speed > enemy.baseSpeed) { enemy.speed = enemy.baseSpeed; n++; }
        if (enemy.bonusAttack > 0 || enemy.bonusDefense > 0) { enemy.bonusAttack = Mathf.Min(0, enemy.bonusAttack); enemy.bonusDefense = Mathf.Min(0, enemy.bonusDefense); enemy.buffTurnsLeft = 0; n++; }
        if (enemy.critRate > 5) { enemy.critRate = 5; n++; }
        if (enemy.critDamage > 135) { enemy.critDamage = 135; n++; }
        if (enemy.critIgnoresEvasion) { enemy.critIgnoresEvasion = false; n++; }
        if (enemy.evasion > 5) { enemy.evasion = 5; n++; }
        if (enemy.regenerationStacks > 0) { n += enemy.regenerationStacks; enemy.regenerationStacks = 0; }
        if (enemy.hasEndurance) { enemy.hasEndurance = false; enemy.enduranceTurns = 0; n++; }
        if (enemy.shield > 0) { enemy.shield = 0; enemy.shieldTurns = 0; enemy.shieldBurst = 0; n++; }
        if (enemy.lifesteal > 0) { enemy.lifesteal = 0; n++; }
        if (enemy.debuffImmuneTurns > 0) { enemy.debuffImmuneTurns = 0; n++; }
        if (enemy.momentumStacks > 0) { enemy.momentumStacks = 0; n++; }
        BattleLog.Info($"<color=#B39DDB>[Dispel] {n} buff(s) stripped from {enemy.DisplayName}</color>");
        if (n > 0) enemy.TakeTrueDamage(DamageCalculator.FromStat(self.TotalAttack, Scaled(atkPercentPerStack, self) * n), DamageKind.Skill);
    }
}
