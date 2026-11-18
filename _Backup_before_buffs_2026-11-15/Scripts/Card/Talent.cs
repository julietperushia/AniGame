using UnityEngine;

/// <summary>
/// Base class for every talent. Talents are ScriptableObjects shared by many cards,
/// so NEVER keep per-battle state in fields: use CardRuntime.Get/SetTalentState instead.
/// </summary>
public abstract class Talent : ScriptableObject
{
    public string talentName;
    [TextArea]
    public string description;
    public virtual bool IsActive => false;

    /// <summary>Name used when the asset's talentName is empty (and by the setup tool when creating assets).</summary>
    public virtual string DefaultName => GetType().Name;

    /// <summary>Description used when the asset's description is empty.</summary>
    public virtual string DefaultDescription => "";

    public string DisplayName => string.IsNullOrEmpty(talentName) ? DefaultName : talentName;

    /// <summary>Rarity scaling of this talent for its owner: 1.0 (Common) to 1.2 (God). Every number a talent uses goes through it.</summary>
    protected static float Power(CardRuntime self) => self != null ? Mathf.Max(0.1f, self.talentPower) : 1f;
    protected static int Scaled(int value, CardRuntime self) => Mathf.RoundToInt(value * Power(self));
    protected static float Scaled(float value, CardRuntime self) => value * Power(self);
    public string Description => string.IsNullOrEmpty(description) ? DefaultDescription : description;

    // Called when battle starts
    public virtual void OnBattleStart(CardRuntime self, CardRuntime enemy) { }

    // Called before attack
    public virtual void OnBeforeAttack(CardRuntime self, CardRuntime enemy) { }

    // Called after attack
    public virtual void OnAfterAttack(CardRuntime self, CardRuntime enemy) { }

    // Called when taking damage from a normal attack
    public virtual void OnTakeDamage(CardRuntime self, ref int damage) { }

    // Called every turn (after status effects, before acting)
    public virtual void OnTurnStart(CardRuntime self, CardRuntime enemy) { }
}

public abstract class PassiveTalent : Talent
{
    public override bool IsActive => false;
}

public abstract class ActiveTalent : Talent
{
    public override bool IsActive => true;

    [Tooltip("Minimum mana needed. The talent fires when the mana bar is full, so the real cost is max(manaCost, maxMana).")]
    public int manaCost = 100;

    /// <summary>Anigame rule: an active talent fires when the card's mana bar is full.</summary>
    public virtual int GetManaCost(CardRuntime self) => Mathf.Max(manaCost, self.maxMana);

    public abstract void Activate(CardRuntime self, CardRuntime enemy);
}
