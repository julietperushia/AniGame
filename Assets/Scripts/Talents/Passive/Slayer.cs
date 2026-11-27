using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Talents/Passive/Slayer")]
public class Slayer : PassiveTalent
{
    [Tooltip("Extra damage per normal attack, in % of the enemy's CURRENT HP (0.10 = 10%).")]
    [FormerlySerializedAs("maxHpPercent")]
    public float currentHpPercent = 0.10f;

    public override string DefaultName => "Slayer";
    public override string DefaultDescription =>
        $"Each of your normal attacks deals extra damage equal to {PctF(currentHpPercent)} of the enemy's current HP (strongest against a healthy enemy).";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy) => self.slayerPercent = Scaled(currentHpPercent, self);
}
