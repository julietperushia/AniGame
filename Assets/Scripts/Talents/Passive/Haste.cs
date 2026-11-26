using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Haste")]
public class Haste : PassiveTalent
{
    [Tooltip("SPD bonus, in % of base SPD.")]
    public int spdBonus = 15;
    [Tooltip("Chance each turn to attack a second time while you are faster (0.2 = 20%).")]
    public float doubleChance = 0.20f;

    public override string DefaultName => "Haste";
    public override string DefaultDescription =>
        $"+{Pct(spdBonus)} SPD. While your SPD is higher than the enemy's, each of your turns has a {PctF(doubleChance)} chance to attack a second time.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.speed += Mathf.Max(1, self.baseSpeed * Scaled(spdBonus, self) / 100);
        self.hasteChance = Scaled(doubleChance, self);
    }
}
