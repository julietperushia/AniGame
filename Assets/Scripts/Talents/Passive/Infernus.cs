using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Infernus")]
public class Infernus : PassiveTalent
{
    [Tooltip("Part of your normal attack damage dealt as true damage (0.15 = 15%).")]
    public float truePart = 0.15f;
    [Tooltip("The true part can't go above this, even with rarity bonus (0.9 = 90%).")]
    public float maxTruePart = 0.9f;
    [Tooltip("You heal this part of the true damage (0.5 = 50%).")]
    public float heal = 0.5f;

    public override string DefaultName => "Infernus";
    public override string DefaultDescription =>
        $"{PctF(truePart, maxTruePart)} of your normal attack damage is dealt as true damage (it ignores DEF and damage reduction), and you heal {P(heal)} of that true damage.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.infernusPercent = Mathf.Min(maxTruePart, Scaled(truePart, self));
        self.infernusHeal = heal;
    }
}
