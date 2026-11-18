using UnityEngine;
using System.Collections.Generic;

/// <summary>What kind of damage was dealt (drives popup colours and log text).</summary>
public enum DamageKind
{
    Normal,
    Critical,
    Skill,
    True,
    Burn,
    Poison,
    Reflect,
    Recoil
}

/// <summary>
/// In-battle state of one card. Created from a <see cref="Card"/> asset plus the owner's level/evolution.
/// All HP changes go through TakeDamage / TakeTrueDamage / Heal so the battle log (and the UI) see them.
/// </summary>
public class CardRuntime
{
    public Card baseCard;
    public Talent talent;

    public int level = 1;
    public int evolution;
    public bool isPlayer;

    public int baseMaxHealth;
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
    /// <summary>Mana regeneration in percent. 100 = normal gain, 140 = +40% (Celestial Influence).</summary>
    public int manaRegen = 100;

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
    public int lifesteal;   // percent of normal-attack damage healed back
    public int critRate;
    public int critDamage;
    public int evasion;

    public float healingBonus = 0f;
    public float healingReduction = 0f; // From Burn effect
    public int damageReduction = 0;          // flat damage removed from every non-true hit
    public float damageTakenReduction = 0f;  // percent (0.05 = 5%) removed from every non-true hit

    public int lastDamageDealt;
    public bool lastAttackWasCrit;

    // Buff system
    public int bonusAttack;
    public int bonusDefense;
    public int buffTurnsLeft;

    // Talent state storage (per-instance, not shared)
    private readonly Dictionary<string, object> talentState = new Dictionary<string, object>();

    // Computed properties
    public float healthPercent => maxHealth > 0 ? (float)health / maxHealth : 0f;
    public int TotalAttack => Mathf.Max(1, attack + bonusAttack);
    public int TotalDefense => Mathf.Max(1, defense + bonusDefense);
    public int TotalSpeed => Mathf.Max(1, speed);
    public bool IsAlive => health > 0;
    public string DisplayName => baseCard != null ? baseCard.DisplayName : "???";
    public CardElement Element => baseCard != null ? baseCard.element : CardElement.Fire;

    public CardRuntime(Card card) : this(card, 1, 0) { }

    public CardRuntime(Card card, int level, int evolution)
    {
        baseCard = card;
        talent = card.talent;
        this.level = Mathf.Clamp(level, 1, CardStats.MaxLevel);
        this.evolution = Mathf.Clamp(evolution, 0, CardStats.MaxEvolution);

        baseMaxHealth = CardStats.Scale(card.health, this.level, this.evolution);
        maxHealth = baseMaxHealth;
        health = maxHealth;

        baseAttack = CardStats.Scale(card.attack, this.level, this.evolution);
        baseDefense = CardStats.Scale(card.defense, this.level, this.evolution);
        baseSpeed = CardStats.Scale(card.speed, this.level, this.evolution);

        attack = baseAttack;
        defense = baseDefense;
        speed = baseSpeed;

        mana = Mathf.Max(0, card.mana);
        maxMana = card.maxMana > 0 ? card.maxMana : 100;
        manaRegen = 100;
        critRate = card.critRate;
        critDamage = card.critDamage > 0 ? card.critDamage : 135;
        evasion = card.evasion;
    }

    /// <summary>
    /// Heals the card by the specified amount (affected by healing bonus / Burn reduction), capped at max health.
    /// </summary>
    public void Heal(int amount)
    {
        if (!IsAlive || amount <= 0) return;
        float multiplier = (1f + healingBonus) * (1f - healingReduction);
        int actualHeal = Mathf.Max(0, Mathf.RoundToInt(amount * multiplier)); // Can't heal if reduction >= 100%
        int before = health;
        health = Mathf.Min(maxHealth, health + actualHeal);
        BattleLog.Healed(this, health - before);
    }

    /// <summary>Deals damage to the card, applying flat damage reduction (min 1).</summary>
    public void TakeDamage(int amount)
    {
        TakeDamage(amount, DamageKind.Skill);
    }

    /// <summary>Deals damage of a given kind. Returns the damage actually taken.</summary>
    public int TakeDamage(int amount, DamageKind kind)
    {
        if (!IsAlive) return 0;
        float scaled = amount * (1f - Mathf.Clamp01(damageTakenReduction));
        int actualDamage = Mathf.Max(1, Mathf.RoundToInt(scaled) - damageReduction);
        actualDamage = Mathf.Min(actualDamage, health);
        health -= actualDamage;
        BattleLog.Damaged(this, actualDamage, kind);
        return actualDamage;
    }

