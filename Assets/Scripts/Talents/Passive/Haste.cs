using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Haste")]
public class Haste : PassiveTalent
{
    public int spdBonus = 15;
    public float doubleChance = 0.20f;

    public override string DefaultName => "Haste";
    public override string DefaultDescription => $"+{spdBonus}% SPD. While you are faster than the enemy, each turn has a {doubleChance * 100:0}% chance to attack twice.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.speed += Mathf.Max(1, self.baseSpeed * Scaled(spdBonus, self) / 100);
        self.hasteChance = Scaled(doubleChance, self);
    }
}
