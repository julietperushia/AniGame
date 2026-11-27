using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Rot")]
public class Rot : PassiveTalent
{
    [Tooltip("How much weaker the enemy's healing and lifesteal are (0.5 = 50%).")]
    public float reduction = 0.5f;
    [Tooltip("ATK you gain per point of healing Rot stops (0.05 = 5% of the healing that was cut).")]
    public float atkPerHealingCut = 0.05f;

    public override string DefaultName => "Rot";
    public override string DefaultDescription =>
        $"The enemy's healing and lifesteal are {PctF(reduction, 1f)} weaker for the whole battle." +
        (atkPerHealingCut > 0f ? $" Every time Rot cuts a heal, you permanently gain ATK equal to {PctF(atkPerHealingCut)} of the healing it stopped." : "");

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        enemy.healTakenMultiplier *= Mathf.Clamp01(1f - Scaled(reduction, self));
        enemy.rotSource = self;
        enemy.rotTalent = this;
        enemy.rotAtkShare = Mathf.Max(0f, Scaled(atkPerHealingCut, self));
        BattleLog.Passive(self, $"<color=#9CCC65>[Rot] {enemy.DisplayName}'s healing is weakened</color>");
    }
}
