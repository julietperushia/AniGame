using UnityEngine;

/// <summary>
/// Status effects in battle. Every number comes from the talent that applies the status (its ScriptableObject
/// fields), passed in here and stored on the target; the defaults below are only used when nothing is passed.
/// </summary>
public static class StatusEffects
{
    public const float BurnPercentPerStack = 0.05f;
    public const int MaxBurnStacks = 4;

    /// <summary>Apply Burn status: 5% max HP true damage per stack per turn (max 4), reduces healing by 75%.
    /// Each tick can miss (missBase% with one stack, +missPerStack% per extra stack); a miss removes every stack.</summary>
    public static void ApplyBurn(CardRuntime target, int stacks = 1, float percentPerStack = BurnPercentPerStack,
                                 int maxStacks = MaxBurnStacks, float healingCut = 0.75f, int missBase = 15, int missPerStack = 5)
    {
        if (target.ResistsDebuff()) return;
        target.burnMissBase = missBase;
        target.burnMissPerStack = missPerStack;
        target.burnPercent = target.burnStacks > 0 ? Mathf.Max(target.burnPercent, percentPerStack) : percentPerStack;
        target.burnStacks = Mathf.Min(Mathf.Max(1, maxStacks), target.burnStacks + Mathf.Max(1, stacks));
        target.healingReduction = Mathf.Clamp01(healingCut);
        BattleLog.Status(target, $"<color=#FF8A3D>{target.DisplayName} is burning! (x{target.burnStacks})</color>");
    }

    /// <summary>Apply Poison status: deals 5 + 5% max HP true damage per stack per turn.
    /// Each tick can miss (missBase% with one stack, +missPerStack% per extra stack); a miss removes every stack.</summary>
    public static void ApplyPoison(CardRuntime target, int stacks = 1, float percentPerStack = 0.05f,
                                   int flatDamage = 5, int missBase = 15, int missPerStack = 5)
    {
        if (target.ResistsDebuff()) return;
        target.poisonFlat = flatDamage;
        target.poisonMissBase = missBase;
        target.poisonMissPerStack = missPerStack;
        target.poisonPercent = target.poisonStacks > 0 ? Mathf.Max(target.poisonPercent, percentPerStack) : percentPerStack;
        target.poisonStacks += stacks;
        BattleLog.Status(target, $"<color=#7CFC6A>{target.DisplayName} is poisoned! (x{target.poisonStacks})</color>");
    }

    /// <summary>Apply Stun: skips next turn, permanently reduces DEF by 10%.</summary>
    public static void ApplyStun(CardRuntime target, int defLossPercent = 10)
    {
        if (target.ResistsDebuff()) return;
        target.isStunned = true;
        int defReduction = target.baseDefense * Mathf.Max(0, defLossPercent) / 100;
        target.defense -= defReduction;
        target.baseDefense -= defReduction; // Permanent reduction
        BattleLog.Status(target, $"<color=#FFE14D>{target.DisplayName} is stunned! DEF -{defReduction} permanently</color>");
    }

    /// <summary>Apply Silence: prevents talent usage for the given turns and caps mana at 100.</summary>
    public static void ApplySilence(CardRuntime target, int turns = 3, bool capMana = true, int manaCap = 100)
    {
        if (target.ResistsDebuff()) return;
        target.silenceTurns = Mathf.Max(target.silenceTurns, turns);
        if (capMana)
        {
            // 3v3: the cap is per card, so a team keeps its starting pool scaled the same way
            int cap = target.IsTeam ? Mathf.RoundToInt(target.basePool * manaCap / 100f) : manaCap;
            target.maxMana = Mathf.Min(target.maxMana, cap);
            target.mana = Mathf.Min(target.mana, target.maxMana);
        }
        BattleLog.Status(target, $"<color=#C78BFF>{target.DisplayName} is silenced for {turns} turns!</color>");
    }

