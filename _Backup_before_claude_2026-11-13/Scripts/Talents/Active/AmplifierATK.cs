using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/AmplifierATK")]
public class AmplifierATK : ActiveTalent
{
    public int atkBonus = 14;

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int bonusAtk = self.baseAttack * atkBonus / 100;
        self.attack += bonusAtk;
        Debug.Log($"<color=cyan>[Amplifier ATK] +{bonusAtk} ATK (Total: {self.attack})</color>");
    }
}
