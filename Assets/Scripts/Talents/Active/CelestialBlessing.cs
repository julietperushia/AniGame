using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/CelestialBlessing")]
public class CelestialBlessing : ActiveTalent
{
    [Tooltip("Max Mana can't grow past this.")]
    public int maxManaLimit = 600;
    [Tooltip("Max Mana gained per cast.")]
    public int maxManaIncrease = 50;
    [Tooltip("Mana Regeneration gained per cast, in % points.")]
    public int manaRegenBonus = 18;
    [Tooltip("HP healed per point of max Mana (2 = heal 2x your max Mana).")]
    public float healPerMaxMana = 2f;
    [Tooltip("Off: it fires again as soon as you have its Mana cost, even after your max Mana has grown (the bigger bar only stores extra Mana). " +
             "On: it needs the whole, bigger bar, so every cast takes longer (old behaviour: in short battles it often fired only once).")]
    public bool needsFullBar = false;

    public override string DefaultName => "Celestial Blessing";
    public override string DefaultDescription =>
        $"Raise your max Mana by {maxManaIncrease} (up to {maxManaLimit}; not while silenced) and your Mana Regeneration by {Pct(manaRegenBonus)}, " +
        $"then heal HP equal to {Mul(healPerMaxMana)} your max Mana. " +
        (needsFullBar ? "In 1v1 a bigger Mana bar means each cast takes longer but heals more."
                      : $"It fires again every time you have {manaCost} Mana: the bigger bar stores extra Mana and makes each heal bigger.");

    /// <summary>1v1: the cost stays at the talent's own Mana cost instead of growing with the bar it enlarges.</summary>
    public override int GetManaCost(CardRuntime self) => needsFullBar ? base.GetManaCost(self) : Mathf.Max(1, manaCost);

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
