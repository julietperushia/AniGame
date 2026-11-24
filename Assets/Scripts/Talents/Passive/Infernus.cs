using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/Infernus")]
public class Infernus : PassiveTalent
{
    public float truePart = 0.15f;
    public float heal = 0.5f;

    public override string DefaultName => "Infernus";
    public override string DefaultDescription => $"{truePart * 100:0}% of your attack damage is dealt as true damage (it ignores DEF and reductions), and you heal for {heal * 100:0}% of that true damage.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy)
    {
        self.infernusPercent = Mathf.Min(0.9f, Scaled(truePart, self));
        self.infernusHeal = heal;
    }
}
