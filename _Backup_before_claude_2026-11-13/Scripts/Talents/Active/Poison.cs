using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/Poison")]
public class Poison : ActiveTalent
{
    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        StatusEffects.ApplyPoison(enemy, 1);
        Debug.Log($"<color=green>[Poison] Enemy is POISONED! (5 + 5% HP true damage/turn)</color>");
    }
}