    /// <summary>Apply Endurance: reduces normal attack damage by 65% for the given turns.</summary>
    public static void ApplyEndurance(CardRuntime target, int turns = 3, float reflectPercent = 0.35f, float reduction = 0.65f)
    {
        target.enduranceReflect = reflectPercent;
        target.enduranceReduction = Mathf.Clamp01(reduction);
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

    /// <summary>Sleep: skips <paramref name="turns"/> turns (attacks don't wake it). While it sleeps, normal attacks on it deal
    /// <paramref name="damageCut"/> less and the opponent's Mana Regeneration is multiplied by <paramref name="casterRegen"/>.</summary>
    public static void ApplySleep(CardRuntime target, int turns = 2, float damageCut = 0.3f, float casterRegen = 0.5f)
    {
        if (target.ResistsDebuff()) return;
        target.sleepDamageCut = Mathf.Clamp01(damageCut);
        target.sleepCasterRegen = Mathf.Max(0f, casterRegen);
        target.sleepTurns = Mathf.Max(target.sleepTurns, turns);
        BattleLog.Status(target, $"<color=#9FA8DA>{target.DisplayName} falls asleep...</color>");
    }

    /// <summary>Blind: the target's normal attacks have a 50% chance to miss.</summary>
    public static void ApplyBlind(CardRuntime target, int turns = 2, int missChance = 50)
    {
        if (target.ResistsDebuff()) return;
        target.blindMissChance = missChance;
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
    public static void ApplyHypnotize(CardRuntime target, int stacks = 1, int chancePerStack = 20, float selfDamage = 0.5f, int immuneTurns = 2)
    {
        if (target.hypnoImmuneTurns > 0)
        {
            BattleLog.Status(target, $"<color=#BA68C8>{target.DisplayName} resists the hypnosis ({target.hypnoImmuneTurns} turns)</color>");
            return;
        }
        if (target.ResistsDebuff()) return;
        target.hypnoChancePerStack = chancePerStack;
        target.hypnoSelfDamage = selfDamage;
        target.hypnoImmuneAfter = immuneTurns;
        target.hypnoStacks += Mathf.Max(1, stacks);
        BattleLog.Status(target, $"<color=#BA68C8>{target.DisplayName} is hypnotized (x{target.hypnoStacks}, {chancePerStack * target.hypnoStacks}% to hit itself)</color>");
    }

    /// <summary>Chance (%) the next Regeneration tick fails (heals nothing, loses one stack).</summary>
    public static int RegenMissChance(CardRuntime card) =>
        Mathf.Clamp(card.regenMissBase + card.regenMissPerStack * Mathf.Max(0, card.regenerationStacks - 1), 0, 100);

    /// <summary>Chance (%) the next Burn tick misses and removes every stack.</summary>
    public static int BurnMissChance(CardRuntime card) =>
        Mathf.Clamp(card.burnMissBase + card.burnMissPerStack * Mathf.Max(0, card.burnStacks - 1), 0, 100);

    /// <summary>Chance (%) the next Poison tick misses and removes every stack.</summary>
    public static int PoisonMissChance(CardRuntime card) =>
        Mathf.Clamp(card.poisonMissBase + card.poisonMissPerStack * Mathf.Max(0, card.poisonStacks - 1), 0, 100);

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
        if (card.manaBlockTurns > 0) { card.manaBlockTurns = 0; n++; }
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
        if (card.manaBlockTurns > 0 && --card.manaBlockTurns == 0) BattleLog.Status(card, $"{card.DisplayName}'s Mana Regeneration is back");
        if (card.hexTurns > 0 && --card.hexTurns == 0) { card.hexTurns = 1; EndHex(card); }
    }

    /// <summary>
    /// Process all status effects at the start of a card's turn.
    /// Returns true if the card can act this turn, false if stunned.
    /// </summary>
    public static bool ProcessStatusEffects(CardRuntime card)
    {
        if (!TickStatuses(card)) return false;
        return ConsumeSkip(card);
    }

    /// <summary>
    /// Regeneration, Burn, Poison and the shield / immunity / silence / endurance countdowns.
    /// 1v1: every turn. 3v3: once per round per team (on its first card's turn). Returns false if the card died.
    /// </summary>
    public static bool TickStatuses(CardRuntime card)
    {
        // Regeneration (heal first). A tick can miss: then it heals nothing and loses one stack
        if (card.regenerationStacks > 0)
        {
            int missChance = RegenMissChance(card);
            if (BattleRandom.Chance(missChance))
            {
                card.regenerationStacks--;
                BattleLog.Status(card, $"<color=#5DFFB0>{card.DisplayName}'s Regeneration fails and loses a stack (x{card.regenerationStacks} left, {missChance}% chance)</color>");
            }
            else
            {
                int healAmount = Mathf.RoundToInt(card.maxHealth * card.regenPercent * card.regenerationStacks);
                card.Heal(healAmount);
            }
        }

        // Burn damage (a miss puts the fire out: every stack is removed)
        if (card.burnStacks > 0 && card.IsAlive)
        {
            int missChance = BurnMissChance(card);
            if (BattleRandom.Chance(missChance))
            {
                BattleLog.Status(card, $"<color=#FF8A3D>The burn fizzles out on {card.DisplayName}! All {card.burnStacks} stacks removed ({missChance}% chance)</color>");
                card.burnStacks = 0;
                card.healingReduction = 0f;
            }
            else
            {
                int burnDamage = Mathf.Max(1, Mathf.RoundToInt(card.maxHealth * card.burnPercent * card.burnStacks));
                card.TakeTrueDamage(burnDamage, DamageKind.Burn);
            }
        }

        // Poison damage (a miss cures it: every stack is removed)
        if (card.poisonStacks > 0 && card.IsAlive)
        {
            int missChance = PoisonMissChance(card);
            if (BattleRandom.Chance(missChance))
            {
                BattleLog.Status(card, $"<color=#7CFC6A>The poison misses {card.DisplayName} and wears off! All {card.poisonStacks} stacks removed ({missChance}% chance)</color>");
                card.poisonStacks = 0;
                card.poisonTurns = 0;
            }
            else
            {
                int poisonDamage = card.poisonFlat + Mathf.RoundToInt(card.maxHealth * card.poisonPercent * card.poisonStacks);
                card.TakeTrueDamage(poisonDamage, DamageKind.Poison);
                card.poisonTurns++;
            }
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
        return true;
    }

    /// <summary>Stun / Freeze / Sleep: returns false (and sets skipReason) if this turn is skipped.
    /// 3v3: called for every card turn, so a Stun only skips the team's next card.</summary>
    public static bool ConsumeSkip(CardRuntime card)
    {
        card.skipReason = null;
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
