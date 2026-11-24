using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Leech")]
public class Leech : PassiveTalent
{
    public float percent = 0.01f;

    public override string DefaultName => "Leech";
    public override string DefaultDescription => $"After every attack (yours and the enemy's): steal {percent * 100:0}% of the enemy's max HP, gain {percent * 100:0}% max HP and heal {percent * 100:0}% max HP.";

    public override void OnAnyAttack(CardRuntime self, CardRuntime enemy, CardRuntime attacker)
    {
        if (!self.IsAlive || !enemy.IsAlive) return;
        float p = Scaled(percent, self);
        int stolen = enemy.TakeTrueDamage(Mathf.Max(1, Mathf.RoundToInt(enemy.maxHealth * p)), DamageKind.True);
        int grow = Mathf.Max(1, Mathf.RoundToInt(self.baseMaxHealth * p));
        self.maxHealth += grow;
        self.Heal(stolen + Mathf.RoundToInt(self.maxHealth * p));
    }
}
