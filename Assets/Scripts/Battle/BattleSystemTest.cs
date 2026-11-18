using UnityEngine;

/// <summary>
/// Test script for the Battle System - attach to a GameObject and assign cards in Inspector
/// </summary>
public class BattleSystemTest : MonoBehaviour
{
    [Header("Cards to Battle")]
    public Card playerCard;
    public Card enemyCard;

    [Header("Auto Start")]
    public bool startOnAwake = true;

    [Header("Manual Start")]
    [Tooltip("Call StartBattle() from a UI button or other script to manually start")]
    public bool manualStart = false;

    private BattleSystem battleSystem;
    private CardRuntime playerRuntime;
    private CardRuntime enemyRuntime;

    void Awake()
    {
        battleSystem = GetComponent<BattleSystem>();
        
        if (battleSystem == null)
        {
            battleSystem = gameObject.AddComponent<BattleSystem>();
        }

        // Subscribe to battle events for debugging
        battleSystem.OnDamageDealt += OnDamageDealt;
        battleSystem.OnRoundEnd += OnRoundEnd;
        battleSystem.OnBattleEnd += OnBattleEnd;

        if (startOnAwake && playerCard != null && enemyCard != null)
        {
            StartBattle();
        }
    }

    // Removed Update() method to fix Input System compatibility
    // To manually start battles, call StartBattle() from a UI button or other script

    public void StartBattle()
    {
        if (playerCard == null || enemyCard == null)
        {
            Debug.LogError("Please assign both Player Card and Enemy Card in the Inspector!");
            return;
        }

        Debug.Log($"<color=cyan>===== BATTLE START =====</color>");
        Debug.Log($"<color=green>Player: {playerCard.cardName}</color> (HP: {playerCard.health}, ATK: {playerCard.attack}, DEF: {playerCard.defense}, SPD: {playerCard.speed})");
        Debug.Log($"<color=red>Enemy: {enemyCard.cardName}</color> (HP: {enemyCard.health}, ATK: {enemyCard.attack}, DEF: {enemyCard.defense}, SPD: {enemyCard.speed})");
        Debug.Log($"Element: <color=green>{playerCard.element}</color> vs <color=red>{enemyCard.element}</color>");
        
        float eleMul = ElementSystem.GetElementMultiplier(playerCard.element, enemyCard.element);
        Debug.Log($"Element Multiplier: {eleMul}x damage for player");

        battleSystem.StartBattle(playerCard, enemyCard);
    }
    
    void GetRuntimeReferences()
    {
        if (playerRuntime == null || enemyRuntime == null)
        {
            // Access via reflection since they're private in BattleSystem
            var playerField = typeof(BattleSystem).GetField("player", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var enemyField = typeof(BattleSystem).GetField("enemy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            playerRuntime = (CardRuntime)playerField.GetValue(battleSystem);
            enemyRuntime = (CardRuntime)enemyField.GetValue(battleSystem);
        }
    }

    void OnDamageDealt(CardRuntime attacker, int damage, bool wasCrit, bool wasEvaded)
    {
        // Ensure we have runtime references (lazy load if needed since events fire during StartBattle)
        GetRuntimeReferences();
        
        // Determine who is who
        bool isPlayerAttacking = (attacker == playerRuntime);
        string attackerName = isPlayerAttacking ? "Player" : "Enemy";
        string defenderName = isPlayerAttacking ? "Enemy" : "Player";
        
        if (wasEvaded)
        {
            Debug.Log($"<color=yellow>⚡ {defenderName} EVADED the attack!</color>");
        }
        else if (wasCrit)
        {
            Debug.Log($"<color=orange>💥 {attackerName} landed a CRITICAL HIT for {damage} damage!</color>");
        }
        else
        {
            Debug.Log($"{attackerName} dealt {damage} damage to {defenderName}");
        }

        // Show current HP (after damage is applied)
        Debug.Log($"HP: <color=green>Player {playerRuntime.health}/{playerRuntime.maxHealth}</color> | <color=red>Enemy {enemyRuntime.health}/{enemyRuntime.maxHealth}</color>");
    }

    void OnRoundEnd(int roundNumber)
    {
        Debug.Log($"<color=cyan>----- Round {roundNumber} End -----</color>");
    }

    void OnBattleEnd(CardRuntime player, CardRuntime enemy)
    {
        Debug.Log($"<color=cyan>===== BATTLE END =====</color>");
        
        switch (battleSystem.battleState)
        {
            case BattleState.PlayerWon:
                Debug.Log($"<color=green>🎉 VICTORY! Player wins!</color>");
                Debug.Log($"Player HP remaining: {player.health}/{player.maxHealth}");
                break;
                
            case BattleState.EnemyWon:
                Debug.Log($"<color=red>💀 DEFEAT! Enemy wins!</color>");
                Debug.Log($"Enemy HP remaining: {enemy.health}/{enemy.maxHealth}");
                break;
                
            case BattleState.Draw:
                Debug.Log($"<color=yellow>⚔️ DRAW! Both fighters fell!</color>");
                break;
        }
    }

    void OnDestroy()
    {
        if (battleSystem != null)
        {
            battleSystem.OnDamageDealt -= OnDamageDealt;
            battleSystem.OnRoundEnd -= OnRoundEnd;
            battleSystem.OnBattleEnd -= OnBattleEnd;
        }
    }
}
