using UnityEngine;

/// <summary>Anigame: "Deals [element] damage equal to 5% of your SPD, plus extra damage based on the SPD difference from the enemy."</summary>
[CreateAssetMenu(menuName = "Talents/Active/DexterityDrive")]
public class DexterityDrive : ActiveTalent
{
    public float spdPercent = 0.05f;
    [Tooltip("Damage per point of SPD you have above the enemy.")]
    public int damagePerSpeedPoint = 5;

    public override string DefaultName => "Dexterity Drive";
    public override string DefaultDescription => $"Deal elemental damage equal to {spdPercent * 100:0}% of your SPD, plus {damagePerSpeedPoint} per point of SPD you have over the enemy.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int baseDamage = DamageCalculator.FromStat(self.TotalSpeed, Scaled(spdPercent, self));
        int bonusDamage = Mathf.Max(0, self.TotalSpeed - enemy.TotalSpeed) * Scaled(damagePerSpeedPoint, self);
        int total = DamageCalculator.WithElement(baseDamage + bonusDamage, self, enemy);
        BattleLog.Info($"<color=#6FD3FF>[Dexterity Drive] base {baseDamage} + speed bonus {bonusDamage}</color>");
        self.lastDamageDealt = enemy.TakeDamage(total, DamageKind.Skill);
    }
}
