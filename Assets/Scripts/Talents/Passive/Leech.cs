using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Leech")]
public class Leech : PassiveTalent
{
    [Tooltip("True damage after every normal attack, in % of the enemy's max HP (0.01 = 1%).")]
    public float stealPercent = 0.01f;
    [Tooltip("Max HP you gain after every normal attack, in % of your base max HP (0.01 = 1%).")]
    public float growPercent = 0.01f;

    public override string DefaultName => "Leech";
    public override string DefaultDescription =>
        $"After every normal attack (yours and the enemy's): deal true damage equal to {PctF(stealPercent)} of the enemy's max HP, raise your max HP by {PctF(growPercent)} " +
        "of your base max HP, and heal exactly the damage dealt.";

    public override void OnAnyAttack(CardRuntime self, CardRuntime enemy, CardRuntime attacker)
    {
        if (!self.IsAlive || !enemy.IsAlive) return;
        int stolen = enemy.TakeTrueDamage(Mathf.Max(1, Mathf.RoundToInt(enemy.maxHealth * Scaled(stealPercent, self))), DamageKind.True);
        int grow = Mathf.Max(1, Mathf.RoundToInt(self.baseMaxHealth * Scaled(growPercent, self)));
        self.maxHealth += grow;
        self.Heal(stolen); // only the damage dealt: no extra heal from max HP
    }
}
