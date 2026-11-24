using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Thorns")]
public class Thorns : PassiveTalent
{
    public float reflect = 0.20f;
    public float critReflect = 0.25f;

    public override string DefaultName => "Thorns";
    public override string DefaultDescription => $"{reflect * 100:0}% of the damage you take from normal attacks goes back to the attacker as true damage ({critReflect * 100:0}% from critical hits).";

    public override void OnHitTaken(CardRuntime self, CardRuntime attacker, int damage, bool crit)
    {
        if (damage <= 0 || attacker == null || !attacker.IsAlive) return;
        int back = Mathf.Max(1, Mathf.RoundToInt(damage * Scaled(crit ? critReflect : reflect, self)));
        BattleLog.Passive(self, $"<color=#9CCC65>[Thorns] {back} damage pricks {attacker.DisplayName}</color>");
        attacker.TakeTrueDamage(back, DamageKind.Reflect);
    }
}
