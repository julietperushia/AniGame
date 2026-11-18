using UnityEngine;

/// <summary>Anigame: "Absorbs up to 20% of enemy familiars' Mana and deals True damage equal to the absorbed Mana x10."</summary>
[CreateAssetMenu(menuName = "Talents/Active/ManaReaver")]
public class ManaReaver : ActiveTalent
{
    public int manaStealPercent = 20;
    public int damagePerMana = 10;

    public override string DefaultName => "Mana Reaver";
    public override string DefaultDescription => $"Absorb {manaStealPercent}% of the enemy's Mana and deal true damage equal to the absorbed Mana x{damagePerMana}.";

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
