using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/CounterStance")]
public class CounterStance : PassiveTalent
{
    [Tooltip("Evasion rating gained.")]
    public int evasionBonus = 5;
    [Tooltip("Damage of the counter, as a part of a normal attack (0.4 = 40%).")]
    public float counterDamage = 0.4f;

    public override string DefaultName => "Counter Stance";
    public override string DefaultDescription =>
        $"+{Num(evasionBonus)} evasion rating. Every time you evade a normal attack, you strike back at once for {PctF(counterDamage)} of a normal attack. " +
        "The counter can't be countered and gives no Mana.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.evasion += Scaled(evasionBonus, self);
        self.counterStance = true;
        self.counterDamage = Scaled(counterDamage, self);
    }
}
