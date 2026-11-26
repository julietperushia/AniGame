using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/UnluckyCoin")]
public class UnluckyCoin : ActiveTalent
{
    [Tooltip("Sides of the dice (rolls 1 to this).")]
    public int diceSides = 20;
    [Tooltip("Enemy stat points lost = roll x this.")]
    public int multiplier = 2;

    public override string DefaultName => "Unlucky Coin";
    public override string DefaultDescription =>
        $"Roll a {diceSides}-sided dice: the enemy loses the roll x {Dec(multiplier)} in one random stat: ATK, DEF, SPD or max HP (1 to {NumR(diceSides * (float)multiplier)} points). " +
        "HP points count like the HP stat.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int roll = BattleRandom.Range(1, Mathf.Max(1, diceSides) + 1);
        int reduction = Mathf.RoundToInt(roll * Scaled((float)multiplier, self));
        switch (BattleRandom.Range(0, 4))
        {
            case 0:
                int hp = reduction * GameRules.HpMultiplier;
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
