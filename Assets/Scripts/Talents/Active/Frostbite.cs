using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Frostbite")]
public class Frostbite : ActiveTalent
{
    [Tooltip("Enemy SPD lost per stack, in % of its base SPD.")]
    public int spdPercent = 15;
    [Tooltip("Most SPD stacks on one enemy.")]
    public int maxStacks = 3;
    [Tooltip("Chance (%) to Freeze an enemy at full SPD (after this cast's slow). It grows as the enemy's SPD drops.")]
    public int freezeChance = 25;
    [Tooltip("Highest Freeze chance (%), reached when the enemy's SPD is at or below the next value.")]
    public int maxFreezeChance = 60;
    [Tooltip("Enemy SPD (in % of its base SPD) at or below which the Freeze chance is at its highest.")]
    public int maxChanceAtSpeedPercent = 35;

    const string Key = "FrostbiteStacks";

    public override string DefaultName => "Frostbite";
    public override string DefaultDescription =>
        $"Lower the enemy's SPD by {Pct(spdPercent)} of its base SPD (stacks up to {N(maxStacks, "time")}), then try to Freeze it: " +
        $"{Pct(freezeChance)} chance at full SPD, growing as its SPD drops, up to {maxFreezeChance}% once its SPD is {maxChanceAtSpeedPercent}% of its base SPD or lower. " +
        "A frozen card skips its next turn and can't evade until then.";

    /// <summary>Freeze chance against this enemy now: from the base chance at full SPD up to the max at the SPD threshold.</summary>
    public int FreezeChanceVs(CardRuntime self, CardRuntime enemy)
    {
        float baseChance = Scaled((float)freezeChance, self);
        float ratio = enemy.baseSpeed > 0 ? enemy.TotalSpeed / (float)enemy.baseSpeed : 1f;
        float floor = Mathf.Clamp(maxChanceAtSpeedPercent / 100f, 0f, 0.99f);
        float t = Mathf.Clamp01((1f - ratio) / (1f - floor));
        return Mathf.RoundToInt(Mathf.Lerp(baseChance, Mathf.Max(baseChance, maxFreezeChance), t));
    }

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
        int chance = FreezeChanceVs(self, enemy);
        if (BattleRandom.Chance(chance)) StatusEffects.ApplyFreeze(enemy);
        else BattleLog.Info($"<color=#81D4FA>[Frostbite] No freeze this time ({chance}% chance)</color>");
    }
}
