using UnityEngine;

/// <summary>
/// Utility class for managing status effects in battle
/// </summary>
public static class StatusEffects
{
    /// <summary>
    /// Apply Burn status: deals 10% true damage per turn, reduces healing by 75%
    /// </summary>
    public static void ApplyBurn(CardRuntime target, int stacks = 1)
    {
        target.burnStacks += stacks;
        target.healingReduction = 0.75f; // 75% healing reduction
    }

    /// <summary>
    /// Apply Poison status: deals 5 + 5% HP true damage per turn
    /// Has 10% base miss chance + 5% per turn it persists
    /// </summary>
    public static void ApplyPoison(CardRuntime target, int stacks = 1)
    {
        target.poisonStacks += stacks;
        target.poisonTurns = 0; // Reset turn counter
    }

    /// <summary>
    /// Apply Stun: skips next turn, permanently reduces DEF by 10%
    /// </summary>
    public static void ApplyStun(CardRuntime target)
    {
        target.isStunned = true;
        // Permanently reduce defense by 10%
        int defReduction = target.baseDefense * 10 / 100;
        target.defense -= defReduction;
        target.baseDefense -= defReduction; // Permanent reduction
    }

    /// <summary>
    /// Apply Silence: prevents ability usage for specified turns
    /// </summary>
    public static void ApplySilence(CardRuntime target, int turns = 3)
    {
        target.silenceTurns = Mathf.Max(target.silenceTurns, turns);
        
        // Capping mana during silence as per requirements
        target.maxMana = 100;
        target.mana = Mathf.Min(target.mana, 100);
    }

    /// <summary>
    /// Apply Endurance: reduces normal attack damage by 65% for specified turns
    /// </summary>
    public static void ApplyEndurance(CardRuntime target, int turns = 3)
    {
        target.hasEndurance = true;
        target.enduranceTurns = turns;
    }

    /// <summary>
    /// Process all status effects at the start of a card's turn
    /// Returns true if card can act this turn, false if stunned
    /// </summary>
    public static bool ProcessStatusEffects(CardRuntime card)
    {
        // Regeneration (heal first)
        if (card.regenerationStacks > 0)
        {
            int healAmount = Mathf.RoundToInt(card.maxHealth * 0.075f * card.regenerationStacks);
            card.Heal(healAmount);
            Debug.Log($"[Regeneration] Healed {healAmount} HP ({card.regenerationStacks} stacks)");
        }

        // Burn damage
        if (card.burnStacks > 0)
        {
            int burnDamage = Mathf.RoundToInt(card.maxHealth * 0.10f * card.burnStacks);
            card.TakeTrueDamage(burnDamage);
            Debug.Log($"<color=orange>[Burn] Dealt {burnDamage} true damage ({card.burnStacks} stacks)</color>");
        }

        // Poison damage (with miss chance)
        if (card.poisonStacks > 0)
        {
            int missChance = 10 + (card.poisonTurns * 5); // 10% base + 5% per turn
            int roll = Random.Range(0, 100);
            
            if (roll >= missChance) // Poison hits
            {
                int poisonDamage = 5 + Mathf.RoundToInt(card.maxHealth * 0.05f * card.poisonStacks);
                card.TakeTrueDamage(poisonDamage);
                Debug.Log($"<color=green>[Poison] Dealt {poisonDamage} true damage ({card.poisonStacks} stacks)</color>");
            }
            else
            {
                Debug.Log($"<color=green>[Poison] Missed! ({missChance}% miss chance)</color>");
            }
            
            card.poisonTurns++;
        }

        // Silence countdown
        if (card.silenceTurns > 0)
        {
            card.silenceTurns--;
            Debug.Log($"[Silence] {card.silenceTurns} turns remaining");
        }

        // Endurance countdown
        if (card.hasEndurance && card.enduranceTurns > 0)
        {
            card.enduranceTurns--;
            if (card.enduranceTurns == 0)
            {
                card.hasEndurance = false;
                Debug.Log("[Endurance] Buff expired");
            }
        }

        // Stun check
        if (card.isStunned)
        {
            card.isStunned = false; // Clear stun after skipping turn
            Debug.Log("<color=yellow>[Stun] Turn skipped!</color>");
            return false; // Card cannot act
        }

        return true; // Card can act
    }
}
