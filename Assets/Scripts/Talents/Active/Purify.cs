using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Purify")]
public class Purify : ActiveTalent
{
    [Tooltip("Turns you can't get new debuffs after the cast.")]
    public int immuneTurns = 2;
    [Tooltip("Heal per effect removed, in % of your max HP (0.05 = 5%).")]
    public float healPerEffect = 0.05f;

    public override string DefaultName => "Purify";
    public override string DefaultDescription =>
        "Remove every Burn, Poison, Stun, Freeze, Sleep, Blind, Hex, Hypnotize, Silence, Mana Regeneration stop and stat drop on you. " +
        $"You can't get new debuffs for {N(immuneTurns, "turn")}, and you heal {PctF(healPerEffect)} of your max HP for each effect removed.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int removed = StatusEffects.Cleanse(self);
        self.debuffImmuneTurns = Mathf.Max(self.debuffImmuneTurns, immuneTurns);
        BattleLog.Info($"<color=#E0F7FA>[Purify] {self.DisplayName} removes {removed} effect(s) and is immune for {immuneTurns} turns</color>");
        if (removed > 0) self.Heal(Mathf.RoundToInt(self.maxHealth * Scaled(healPerEffect, self) * removed));
    }
}
