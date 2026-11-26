using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Guts")]
public class Guts : PassiveTalent
{
    [Tooltip("HP left after surviving the knockout hit.")]
    public int surviveHp = 1;
    [Tooltip("ATK bonus after surviving (%).")]
    public int atkBonus = 30;
    [Tooltip("Turns the ATK bonus lasts.")]
    public int turns = 3;

    public override string DefaultName => "Guts";
    public override string DefaultDescription =>
        $"Once per battle, survive a hit that would knock you out with {surviveHp} HP left, then gain +{atkBonus}% ATK for {N(turns, "turn")}.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.gutsReady = true;
        self.gutsHp = Mathf.Max(1, surviveHp);
        self.gutsAtkBonus = atkBonus;
        self.gutsTurns = turns;
    }
}
