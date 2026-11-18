using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/BalancingStrike")]
public class BalancingStrike : ActiveTalent
{
    public float lowHpPercent = 0.15f;
    public float highHpPercent = 0.05f;

    public override string DefaultName => "Balancing Strike";
    public override string DefaultDescription => $"Deal true damage equal to {lowHpPercent * 100:0}% of your max HP if your HP% is lower than the enemy's, otherwise {highHpPercent * 100:0}%.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        bool behind = self.healthPercent < enemy.healthPercent;
        int damage = Mathf.Max(1, Mathf.RoundToInt(self.maxHealth * Scaled(behind ? lowHpPercent : highHpPercent, self)));
        BattleLog.Info($"<color=#FFE14D>[Balancing Strike] {(behind ? "Behind on HP - empowered!" : "Ahead on HP")}</color>");
        self.lastDamageDealt = enemy.TakeTrueDamage(damage, DamageKind.True);
    }
}
