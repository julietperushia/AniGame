using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Hex")]
public class HexTalent : ActiveTalent
{
    [Tooltip("Turns the enemy's ATK and DEF stay swapped.")]
    public int turns = 2;
    [Tooltip("After the swap you permanently gain this % of the gap between the enemy's ATK and DEF " +
             "(as ATK if its ATK is higher, as DEF if its DEF is higher). 100 = the whole gap.")]
    public int gapGainPercent = 100;

    public override string DefaultName => "Hex";
    public override string DefaultDescription =>
        $"Swap the enemy's ATK and DEF for {N(turns, "turn")}. They swap back when the Hex ends. " +
        (gapGainPercent > 0 ? $"Then, if its ATK is higher than its DEF, you permanently gain ATK equal to {(gapGainPercent == 100 ? "the difference" : $"{gapGainPercent}% of the difference")}; " +
                              "if its DEF is higher, you gain DEF instead." : "");

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        StatusEffects.ApplyHex(enemy, turns);
        if (enemy.hexTurns <= 0 || gapGainPercent <= 0) return; // resisted: no swap, no gain
        int gap = enemy.TotalAttack - enemy.TotalDefense;       // after the swap
        int gain = Mathf.RoundToInt(Mathf.Abs(gap) * gapGainPercent / 100f);
        if (gain <= 0) return;
        if (gap > 0) self.attack += gain; else self.defense += gain;
        BattleLog.Info($"<color=#CE93D8>[Hex] {self.DisplayName} takes the gap: +{gain} {(gap > 0 ? "ATK" : "DEF")}</color>");
    }
}
