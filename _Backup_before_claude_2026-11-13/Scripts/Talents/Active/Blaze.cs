using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Blaze")]
public class Blaze : ActiveTalent
{
    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        StatusEffects.ApplyBurn(enemy, 1);
        Debug.Log($"<color=orange>[Blaze] Enemy is BURNING! (10% true damage/turn, -75% healing)</color>");
    }
}
