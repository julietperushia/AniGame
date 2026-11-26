using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Grievous Limiter")]
public class GrievousLimiter : PassiveTalent
{
    [Tooltip("Enemy ATK lost when the battle starts, in % of its base ATK.")]
    public int startReduction = 40;
    [Tooltip("ATK the enemy gets back at the start of each of your turns, in % of its base ATK.")]
    public int recoveryPerTurn = 5;

    const string Key = "GrievousRemaining";

    public override string DefaultName => "Grievous Limiter";
    public override string DefaultDescription =>
        $"When the battle starts, lower the enemy's ATK by {Pct(startReduction)} of its base ATK. It gets back {recoveryPerTurn}% of its base ATK at the start of each of your turns " +
        "until the whole debuff is gone.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        int reduction = Mathf.Max(1, enemy.baseAttack * Scaled(startReduction, self) / 100);
        enemy.attack -= reduction;
        self.SetTalentState(Key, reduction);
        BattleLog.Passive(self, $"<color=#FF6B6B>[Grievous Limiter] {enemy.DisplayName} ATK -{reduction}</color>");
    }

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        int remaining = self.GetTalentState(Key, 0);
        if (remaining <= 0) return;
        int recover = Mathf.Min(remaining, Mathf.Max(1, enemy.baseAttack * recoveryPerTurn / 100));
        enemy.attack += recover;
        self.SetTalentState(Key, remaining - recover);
    }
}
