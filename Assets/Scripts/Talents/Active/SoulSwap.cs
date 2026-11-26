using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/SoulSwap")]
public class SoulSwap : ActiveTalent
{
    [Tooltip("How many times it can be used per battle.")]
    public int usesPerBattle = 1;

    [SerializeField, HideInInspector] int dataVersion;
    const string Key = "SoulSwapUses";

    public override string DefaultName => "Soul Swap";
    public override string DefaultDescription =>
        "If your HP% is lower than the enemy's, swap HP% with it (you get its HP%, it gets yours). " +
        $"Usable {(usesPerBattle == 1 ? "once" : $"{usesPerBattle} times")} per battle. Your Mana bar grows to {manaCost} at the start of the battle so you can pay for it.";

    void Reset() { manaCost = 200; dataVersion = 1; }

    protected override void OnValidate()
    {
        if (dataVersion < 1) { dataVersion = 1; if (manaCost < 200) manaCost = 200; } // the cost used to be fixed in code
        base.OnValidate();
    }

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.maxMana = Mathf.Max(self.maxMana, manaCost);
    }

    public override bool CanActivate(CardRuntime self, CardRuntime enemy) =>
        self.GetTalentState(Key, 0) < usesPerBattle && self.healthPercent < enemy.healthPercent;

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.SetTalentState(Key, self.GetTalentState(Key, 0) + 1);
        float mine = self.healthPercent, theirs = enemy.healthPercent;
        int myTarget = Mathf.Clamp(Mathf.RoundToInt(theirs * self.maxHealth), 1, self.maxHealth);
        int theirTarget = Mathf.Clamp(Mathf.RoundToInt(mine * enemy.maxHealth), 1, enemy.maxHealth);
        BattleLog.Info($"<color=#F06292>[Soul Swap] {self.DisplayName} and {enemy.DisplayName} trade souls! ({mine * 100:0}% <> {theirs * 100:0}%)</color>");
        if (enemy.health > theirTarget) enemy.TakeTrueDamage(enemy.health - theirTarget, DamageKind.True);
        if (myTarget > self.health) { int gain = myTarget - self.health; self.health = myTarget; BattleLog.Healed(self, gain); }
    }
}
