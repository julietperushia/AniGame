using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/CelestialBlessing")]
public class CelestialBlessing : ActiveTalent
{
    public int maxManaLimit = 600;
    public int manaRegenBonus = 18;

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.maxMana = Mathf.Min(self.maxMana + 50, maxManaLimit);
        int currentMaxMana = self.maxMana;

        self.manaRegen = Mathf.RoundToInt(self.manaRegen * (1f + manaRegenBonus / 100f));

        int healAmount = currentMaxMana * 2;
        self.Heal(healAmount);

        Debug.Log($"<color=gold>[CelestialBlessing] Max Mana: {currentMaxMana}, +{manaRegenBonus}% regen, +{healAmount} HP!</color>");
    }
}
