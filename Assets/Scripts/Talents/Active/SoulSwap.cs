using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/SoulSwap")]
public class SoulSwap : ActiveTalent
{
    const string Key = "SoulSwapUsed";

    public override string DefaultName => "Soul Swap";
    public override string DefaultDescription => "Costs 200 mana. If your HP% is lower than the enemy's, you swap HP% with it. Once per battle.";

    public override int GetManaCost(CardRuntime self) => 200;

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.maxMana = Mathf.Max(self.maxMana, 200);
    }

    public override bool CanActivate(CardRuntime self, CardRuntime enemy) =>
        !self.GetTalentState(Key, false) && self.healthPercent < enemy.healthPercent;

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.SetTalentState(Key, true);
        float mine = self.healthPercent, theirs = enemy.healthPercent;
        int myTarget = Mathf.Clamp(Mathf.RoundToInt(theirs * self.maxHealth), 1, self.maxHealth);
        int theirTarget = Mathf.Clamp(Mathf.RoundToInt(mine * enemy.maxHealth), 1, enemy.maxHealth);
        BattleLog.Info($"<color=#F06292>[Soul Swap] {self.DisplayName} and {enemy.DisplayName} trade souls! ({mine * 100:0}% <> {theirs * 100:0}%)</color>");
        if (enemy.health > theirTarget) enemy.TakeTrueDamage(enemy.health - theirTarget, DamageKind.True);
        if (myTarget > self.health) { int gain = myTarget - self.health; self.health = myTarget; BattleLog.Healed(self, gain); }
    }
}
