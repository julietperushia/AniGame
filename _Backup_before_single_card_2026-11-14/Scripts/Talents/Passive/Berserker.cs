using UnityEngine;

/// <summary>Asset text: "While your health is lower than 30%, increase the ATK of all allied familiars by 30%".
/// Fixed: the bonus is now removed again if you are healed back above the threshold.</summary>
[CreateAssetMenu(menuName = "Talents/Passive/Berserker")]
public class Berserker : PassiveTalent
{
    public float triggerHp = 0.3f;
    public int atkBonus = 30;

    const string Key = "BerserkerBonus";

    public override string DefaultName => "Berserker";
    public override string DefaultDescription => $"While your health is lower than {triggerHp * 100:0}%, increase your ATK by {atkBonus}%.";

    public override void OnTurnStart(CardRuntime self, CardRuntime enemy)
    {
        int applied = self.GetTalentState(Key, 0);
        bool shouldApply = self.healthPercent < triggerHp;
        if (shouldApply && applied == 0)
        {
            int bonus = Mathf.Max(1, self.baseAttack * atkBonus / 100);
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
