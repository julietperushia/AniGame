using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Frostbite")]
public class Frostbite : ActiveTalent
{
    public int spdPercent = 15;
    public int maxStacks = 3;
    public int freezeChance = 25;
    const string Key = "FrostbiteStacks";

    public override string DefaultName => "Frostbite";
    public override string DefaultDescription => $"Lowers the enemy's SPD by {spdPercent}% (stacks up to {maxStacks} times) and has a {freezeChance}% chance to Freeze it: a frozen card skips its next turn.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int stacks = enemy.GetTalentState(Key, 0);
        if (stacks < maxStacks)
        {
            int loss = Mathf.Max(1, enemy.baseSpeed * Scaled(spdPercent, self) / 100);
            enemy.speed = Mathf.Max(1, enemy.speed - loss);
            enemy.SetTalentState(Key, stacks + 1);
            BattleLog.Info($"<color=#81D4FA>[Frostbite] {enemy.DisplayName} SPD -{loss} (x{stacks + 1})</color>");
        }
        if (BattleRandom.Chance(Mathf.RoundToInt(Scaled((float)freezeChance, self)))) StatusEffects.ApplyFreeze(enemy);
    }
}
