using UnityEngine;

/// <summary>Asset text: "Roll a 20 sided dice. Enemy familiars have their HP/ATK/DEF/SPD reduced by the amount rolled x 2 at random."
/// HP loss is x10 because HP is stored on the x10 scale.</summary>
[CreateAssetMenu(menuName = "Talents/Active/UnluckyCoin")]
public class UnluckyCoin : ActiveTalent
{
    public int multiplier = 2;

    public override string DefaultName => "Unlucky Coin";
    public override string DefaultDescription => $"Roll a 20 sided dice. The enemy's HP/ATK/DEF/SPD is reduced by the amount rolled x {multiplier} at random.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int roll = BattleRandom.Range(1, 21);
        int reduction = Mathf.RoundToInt(roll * Scaled((float)multiplier, self));
        switch (BattleRandom.Range(0, 4))
        {
            case 0:
                int hp = reduction * 10;
                enemy.maxHealth = Mathf.Max(1, enemy.maxHealth - hp);
                enemy.health = Mathf.Min(enemy.health, enemy.maxHealth);
                BattleLog.Info($"<color=#FF6B6B>[Unlucky Coin] Rolled {roll}! Enemy -{hp} Max HP</color>");
                break;
            case 1:
                enemy.attack -= reduction;
                BattleLog.Info($"<color=#FF6B6B>[Unlucky Coin] Rolled {roll}! Enemy -{reduction} ATK</color>");
                break;
            case 2:
                enemy.defense -= reduction;
                BattleLog.Info($"<color=#FF6B6B>[Unlucky Coin] Rolled {roll}! Enemy -{reduction} DEF</color>");
                break;
            default:
                enemy.speed -= reduction;
                BattleLog.Info($"<color=#FF6B6B>[Unlucky Coin] Rolled {roll}! Enemy -{reduction} SPD</color>");
                break;
        }
    }
}
