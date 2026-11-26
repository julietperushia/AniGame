using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/LifeSap")]
public class LifeSap : PassiveTalent
{
    [Tooltip("Damage at the start of each of your turns, in % of the enemy's max HP.")]
    public int damagePercent = 2;
    [Tooltip("Heal, in % of the damage dealt (175 = 1.75x).")]
    public int healMultiplier = 175;

    public override string DefaultName => "Life Sap";
    public override string DefaultDescription =>
        $"At the start of each of your turns, deal damage equal to {PctF(damagePercent / 100f)} of the enemy's max HP and heal {healMultiplier}% of the damage dealt.";

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        int dmg = Mathf.Max(1, Mathf.RoundToInt(enemy.maxHealth * Scaled((float)damagePercent, self) / 100f));
        int dealt = enemy.TakeDamage(dmg, DamageKind.Skill);
        if (dealt > 0) self.Heal(dealt * healMultiplier / 100);
    }
}
