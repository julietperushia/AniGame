using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Sleep")]
public class Sleep : ActiveTalent
{
    [Tooltip("Turns the enemy sleeps. Attacks don't wake it up.")]
    public int turns = 2;
    [Tooltip("Normal attacks on the sleeping enemy deal this much less damage (0.3 = -30%).")]
    public float attackDamageCut = 0.3f;
    [Tooltip("Your Mana Regeneration while the enemy sleeps (0.5 = half), so you can't chain Sleep.")]
    public float manaRegenWhileAsleep = 0.5f;

    public override string DefaultName => "Sleep";
    public override string DefaultDescription =>
        $"Put the enemy to sleep for {N(turns, "turn")}: it skips them and can't evade, and attacks don't wake it up. " +
        $"While it sleeps, your normal attacks on it deal {P(attackDamageCut)} less damage and your Mana Regeneration is {(manaRegenWhileAsleep <= 0f ? "stopped" : $"cut to {P(manaRegenWhileAsleep)}")}.";

    public override void Activate(CardRuntime self, CardRuntime enemy) =>
        StatusEffects.ApplySleep(enemy, turns, attackDamageCut, manaRegenWhileAsleep);
}
