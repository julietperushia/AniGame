using UnityEngine;
using System;

public enum BattleState
{
    Ongoing,
    PlayerWon,
    EnemyWon,
    Draw
}

public class BattleSystem : MonoBehaviour
{
    CardRuntime player;
    CardRuntime enemy;

    int turnNumber = 1;
    public BattleState battleState = BattleState.Ongoing;

    // Events for UI integration
    public event Action<CardRuntime, CardRuntime> OnBattleEnd;
    public event Action<int> OnRoundEnd;
    public event Action<CardRuntime, int, bool, bool> OnDamageDealt; // attacker, damage, wasCrit, wasEvaded

    public void StartBattle(Card playerCard, Card enemyCard)
    {
        player = new CardRuntime(playerCard);
        enemy = new CardRuntime(enemyCard);
        battleState = BattleState.Ongoing;
        turnNumber = 1;

        player.talent?.OnBattleStart(player, enemy);
        enemy.talent?.OnBattleStart(enemy, player);

        StartRound();
    }

    void StartRound()
    {
        if (battleState != BattleState.Ongoing)
            return;

        CardRuntime first;
        CardRuntime second;

        if (player.speed >= enemy.speed)
        {
            first = player;
            second = enemy;
        }
        else
        {
            first = enemy;
            second = player;
        }

        ExecuteTurn(first, second);
        
        // Check if battle ended after first turn
        if (CheckBattleEnd())
            return;

        ExecuteTurn(second, first);

        // Check if battle ended after second turn
        if (CheckBattleEnd())
            return;

        OnRoundEnd?.Invoke(turnNumber);
        turnNumber++;
        
        // Continue to next round
        StartRound();
    }

    void ExecuteTurn(CardRuntime attacker, CardRuntime defender)
    {
        if (!attacker.IsAlive || !defender.IsAlive)
            return;

        // Process status effects (Burn, Poison, Regeneration, etc.)
        bool canAct = StatusEffects.ProcessStatusEffects(attacker);
        if (!canAct) // Stunned, skip turn
        {
            return;
        }

        attacker.talent?.OnTurnStart(attacker, defender);

        bool usedSkill = false;

        // Check if card can use active talent (mana >= 100, not silenced)
        if (attacker.talent is ActiveTalent active &&
            attacker.mana >= active.manaCost &&
            attacker.silenceTurns == 0) // Not silenced
        {
            attacker.mana -= active.manaCost;
            active.Activate(attacker, defender);
            usedSkill = true;
        }
        else
        {
            NormalAttack(attacker, defender);
        }

        GainAttackMana(attacker, usedSkill);
        attacker.TickBuffs();
    }

    void NormalAttack(CardRuntime attacker, CardRuntime defender)
    {
        attacker.talent?.OnBeforeAttack(attacker, defender);

        // Check evasion first
        bool evaded = CheckEvasion(defender);
        if (evaded)
        {
            attacker.lastDamageDealt = 0;
            attacker.lastAttackWasCrit = false;
            OnDamageDealt?.Invoke(attacker, 0, false, true);
            defender.GainMana(5);
            return;
        }

        // Calculate base damage using Anigame formula
        int damage = CalculateDamage(attacker, defender, out bool isCrit);

        // Apply Endurance buff (65% damage reduction for normal attacks + 35% reflect)
        if (defender.hasEndurance)
        {
            int originalDamage = damage;
            damage = Mathf.RoundToInt(originalDamage * 0.35f); // 65% reduction = 35% remaining
            
            int blockedDamage = originalDamage - damage;
            int reflectDamage = Mathf.RoundToInt(blockedDamage * 0.35f);
            
            if (reflectDamage > 0)
            {
                attacker.TakeTrueDamage(reflectDamage);
                Debug.Log($"<color=cyan>[Endurance] Blocked {blockedDamage} damage. Reflecting {reflectDamage} true damage back to {attacker.baseCard.cardName}!</color>");
            }
            
            Debug.Log($"[Endurance] Reduced damage by 65% ({damage} damage taken)");
        }

        // Allow talents to modify damage
        defender.talent?.OnTakeDamage(defender, ref damage);

        // Apply damage
        defender.TakeDamage(damage);
        attacker.lastDamageDealt = damage;
        attacker.lastAttackWasCrit = isCrit;

        // Defender gains mana when hit
        defender.GainMana(5);

        // Trigger damage event
        OnDamageDealt?.Invoke(attacker, damage, isCrit, false);

        attacker.talent?.OnAfterAttack(attacker, defender);
    }

    /// <summary>
    /// Calculates damage using the Anigame formula:
    /// damage = ((ATK * 10 * ATK + 638000) / (8.7 * DEF * 10)) * ELE_MUL * 10
    /// </summary>
    int CalculateDamage(CardRuntime attacker, CardRuntime defender, out bool isCrit)
    {
        float atk = attacker.TotalAttack;
        float def = defender.TotalDefense;

        // Anigame damage formula
        float baseDamage = ((atk * 10f * atk + 638000f) / (8.7f * def * 10f)) * 10f;

        // Apply element multiplier
        float elementMul = ElementSystem.GetElementMultiplier(
            attacker.baseCard.element, 
            defender.baseCard.element
        );
        baseDamage *= elementMul;

        // Check for critical hit
        isCrit = CheckCriticalHit(attacker);
        if (isCrit)
        {
            baseDamage *= (attacker.critDamage / 100f);
        }

        return Mathf.Max(1, Mathf.RoundToInt(baseDamage));
    }

    /// <summary>
    /// Checks if a critical hit occurs based on crit rate
    /// </summary>
    bool CheckCriticalHit(CardRuntime attacker)
    {
        int roll = UnityEngine.Random.Range(0, 100);
        return roll < attacker.critRate;
    }

    /// <summary>
    /// Checks if an attack is evaded based on evasion stat
    /// </summary>
    bool CheckEvasion(CardRuntime defender)
    {
        int roll = UnityEngine.Random.Range(0, 100);
        return roll < defender.evasion;
    }

    void GainAttackMana(CardRuntime attacker, bool usedSkill)
    {
        int manaGain;

        if (usedSkill)
        {
            manaGain = 10;
        }
        else
        {
            manaGain = Mathf.CeilToInt(80f / turnNumber) + 20;
        }

        attacker.GainMana(manaGain);
    }

    /// <summary>
    /// Checks if battle has ended and updates battle state
    /// </summary>
    bool CheckBattleEnd()
    {
        bool playerAlive = player.IsAlive;
        bool enemyAlive = enemy.IsAlive;

        if (!playerAlive && !enemyAlive)
        {
            battleState = BattleState.Draw;
            OnBattleEnd?.Invoke(player, enemy);
            return true;
        }
        else if (!enemyAlive)
        {
            battleState = BattleState.PlayerWon;
            OnBattleEnd?.Invoke(player, enemy);
            return true;
        }
        else if (!playerAlive)
        {
            battleState = BattleState.EnemyWon;
            OnBattleEnd?.Invoke(player, enemy);
            return true;
        }

        return false;
    }
}
