using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Cursed")]
public class Cursed : PassiveTalent
{
    [Tooltip("True damage each time the enemy casts an active, in % of ITS max HP (0.10 = 10%).")]
    public float damagePercent = 0.10f;
    [Tooltip("You heal this part of the damage dealt (1 = 100%).")]
    public float healPercent = 1f;

    public override string DefaultName => "Cursed";
    public override string DefaultDescription =>
        $"Every time the enemy casts an active talent, it takes true damage equal to {PctF(damagePercent)} of its max HP, " +
        (healPercent >= 1f ? "and you heal that much." : $"and you heal {P(healPercent)} of it.");

    public override void OnEnemySkill(CardRuntime self, CardRuntime enemy)
    {
        BattleLog.Passive(self, $"<color=#9575CD>[Cursed] The curse strikes {enemy.DisplayName}!</color>");
        int dealt = enemy.TakeTrueDamage(Mathf.RoundToInt(enemy.maxHealth * Scaled(damagePercent, self)), DamageKind.True);
        if (dealt > 0 && healPercent > 0f) self.Heal(Mathf.RoundToInt(dealt * healPercent));
    }
}
