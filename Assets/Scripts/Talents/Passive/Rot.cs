using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Rot")]
public class Rot : PassiveTalent
{
    public float reduction = 0.5f;

    public override string DefaultName => "Rot";
    public override string DefaultDescription => $"The enemy's healing and lifesteal are {reduction * 100:0}% weaker for the whole battle.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        enemy.healTakenMultiplier *= Mathf.Clamp01(1f - Scaled(reduction, self));
        BattleLog.Passive(self, $"<color=#9CCC65>[Rot] {enemy.DisplayName}'s healing is weakened</color>");
    }
}
