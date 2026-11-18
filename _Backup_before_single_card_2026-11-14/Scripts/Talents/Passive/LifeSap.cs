using UnityEngine;

/// <summary>Anigame: "Every round, deal 2% of the enemy familiars' max HP as damage, and heal for 175% of the damage dealt".</summary>
[CreateAssetMenu(menuName = "Talents/Passive/LifeSap")]
public class LifeSap : PassiveTalent
{
    public int damagePercent = 2;
    public int healMultiplier = 175;

    public override string DefaultName => "Life Sap";
    public override string DefaultDescription => $"Every turn, deal {damagePercent}% of the enemy's max HP as damage and heal for {healMultiplier}% of the damage dealt.";

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        int dmg = Mathf.Max(1, enemy.maxHealth * damagePercent / 100);
        int dealt = enemy.TakeDamage(dmg, DamageKind.Skill);
        if (dealt > 0) self.Heal(dealt * healMultiplier / 100);
    }
}
