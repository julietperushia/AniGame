using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Slayer")]
public class Slayer : PassiveTalent
{
    public float maxHpPercent = 0.10f;

    public override string DefaultName => "Slayer";
    public override string DefaultDescription => $"Your attacks deal extra damage equal to {maxHpPercent * 100:0}% of the enemy's max HP.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy) => self.slayerPercent = Scaled(maxHpPercent, self);
}
