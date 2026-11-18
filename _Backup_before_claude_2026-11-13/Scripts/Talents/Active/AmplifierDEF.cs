using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/AmplifierDEF")]
public class AmplifierDEF : ActiveTalent
{
    public int defBonus = 14;

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        int bonusDef = self.baseDefense * defBonus / 100;
        self.defense += bonusDef;
        Debug.Log($"<color=cyan>[Amplifier DEF] +{bonusDef} DEF (Total: {self.defense})</color>");
    }
}
