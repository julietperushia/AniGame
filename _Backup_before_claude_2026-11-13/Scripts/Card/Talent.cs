using UnityEngine;

public abstract class Talent : ScriptableObject
{
    public string talentName;
    [TextArea]
    public string description;
    public virtual bool IsActive => false;

    // Called when battle starts
    public virtual void OnBattleStart(CardRuntime self, CardRuntime enemy) { }

    // Called before attack
    public virtual void OnBeforeAttack(CardRuntime self, CardRuntime enemy) { }

    // Called after attack
    public virtual void OnAfterAttack(CardRuntime self, CardRuntime enemy) { }

    // Called when taking damage
    public virtual void OnTakeDamage(CardRuntime self, ref int damage) { }

    // Called every turn
    public virtual void OnTurnStart(CardRuntime self, CardRuntime enemy) { }
}
public abstract class PassiveTalent : Talent
{
    public override bool IsActive => false;
}
public abstract class ActiveTalent : Talent
{
    public override bool IsActive => true;
    public int manaCost = 100;

    public abstract void Activate(CardRuntime self, CardRuntime enemy);
}