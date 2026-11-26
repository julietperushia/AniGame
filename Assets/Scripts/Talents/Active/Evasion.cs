using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Evasion")]
public class Evasion : ActiveTalent
{
    [Tooltip("Evasion rating gained per cast (no limit; the real chance has diminishing returns).")]
    public int evasionBonus = 17;

    public override string DefaultName => "Evasion";
    public override string DefaultDescription =>
        $"Gain +{Num(evasionBonus)} evasion rating every cast (stacks, no limit). The real chance to evade a normal attack has diminishing returns and never passes {GameRules.EvasionCap:0}% " +
        $"(rating 25 = {GameRules.EvadeChance(25):0.#}%, 50 = {GameRules.EvadeChance(50):0.#}%, 100 = {GameRules.EvadeChance(100):0.#}%).";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.evasion += Scaled(evasionBonus, self);
        BattleLog.Info($"<color=#6FD3FF>[Evasion] {self.DisplayName} evasion rating {GameRules.EvasionLabel(self.evasion)}</color>");
    }
}
