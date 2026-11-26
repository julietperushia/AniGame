using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Rot")]
public class Rot : PassiveTalent
{
    [Tooltip("How much weaker the enemy's healing and lifesteal are (0.5 = 50%).")]
    public float reduction = 0.5f;

    public override string DefaultName => "Rot";
    public override string DefaultDescription => $"The enemy's healing and lifesteal are {PctF(reduction, 1f)} weaker for the whole battle.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        enemy.healTakenMultiplier *= Mathf.Clamp01(1f - Scaled(reduction, self));
        BattleLog.Passive(self, $"<color=#9CCC65>[Rot] {enemy.DisplayName}'s healing is weakened</color>");
    }
}
