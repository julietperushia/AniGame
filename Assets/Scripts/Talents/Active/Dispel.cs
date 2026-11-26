using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Dispel")]
public class Dispel : ActiveTalent
{
    [Tooltip("True damage per buff removed, in % of your ATK (0.10 = 10%).")]
    public float atkPercentPerStack = 0.10f;
    [Tooltip("Crit rate above this (%) counts as a buff and is removed.")]
    public int critRateFloor = 5;
    [Tooltip("Crit damage above this (%) counts as a buff and is removed.")]
    public int critDamageFloor = 135;
    [Tooltip("Evasion rating above this counts as a buff and is removed.")]
    public int evasionFloor = 5;

    public override string DefaultName => "Dispel";
    public override string DefaultDescription =>
        $"Strip every buff from the enemy: raised ATK / DEF / SPD, timed buffs, crit rate above {critRateFloor}%, crit damage above {critDamageFloor}%, " +
        $"Precision's evasion pierce, evasion rating above {evasionFloor}, Regeneration stacks, Endurance, barriers, lifesteal, debuff immunity and Momentum. " +
        $"Then deal true damage equal to {PctF(atkPercentPerStack)} of your ATK for each buff removed (every Regeneration stack counts).";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int n = 0;
        if (enemy.attack > enemy.baseAttack) { enemy.attack = enemy.baseAttack; n++; }
        if (enemy.defense > enemy.baseDefense) { enemy.defense = enemy.baseDefense; n++; }
        if (enemy.speed > enemy.baseSpeed) { enemy.speed = enemy.baseSpeed; n++; }
        if (enemy.bonusAttack > 0 || enemy.bonusDefense > 0) { enemy.bonusAttack = Mathf.Min(0, enemy.bonusAttack); enemy.bonusDefense = Mathf.Min(0, enemy.bonusDefense); enemy.buffTurnsLeft = 0; n++; }
        if (enemy.critRate > critRateFloor) { enemy.critRate = critRateFloor; n++; }
        if (enemy.critDamage > critDamageFloor) { enemy.critDamage = critDamageFloor; n++; }
        if (enemy.critIgnoresEvasion) { enemy.critIgnoresEvasion = false; n++; }
        if (enemy.evasion > evasionFloor) { enemy.evasion = evasionFloor; n++; }
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
