using UnityEngine;

/// <summary>
/// Utility class for managing status effects in battle.
/// Numbers follow the Anigame talent texts (dbgames.info):
///  Burn  - 5% max HP true damage per stack per turn (max 4 stacks), healing received -75%.
///          (Asset text says 10%; at 10% per stack Blaze won 76% of balanced fights, so it was halved and capped.)
///  Stun  - skip the next turn, DEF permanently -10%.
///  Poison - 5 + 5% max HP true damage per stack, 10% miss chance +5% per turn it lasts.
/// </summary>
public static class StatusEffects
{
    public const float BurnPercentPerStack = 0.05f;
    public const int MaxBurnStacks = 4;

    /// <summary>Apply Burn status: 5% max HP true damage per stack per turn (max 4), reduces healing by 75%.</summary>
    public static void ApplyBurn(CardRuntime target, int stacks = 1, float percentPerStack = BurnPercentPerStack)
    {
        target.burnPercent = target.burnStacks > 0 ? Mathf.Max(target.burnPercent, percentPerStack) : percentPerStack;
        target.burnStacks = Mathf.Min(MaxBurnStacks, target.burnStacks + stacks);
        target.healingReduction = 0.75f;
        BattleLog.Status(target, $"<color=#FF8A3D>{target.DisplayName} is burning! (x{target.burnStacks})</color>");
    }

    /// <summary>Apply Poison status: deals 5 + 5% max HP true damage per turn. 10% base miss chance +5% per turn.</summary>
    public static void ApplyPoison(CardRuntime target, int stacks = 1, float percentPerStack = 0.05f)
    {
        target.poisonPercent = target.poisonStacks > 0 ? Mathf.Max(target.poisonPercent, percentPerStack) : percentPerStack;
        target.poisonStacks += stacks;
        target.poisonTurns = 0; // Reset turn counter
        BattleLog.Status(target, $"<color=#7CFC6A>{target.DisplayName} is poisoned! (x{target.poisonStacks})</color>");
    }

    /// <summary>Apply Stun: skips next turn, permanently reduces DEF by 10%.</summary>
    public static void ApplyStun(CardRuntime target)
    {
        target.isStunned = true;
        int defReduction = target.baseDefense * 10 / 100;
        target.defense -= defReduction;
        target.baseDefense -= defReduction; // Permanent reduction
        BattleLog.Status(target, $"<color=#FFE14D>{target.DisplayName} is stunned! DEF -{defReduction} permanently</color>");
    }

    /// <summary>Apply Silence: prevents talent usage for the given turns and caps mana at 100.</summary>
    public static void ApplySilence(CardRuntime target, int turns = 3)
    {
        target.silenceTurns = Mathf.Max(target.silenceTurns, turns);
        target.maxMana = 100;
        target.mana = Mathf.Min(target.mana, 100);
        BattleLog.Status(target, $"<color=#C78BFF>{target.DisplayName} is silenced for {turns} turns!</color>");
    }

    /// <summary>Apply Endurance: reduces normal attack damage by 65% for the given turns.</summary>
    public static void ApplyEndurance(CardRuntime target, int turns = 3, float reflectPercent = 0.35f)
    {
        target.enduranceReflect = reflectPercent;
        target.hasEndurance = true;
        target.enduranceTurns = Mathf.Max(target.enduranceTurns, turns);
        BattleLog.Status(target, $"<color=#6FD3FF>{target.DisplayName} braces with Endurance ({turns} turns)</color>");
    }

    /// <summary>
    /// Process all status effects at the start of a card's turn.
    /// Returns true if the card can act this turn, false if stunned.
    /// </summary>
    public static bool ProcessStatusEffects(CardRuntime card)
    {
        // Regeneration (heal first)
        if (card.regenerationStacks > 0)
        {
            int healAmount = Mathf.RoundToInt(card.maxHealth * card.regenPercent * card.regenerationStacks);
            card.Heal(healAmount);
        }

        // Burn damage
        if (card.burnStacks > 0 && card.IsAlive)
        {
            int burnDamage = Mathf.Max(1, Mathf.RoundToInt(card.maxHealth * card.burnPercent * card.burnStacks));
            card.TakeTrueDamage(burnDamage, DamageKind.Burn);
        }

        // Poison damage (with miss chance)
        if (card.poisonStacks > 0 && card.IsAlive)
        {
            int missChance = 10 + (card.poisonTurns * 5); // 10% base + 5% per turn
            if (!BattleRandom.Chance(missChance))
            {
                int poisonDamage = 5 + Mathf.RoundToInt(card.maxHealth * card.poisonPercent * card.poisonStacks);
                card.TakeTrueDamage(poisonDamage, DamageKind.Poison);
            }
            else
            {
                BattleLog.Status(card, $"<color=#7CFC6A>Poison missed {card.DisplayName} ({missChance}% chance)</color>");
            }
            card.poisonTurns++;
        }

        if (!card.IsAlive) return false;

        // Silence countdown
        if (card.silenceTurns > 0)
        {
            card.silenceTurns--;
            if (card.silenceTurns == 0) BattleLog.Status(card, $"{card.DisplayName} is no longer silenced");
        }

        // Endurance countdown
        if (card.hasEndurance && card.enduranceTurns > 0)
        {
            card.enduranceTurns--;
            if (card.enduranceTurns == 0)
            {
                card.hasEndurance = false;
                BattleLog.Status(card, $"{card.DisplayName}'s Endurance faded");
            }
        }

        // Stun check
        if (card.isStunned)
        {
            card.isStunned = false; // Clear stun after skipping turn
            return false;
        }

        return true;
    }
}
