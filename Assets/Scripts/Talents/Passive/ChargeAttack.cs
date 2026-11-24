using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Passive/ChargeAttack")]
public class ChargeAttack : PassiveTalent
{
    public override string DefaultName => "Charge Attack";
    public override string DefaultDescription => "While above 30% HP you don't attack: each attack is stored instead. Once your HP drops to 30% or lower, your next attack releases everything and deals 130% of the stored damage on top.";

    public override void OnBattleStart(CardRuntime self, CardRuntime enemy) => self.chargeAttackPassive = true;
}
