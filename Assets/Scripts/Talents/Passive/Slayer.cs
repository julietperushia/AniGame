using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Slayer")]
public class Slayer : PassiveTalent
{
    [Tooltip("Extra damage per normal attack, in % of the enemy's max HP (0.10 = 10%).")]
    public float maxHpPercent = 0.10f;

    public override string DefaultName => "Slayer";
    public override string DefaultDescription => $"Each of your normal attacks deals extra damage equal to {PctF(maxHpPercent)} of the enemy's max HP.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy) => self.slayerPercent = Scaled(maxHpPercent, self);
}
