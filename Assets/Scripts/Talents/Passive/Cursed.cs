using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Cursed")]
public class Cursed : PassiveTalent
{
    public float damagePercent = 0.10f;

    public override string DefaultName => "Cursed";
    public override string DefaultDescription => $"Every time the enemy casts an active talent, it takes true damage equal to {damagePercent * 100:0}% of its max HP and you heal that much.";

    public override void OnEnemySkill(CardRuntime self, CardRuntime enemy)
    {
        BattleLog.Passive(self, $"<color=#9575CD>[Cursed] The curse strikes {enemy.DisplayName}!</color>");
        int dealt = enemy.TakeTrueDamage(Mathf.RoundToInt(enemy.maxHealth * Scaled(damagePercent, self)), DamageKind.True);
        if (dealt > 0) self.Heal(dealt);
    }
}
