using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Berserker")]
public class Berserker : PassiveTalent
{
    [Tooltip("Active while your HP is below this (0.3 = 30%).")]
    public float triggerHp = 0.3f;
    [Tooltip("ATK bonus while active, in % of base ATK.")]
    public int atkBonus = 30;

    const string Key = "BerserkerBonus";

    public override string DefaultName => "Berserker";
    public override string DefaultDescription =>
        $"While your HP is below {P(triggerHp)}, your ATK is raised by {Pct(atkBonus)} of your base ATK. The bonus goes away if you heal back above {P(triggerHp)} " +
        "(checked at the start of each of your turns).";

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        int applied = self.GetTalentState(Key, 0);
        bool shouldApply = self.healthPercent < triggerHp;
        if (shouldApply && applied == 0)
        {
            int bonus = Mathf.Max(1, self.baseAttack * Scaled(atkBonus, self) / 100);
            self.attack += bonus;
            self.SetTalentState(Key, bonus);
            BattleLog.Passive(self, $"<color=#FF6B6B>[Berserker] {self.DisplayName} goes berserk! +{bonus} ATK</color>");
        }
        else if (!shouldApply && applied > 0)
        {
            self.attack -= applied;
            self.SetTalentState(Key, 0);
            BattleLog.Passive(self, $"[Berserker] {self.DisplayName} calms down (-{applied} ATK)");
        }
    }
}