    /// <summary>Deals true damage that bypasses all defenses and damage reduction.</summary>
    public void TakeTrueDamage(int amount)
    {
        TakeTrueDamage(amount, DamageKind.True);
    }

    public int TakeTrueDamage(int amount, DamageKind kind)
    {
        if (!IsAlive || amount <= 0) return 0;
        int actualDamage = Mathf.Min(amount, health);
        health -= actualDamage;
        BattleLog.Damaged(this, actualDamage, kind);
        return actualDamage;
    }

    /// <summary>Resets stats (and max HP) to base values, clears timed buffs and restores full health.</summary>
    public void ResetToBaseStats()
    {
        attack = baseAttack;
        defense = baseDefense;
        speed = baseSpeed;
        bonusAttack = 0;
        bonusDefense = 0;
        buffTurnsLeft = 0;
        maxHealth = baseMaxHealth;
        int before = health;
        health = maxHealth;
        if (health > before) BattleLog.Healed(this, health - before);
    }

    /// <summary>Adds a timed buff with percentage-based stat bonuses.</summary>
    public void AddTimedBuff(int atkBonusPercent, int defBonusPercent, int turns)
    {
        bonusAttack += baseAttack * atkBonusPercent / 100;
        bonusDefense += baseDefense * defBonusPercent / 100;
        buffTurnsLeft = Mathf.Max(buffTurnsLeft, turns);
    }

    /// <summary>Decrements buff duration and removes buffs when expired (called at the end of this card's turn).</summary>
    public void TickBuffs()
    {
        if (buffTurnsLeft > 0)
        {
            buffTurnsLeft--;
            if (buffTurnsLeft == 0)
            {
                bonusAttack = 0;
                bonusDefense = 0;
                BattleLog.Status(this, $"{DisplayName}'s timed buff expired");
            }
        }
    }

    /// <summary>Adds raw mana, capped at maxMana (no regen modifier).</summary>
    public void GainMana(int amount)
    {
        mana = Mathf.Clamp(mana + amount, 0, maxMana);
    }

    /// <summary>Adds mana scaled by <see cref="manaRegen"/>.</summary>
    public void GainManaWithRegen(int amount)
    {
        GainMana(Mathf.RoundToInt(amount * Mathf.Max(0, manaRegen) / 100f));
    }

    // ========== Talent State Management ==========

    /// <summary>Gets talent-specific state data (per-instance, not shared across the ScriptableObject).</summary>
    public T GetTalentState<T>(string key, T defaultValue = default)
    {
        if (talentState.TryGetValue(key, out object value) && value is T typed)
        {
            return typed;
        }
        return defaultValue;
    }

    /// <summary>Sets talent-specific state data.</summary>
    public void SetTalentState<T>(string key, T value)
    {
        talentState[key] = value;
    }

    /// <summary>Checks if talent state exists.</summary>
    public bool HasTalentState(string key)
    {
        return talentState.ContainsKey(key);
    }

    /// <summary>One-line rich-text summary of active status effects (for the UI).</summary>
    public string StatusSummary()
    {
        var parts = new List<string>();
        if (burnStacks > 0) parts.Add($"<color=#FF8A3D>BURN x{burnStacks}</color>");
        if (poisonStacks > 0) parts.Add($"<color=#7CFC6A>POISON x{poisonStacks}</color>");
        if (isStunned) parts.Add("<color=#FFE14D>STUN</color>");
        if (silenceTurns > 0) parts.Add($"<color=#C78BFF>SILENCE {silenceTurns}</color>");
        if (regenerationStacks > 0) parts.Add($"<color=#5DFFB0>REGEN x{regenerationStacks}</color>");
        if (hasEndurance) parts.Add($"<color=#6FD3FF>ENDURE {enduranceTurns}</color>");
        if (buffTurnsLeft > 0) parts.Add($"<color=#FFD54F>BLESS {buffTurnsLeft}</color>");
        if (lifesteal > 0) parts.Add($"<color=#FF6B8B>LIFESTEAL {lifesteal}%</color>");
        return string.Join("  ", parts);
    }
}
