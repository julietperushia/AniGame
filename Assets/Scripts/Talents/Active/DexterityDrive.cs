using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/DexterityDrive")]
public class DexterityDrive : ActiveTalent
{
    [Tooltip("SPD gained for the rest of the battle on every cast, before the hit, in % of base SPD.")]
    public int speedGainPercent = 10;
    [Tooltip("Base damage in % of your SPD (0.05 = 5%).")]
    public float spdPercent = 0.05f;
    [Tooltip("Extra damage per point of SPD you have above the enemy.")]
    public int damagePerSpeedPoint = 5;

    public override string DefaultName => "Dexterity Drive";
    public override string DefaultDescription =>
        $"Raise your SPD by {Pct(speedGainPercent)} of your base SPD for the rest of the battle, then deal damage of your element equal to {PctF(spdPercent)} of your SPD, " +
        $"plus {Num(damagePerSpeedPoint)} damage for every point of SPD you have above the enemy. Element advantage applies. Every cast makes you faster, so the hits keep growing.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        // The SPD gain goes through the stat rules after this call (Blackhole, Chaotic...): count it the same way here.
        int gain = Mathf.Max(0, self.baseSpeed * Scaled(speedGainPercent, self) / 100);
        self.speed += gain;
        int speed = Mathf.Max(1, self.TotalSpeed - gain + Blackhole.EffectiveGain(self, gain));
        int baseDamage = DamageCalculator.FromStat(speed, Scaled(spdPercent, self));
        int bonusDamage = Mathf.Max(0, speed - enemy.TotalSpeed) * Scaled(damagePerSpeedPoint, self);
        int total = DamageCalculator.WithElement(baseDamage + bonusDamage, self, enemy);
        BattleLog.Info($"<color=#6FD3FF>[Dexterity Drive] SPD {speed} vs {enemy.TotalSpeed}: base {baseDamage} + speed bonus {bonusDamage}</color>");
        self.lastDamageDealt = enemy.TakeDamage(total, DamageKind.Skill);
    }
}
