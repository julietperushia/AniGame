using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/LuckyCoin")]
public class LuckyCoin : ActiveTalent
{
    [Tooltip("Sides of the dice (rolls 1 to this).")]
    public int diceSides = 20;
    [Tooltip("Stat points gained = roll x this.")]
    public int multiplier = 3;

    public override string DefaultName => "Lucky Coin";
    public override string DefaultDescription =>
        $"Roll a {diceSides}-sided dice and gain the roll x {Dec(multiplier)} in one random stat: ATK, DEF, SPD or HP (1 to {NumR(diceSides * (float)multiplier)} points). " +
        "HP points count like the HP stat and are healed at once.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int roll = BattleRandom.Range(1, Mathf.Max(1, diceSides) + 1);
        int bonus = Mathf.RoundToInt(roll * Scaled((float)multiplier, self));
        switch (BattleRandom.Range(0, 4))
        {
            case 0:
                int hp = bonus * GameRules.HpMultiplier;
                self.maxHealth += hp;
                BattleLog.Info($"<color=#7CFC6A>[Lucky Coin] Rolled {roll}! +{hp} Max HP</color>");
                self.Heal(hp);
                break;
            case 1:
                self.attack += bonus;
                BattleLog.Info($"<color=#7CFC6A>[Lucky Coin] Rolled {roll}! +{bonus} ATK</color>");
                break;
            case 2:
                self.defense += bonus;
                BattleLog.Info($"<color=#7CFC6A>[Lucky Coin] Rolled {roll}! +{bonus} DEF</color>");
                break;
            default:
                self.speed += bonus;
                BattleLog.Info($"<color=#7CFC6A>[Lucky Coin] Rolled {roll}! +{bonus} SPD</color>");
                break;
        }
    }
}
