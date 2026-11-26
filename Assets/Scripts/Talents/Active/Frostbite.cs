using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Frostbite")]
public class Frostbite : ActiveTalent
{
    [Tooltip("Enemy SPD lost per stack, in % of its base SPD.")]
    public int spdPercent = 15;
    [Tooltip("Most SPD stacks on one enemy.")]
    public int maxStacks = 3;
    [Tooltip("Chance (%) to Freeze the enemy each cast.")]
    public int freezeChance = 25;

    const string Key = "FrostbiteStacks";

    public override string DefaultName => "Frostbite";
    public override string DefaultDescription =>
        $"Lower the enemy's SPD by {Pct(spdPercent)} of its base SPD (stacks up to {N(maxStacks, "time")}), with a {Pct(freezeChance)} chance to Freeze it: " +
        "a frozen card skips its next turn and can't evade until then.";

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
