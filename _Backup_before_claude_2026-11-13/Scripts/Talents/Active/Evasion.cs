using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Evasion")]
public class Evasion : ActiveTalent
{
    public float evasionBonus = 1.17f;

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        self.evasion = Mathf.FloorToInt(self.evasion * evasionBonus);
        Debug.Log($"<color=cyan>[Evasion] +{Mathf.RoundToInt((evasionBonus - 1f) * 100f)}% evasion (Total: {self.evasion}%)</color>");
    }
}
