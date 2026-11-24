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
        if (target.ResistsDebuff()) return;
        target.burnPercent = target.burnStacks > 0 ? Mathf.Max(target.burnPercent, percentPerStack) : percentPerStack;
        target.burnStacks = Mathf.Min(MaxBurnStacks, target.burnStacks + stacks);
        target.healingReduction = 0.75f;
        BattleLog.Status(target, $"<color=#FF8A3D>{target.DisplayName} is burning! (x{target.burnStacks})</color>");
    }

    /// <summary>Apply Poison status: deals 5 + 5% max HP true damage per turn. 10% base miss chance +5% per turn.</summary>
    public static void ApplyPoison(CardRuntime target, int stacks = 1, float percentPerStack = 0.05f)
    {
        if (target.ResistsDebuff()) return;
        target.poisonPercent = target.poisonStacks > 0 ? Mathf.Max(target.poisonPercent, percentPerStack) : percentPerStack;
        target.poisonStacks += stacks;
        target.poisonTurns = 0; // Reset turn counter
        BattleLog.Status(target, $"<color=#7CFC6A>{target.DisplayName} is poisoned! (x{target.poisonStacks})</color>");
    }

    /// <summary>Apply Stun: skips next turn, permanently reduces DEF by 10%.</summary>
    public static void ApplyStun(CardRuntime target)
    {
        if (target.ResistsDebuff()) return;
        target.isStunned = true;
        int defReduction = target.baseDefense * 10 / 100;
        target.defense -= defReduction;
        target.baseDefense -= defReduction; // Permanent reduction
        BattleLog.Status(target, $"<color=#FFE14D>{target.DisplayName} is stunned! DEF -{defReduction} permanently</color>");
    }

    /// <summary>Apply Silence: prevents talent usage for the given turns and caps mana at 100.</summary>
    public static void ApplySilence(CardRuntime target, int turns = 3, bool capMana = true)
    {
        if (target.ResistsDebuff()) return;
        target.silenceTurns = Mathf.Max(target.silenceTurns, turns);
        if (capMana)
        {
            target.maxMana = 100;
            target.mana = Mathf.Min(target.mana, 100);
        }
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

    /// <summary>Frostbite: the target skips its next turn (no DEF loss).</summary>
    public static void ApplyFreeze(CardRuntime target)
    {
        if (target.ResistsDebuff()) return;
        target.frozen = true;
        BattleLog.Status(target, $"<color=#81D4FA>{target.DisplayName} is frozen solid!</color>");
    }

    /// <summary>Sleep: skips up to <paramref name="turns"/> turns; a normal attack wakes it (+40% on that hit).</summary>
    public static void ApplySleep(CardRuntime target, int turns = 2)
    {
        if (target.ResistsDebuff()) return;
        target.sleepTurns = Mathf.Max(target.sleepTurns, turns);
        BattleLog.Status(target, $"<color=#9FA8DA>{target.DisplayName} falls asleep...</color>");
    }

    /// <summary>Blind: the target's normal attacks have a 50% chance to miss.</summary>
    public static void ApplyBlind(CardRuntime target, int turns = 2)
    {
        if (target.ResistsDebuff()) return;
        target.blindTurns = Mathf.Max(target.blindTurns, turns);
        BattleLog.Status(target, $"<color=#B0BEC5>{target.DisplayName} is blinded for {turns} turns!</color>");
    }

    /// <summary>Hex: swaps the target's ATK and DEF (swapped back when it ends).</summary>
    public static void ApplyHex(CardRuntime target, int turns = 2)
    {
        if (target.ResistsDebuff()) return;
        if (target.hexTurns <= 0)
        {
            int a = target.attack;
            target.attack = target.defense;
            target.defense = a;
        }
        target.hexTurns = Mathf.Max(target.hexTurns, turns);
        BattleLog.Status(target, $"<color=#CE93D8>{target.DisplayName} is hexed: ATK and DEF swapped!</color>");
    }

    public static void EndHex(CardRuntime target)
    {
        if (target.hexTurns <= 0) return;
        target.hexTurns = 0;
        int a = target.attack;
        target.attack = target.defense;
        target.defense = a;
        BattleLog.Status(target, $"{target.DisplayName}'s hex wears off");
    }

    /// <summary>Hypnotize: +1 stack (+20% chance to hit itself). Not while immune after a trigger.</summary>
    public static void ApplyHypnotize(CardRuntime target)
    {
        if (target.hypnoImmuneTurns > 0)
        {
            BattleLog.Status(target, $"<color=#BA68C8>{target.DisplayName} resists the hypnosis ({target.hypnoImmuneTurns} turns)</color>");
            return;
        }
        if (target.ResistsDebuff()) return;
        target.hypnoStacks++;
        BattleLog.Status(target, $"<color=#BA68C8>{target.DisplayName} is hypnotized (x{target.hypnoStacks}, {20 * target.hypnoStacks}% to hit itself)</color>");
    }

    /// <summary>Removes every negative effect (Purify). Returns how many were removed.</summary>
    public static int Cleanse(CardRuntime card)
    {
        int n = 0;
        if (card.burnStacks > 0) { card.burnStacks = 0; card.healingReduction = 0f; n++; }
        if (card.poisonStacks > 0) { card.poisonStacks = 0; card.poisonTurns = 0; n++; }
        if (card.isStunned) { card.isStunned = false; n++; }
        if (card.silenceTurns > 0) { card.silenceTurns = 0; n++; }
        if (card.frozen) { card.frozen = false; n++; }
        if (card.sleepTurns > 0) { card.sleepTurns = 0; n++; }
        if (card.blindTurns > 0) { card.blindTurns = 0; n++; }
        if (card.hexTurns > 0) { EndHex(card); n++; }
        if (card.hypnoStacks > 0) { card.hypnoStacks = 0; n++; }
        if (card.attack < card.baseAttack) { card.attack = card.baseAttack; n++; }
        if (card.defense < card.baseDefense) { card.defense = card.baseDefense; n++; }
        if (card.speed < card.baseSpeed) { card.speed = card.baseSpeed; n++; }
        if (card.bonusAttack < 0) { card.bonusAttack = 0; n++; }
        if (card.bonusDefense < 0) { card.bonusDefense = 0; n++; }
        return n;
    }

    /// <summary>End of this card's own turn: Blind / Hex / hypnosis immunity count down.</summary>
    public static void EndOfTurn(CardRuntime card)
    {
        if (card.blindTurns > 0) card.blindTurns--;
        if (card.hypnoImmuneTurns > 0) card.hypnoImmuneTurns--;
        if (card.hexTurns > 0 && --card.hexTurns == 0) { card.hexTurns = 1; EndHex(card); }
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
        card.skipReason = null;

        // Shield and immunity count down on the owner's turn
        if (card.shieldTurns > 0 && --card.shieldTurns == 0 && card.shield > 0)
        {
            card.shield = 0;
            card.shieldBurst = 0;
            BattleLog.Status(card, $"{card.DisplayName}'s barrier fades");
        }
        if (card.debuffImmuneTurns > 0) card.debuffImmuneTurns--;

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

        // Stun / Freeze / Sleep
        if (card.isStunned)
        {
            card.isStunned = false; // Clear stun after skipping turn
            card.skipReason = "stunned";
            return false;
        }
        if (card.frozen)
        {
            card.frozen = false;
            card.skipReason = "frozen";
            return false;
        }
        if (card.sleepTurns > 0)
        {
            card.sleepTurns--;
            card.skipReason = "asleep";
            return false;
        }

        return true;
    }
}
