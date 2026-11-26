using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/ManaReaver")]
public class ManaReaver : ActiveTalent
{
    [Tooltip("Part of the enemy's current Mana absorbed (%).")]
    public int manaStealPercent = 20;
    [Tooltip("True damage per point of Mana absorbed.")]
    public int damagePerMana = 10;

    public override string DefaultName => "Mana Reaver";
    public override string DefaultDescription =>
        $"Absorb {Pct(manaStealPercent)} of the enemy's current Mana (you gain it) and deal true damage equal to the absorbed Mana x {damagePerMana}.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int manaToSteal = Mathf.RoundToInt(enemy.mana * Scaled(manaStealPercent, self) / 100f);
        enemy.mana = Mathf.Max(0, enemy.mana - manaToSteal);
        self.GainMana(manaToSteal);
        BattleLog.Info($"<color=#C78BFF>[Mana Reaver] Absorbed {manaToSteal} mana</color>");
        if (manaToSteal > 0)
            self.lastDamageDealt = enemy.TakeTrueDamage(manaToSteal * damagePerMana, DamageKind.True);
    }
}
