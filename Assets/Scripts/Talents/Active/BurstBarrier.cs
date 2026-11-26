using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/BurstBarrier")]
public class BurstBarrier : ActiveTalent
{
    [Tooltip("Barrier size in % of your max HP (0.15 = 15%).")]
    public float shieldPercent = 0.15f;
    [Tooltip("Turns the barrier lasts (counted on your turns).")]
    public int turns = 3;
    [Tooltip("True damage of the explosion when the barrier breaks, in % of your ATK (0.5 = 50%).")]
    public float burstAtkPercent = 0.5f;
    [Tooltip("Mana Regeneration lost while the barrier is up (0.5 = -50%), so it can't be raised again right away.")]
    public float manaRegenCut = 0.5f;

    public override string DefaultName => "Burst Barrier";
    public override string DefaultDescription =>
        $"Raise a barrier worth {PctF(shieldPercent)} of your max HP for {N(turns, "turn")}. It absorbs all damage before your HP. " +
        $"If it breaks, it explodes and deals true damage equal to {PctF(burstAtkPercent)} of your ATK to the enemy; if it lasts, it fades without exploding. " +
        (manaRegenCut > 0f ? $"While the barrier is up, your Mana Regeneration is reduced by {P(manaRegenCut)}. " : "") +
        "Casting again refreshes the time and keeps the bigger barrier.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.shield = Mathf.Max(self.shield, Mathf.RoundToInt(self.maxHealth * Scaled(shieldPercent, self)));
        self.shieldTurns = turns;
        self.shieldBurst = DamageCalculator.FromStat(self.TotalAttack, Scaled(burstAtkPercent, self));
        self.shieldManaRegen = 1f - Mathf.Clamp01(manaRegenCut);
        BattleLog.Info($"<color=#6FD3FF>[Burst Barrier] {self.DisplayName} raises a {self.shield} HP barrier</color>");
    }
}
