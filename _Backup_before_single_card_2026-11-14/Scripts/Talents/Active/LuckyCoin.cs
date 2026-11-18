using UnityEngine;

/// <summary>Asset text: "Roll a 20 sided dice. Your allied familiars gain the amount rolled x 3 HP/ATK/DEF/SPD at random."
/// HP gains are x10 because HP is stored on the x10 scale.</summary>
[CreateAssetMenu(menuName = "Talents/Active/LuckyCoin")]
public class LuckyCoin : ActiveTalent
{
    public int multiplier = 3;

    public override string DefaultName => "Lucky Coin";
    public override string DefaultDescription => $"Roll a 20 sided dice. Gain the amount rolled x {multiplier} HP/ATK/DEF/SPD at random.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int roll = BattleRandom.Range(1, 21);
        int bonus = Mathf.RoundToInt(roll * multiplier * CardStats.GlobalStatScale);
        switch (BattleRandom.Range(0, 4))
        {
            case 0:
                int hp = bonus * 10;
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
