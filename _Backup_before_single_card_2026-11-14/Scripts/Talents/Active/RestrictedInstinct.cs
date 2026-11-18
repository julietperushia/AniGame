using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/RestrictedInstinct")]
public class RestrictedInstinct : ActiveTalent
{
    public int silenceDuration = 3;

    public override string DefaultName => "Restricted Instinct";
    public override string DefaultDescription => $"Silence the enemy for {silenceDuration} turns: it cannot use its talent and its mana is capped at 100.";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        StatusEffects.ApplySilence(enemy, silenceDuration);
    }
}
