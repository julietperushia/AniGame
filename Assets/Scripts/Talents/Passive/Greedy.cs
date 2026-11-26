using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Greedy")]
public class Greedy : PassiveTalent
{
    [Tooltip("Part of every enemy heal that heals you instead (1 = all of it).")]
    public float stealPercent = 1f;

    public override string DefaultName => "Greedy";
    public override string DefaultDescription =>
        stealPercent >= 1f
            ? "All of the enemy's healing (talents, regeneration, lifesteal) heals you instead."
            : $"{P(stealPercent)} of every heal the enemy gets (talents, regeneration, lifesteal) heals you instead.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy) => self.greedyPercent = Mathf.Clamp01(stealPercent);
}
