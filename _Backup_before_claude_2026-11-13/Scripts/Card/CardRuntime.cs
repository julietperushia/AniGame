using UnityEngine;
using System.Collections.Generic;

public class CardRuntime
{
    public Card baseCard;
    public Talent talent;

    public int maxHealth;
    public int health;

    public int baseAttack;
    public int baseDefense;
    public int baseSpeed;

    public int attack;
    public int defense;
    public int speed;

    public int mana;
    public int maxMana = 100;
    public int manaRegen;

    // Status Effects
    public int burnStacks;
    public int poisonStacks;
    public int poisonTurns; // Tracks how many turns poison has been active (for miss chance)
    public bool isStunned;
    public int silenceTurns;
    public int regenerationStacks;
    public bool hasEndurance;
    public int enduranceTurns;

    // Combat modifiers
    public int lifesteal;
    public int critRate;
    public int critDamage;
    public int evasion;

    public float healingBonus = 0f;
    public float healingReduction = 0f; // From Burn effect
    public int damageReduction = 0; 

    public int lastDamageDealt;
    public bool lastAttackWasCrit;

    // Buff system
    public int bonusAttack;
    public int bonusDefense;
    public int buffTurnsLeft;

    // Talent state storage (per-instance, not shared)
    private Dictionary<string, object> talentState = new Dictionary<string, object>();

    // Computed properties
    public float healthPercent => (float)health / maxHealth;
    public int TotalAttack => attack + bonusAttack;
    public int TotalDefense => defense + bonusDefense;
    public bool IsAlive => health > 0;

    public CardRuntime(Card card)
    {
        baseCard = card;
        talent = card.talent;

        maxHealth = card.health;
        health = card.health;

        baseAttack = card.attack;
        baseDefense = card.defense;
        baseSpeed = card.speed;

        attack = baseAttack;
        defense = baseDefense;
        speed = baseSpeed;

        mana = card.mana;
        maxMana = card.maxMana;
        critRate = card.critRate;
        critDamage = card.critDamage;
        evasion = card.evasion;
    }

    /// <summary>
    /// Heals the card by the specified amount, capped at max health
    /// </summary>
    public void Heal(int amount)
    {
        float multiplier = (1f + healingBonus) * (1f - healingReduction);
        int actualHeal = Mathf.RoundToInt(amount * multiplier);
        actualHeal = Mathf.Max(0, actualHeal); // Can't heal if reduction >= 100%
        health = Mathf.Min(maxHealth, health + actualHeal);
    }

    /// <summary>
    /// Deals damage to the card, applying damage reduction
    /// </summary>
    public void TakeDamage(int amount)
    {
        int actualDamage = Mathf.Max(1, amount - damageReduction);
        health = Mathf.Max(0, health - actualDamage);
    }

    /// <summary>
    /// Deals true damage that bypasses all defenses and damage reduction
    /// </summary>
    public void TakeTrueDamage(int amount)
    {
        health = Mathf.Max(0, health - amount);
    }

    /// <summary>
    /// Resets stats to base values and restores full health
    /// </summary>
    public void ResetToBaseStats()
    {
        attack = baseAttack;
        defense = baseDefense;
        speed = baseSpeed;
        health = maxHealth;
    }

    /// <summary>
    /// Adds a timed buff with percentage-based stat bonuses
    /// </summary>
    public void AddTimedBuff(int atkBonusPercent, int defBonusPercent, int turns)
    {
        bonusAttack += baseAttack * atkBonusPercent / 100;
        bonusDefense += baseDefense * defBonusPercent / 100;
        buffTurnsLeft = turns;
    }

    /// <summary>
    /// Decrements buff duration and removes buffs when expired
    /// </summary>
    public void TickBuffs()
    {
        if (buffTurnsLeft > 0)
        {
            buffTurnsLeft--;
            if (buffTurnsLeft == 0)
            {
                bonusAttack = 0;
                bonusDefense = 0;
            }
        }
    }

    /// <summary>
    /// Adds mana, capped at maxMana
    /// </summary>
    public void GainMana(int amount)
    {
        mana = Mathf.Min(mana + amount, maxMana);
    }

    // ========== Talent State Management ==========
    
    /// <summary>
    /// Gets talent-specific state data (per-instance, not shared across ScriptableObject)
    /// </summary>
    public T GetTalentState<T>(string key, T defaultValue = default)
    {
        if (talentState.TryGetValue(key, out object value))
        {
            return (T)value;
        }
        return defaultValue;
    }

    /// <summary>
    /// Sets talent-specific state data
    /// </summary>
    public void SetTalentState<T>(string key, T value)
    {
        talentState[key] = value;
    }

    /// <summary>
    /// Checks if talent state exists
    /// </summary>
    public bool HasTalentState(string key)
    {
        return talentState.ContainsKey(key);
    }
}
