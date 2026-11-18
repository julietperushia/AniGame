using UnityEngine;

/// <summary>
/// Asset text: "Increase your max Mana by 50 (up to 600 total) and Mana Regeneration by 18%,
/// and heal allied familiars equal to 2x your max Mana."
/// Talents fire when the mana bar is full, so a bigger bar means slower but bigger heals.
/// </summary>
[CreateAssetMenu(menuName = "Talents/Active/CelestialBlessing")]
public class CelestialBlessing : ActiveTalent
{
    public int maxManaLimit = 600;
    public int maxManaIncrease = 50;
    public int manaRegenBonus = 18;
    public float healPerMaxMana = 2f;

    public override string DefaultName => "Celestial Blessing";
    public override string DefaultDescription => $"Increase your max Mana by {maxManaIncrease} (up to {maxManaLimit} total) and Mana Regeneration by {manaRegenBonus}%, and heal equal to {healPerMaxMana:0.#}x your max Mana.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        if (self.silenceTurns == 0)
            self.maxMana = Mathf.Min(maxManaLimit, self.maxMana + maxManaIncrease);
        self.manaRegen += Scaled(manaRegenBonus, self);
        int healAmount = Mathf.RoundToInt(self.maxMana * Scaled(healPerMaxMana, self));
        BattleLog.Info($"<color=#FFD54F>[Celestial Blessing] Max Mana {self.maxMana}, Regen {self.manaRegen}%</color>");
        self.Heal(healAmount);
    }
}
