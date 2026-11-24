using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Purify")]
public class Purify : ActiveTalent
{
    public int immuneTurns = 2;
    public float healPerEffect = 0.05f;

    public override string DefaultName => "Purify";
    public override string DefaultDescription => $"Removes every Burn, Poison, Stun, Freeze, Sleep, Blind, Hex, Hypnotize, Silence and stat drop on you. You can't get new debuffs for {immuneTurns} turns, and you heal {healPerEffect * 100:0}% max HP for each effect removed.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int removed = StatusEffects.Cleanse(self);
        self.debuffImmuneTurns = Mathf.Max(self.debuffImmuneTurns, immuneTurns);
        BattleLog.Info($"<color=#E0F7FA>[Purify] {self.DisplayName} removes {removed} effect(s) and is immune for {immuneTurns} turns</color>");
        if (removed > 0) self.Heal(Mathf.RoundToInt(self.maxHealth * Scaled(healPerEffect, self) * removed));
    }
}
