using UnityEngine;

/// <summary>Asset text: "When the battle starts, decrease the ATK of enemy familiars by 50%. Their ATK increases by 6% every turn after that."
/// Fixed: per-battle state moved out of the shared ScriptableObject, and the recovery stops once the debuff is gone.</summary>
[CreateAssetMenu(menuName = "Talents/Passive/Grievous Limiter")]
public class GrievousLimiter : PassiveTalent
{
    public int startReduction = 40;
    public int recoveryPerTurn = 5;

    const string Key = "GrievousRemaining";

    public override string DefaultName => "Grievous Limiter";
    public override string DefaultDescription => $"When the battle starts, decrease the enemy's ATK by {startReduction}%. It recovers {recoveryPerTurn}% every turn after that.";

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
