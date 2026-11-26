using UnityEngine;

[CreateAssetMenu(menuName = "Talents/Active/RestrictedInstinct")]
public class RestrictedInstinct : ActiveTalent
{
    [Tooltip("Turns of Silence.")]
    public int silenceDuration = 3;
    [Tooltip("The silenced enemy's max Mana is cut to this (Mana above it is lost). 3v3: per card.")]
    public int manaCap = 100;

    public override string DefaultName => "Restricted Instinct";
    public override string DefaultDescription =>
        $"Silence the enemy for {TurnsS(silenceDuration)}: it can't use its active talent, and its max Mana is cut to {manaCap} (any Mana above that is lost).";

    public override void Activate(CardRuntime self, CardRuntime enemy)
    {
        StatusEffects.ApplySilence(enemy, Scaled(silenceDuration, self), true, manaCap);
    }
}
