using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/RestrictedInstinct")]
public class RestrictedInstinct : ActiveTalent
{
    public int silenceDuration = 3;

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        StatusEffects.ApplySilence(enemy, silenceDuration);
        Debug.Log($"<color=purple>[RestrictedInstinct] Enemy is SILENCED for {silenceDuration} turns!</color>");
    }
}
