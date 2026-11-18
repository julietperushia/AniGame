using UnityEngine;
using System;

/// <summary>
/// Legacy MonoBehaviour wrapper kept for BattleSystemTest / quick console battles.
/// It runs a <see cref="BattleEngine"/> instantly and raises the original events.
/// The animated, UI-driven battle lives in BattleScreen.
/// </summary>
public class BattleSystem : MonoBehaviour
{
    // Field names kept: BattleSystemTest reads them through reflection.
    CardRuntime player;
    CardRuntime enemy;

    public BattleState battleState = BattleState.Ongoing;

    [Tooltip("Print every battle event to the Console.")]
    public bool logToConsole = true;

    public BattleEngine Engine { get; private set; }
    public CardRuntime Player => player;
    public CardRuntime Enemy => enemy;

    // Events for UI integration
    public event Action<CardRuntime, CardRuntime> OnBattleEnd;
    public event Action<int> OnRoundEnd;
    public event Action<CardRuntime, int, bool, bool> OnDamageDealt; // attacker, damage, wasCrit, wasEvaded

    public void StartBattle(Card playerCard, Card enemyCard)
    {
        StartBattle(playerCard, 1, 0, enemyCard, 1, 0);
    }

    public void StartBattle(Card playerCard, int playerLevel, int playerEvo, Card enemyCard, int enemyLevel, int enemyEvo)
    {
        player = new CardRuntime(playerCard, playerLevel, playerEvo);
        enemy = new CardRuntime(enemyCard, enemyLevel, enemyEvo);
        battleState = BattleState.Ongoing;

        Engine = new BattleEngine(player, enemy, Environment.TickCount);
        Engine.Begin();

        int processed = 0;
        int guard = 0;
        while (guard++ < 10000)
        {
            for (; processed < Engine.Events.Count; processed++) Dispatch(Engine.Events[processed]);
            if (Engine.IsOver) break;
            Engine.Step();
        }
        for (; processed < Engine.Events.Count; processed++) Dispatch(Engine.Events[processed]);

        battleState = Engine.State;
        OnBattleEnd?.Invoke(player, enemy);
    }

    void Dispatch(BattleEvent e)
    {
        if (logToConsole && !string.IsNullOrEmpty(e.text) && e.type != BattleEventType.TurnStart)
            Debug.Log($"[R{e.round}] {e.text}");

        switch (e.type)
        {
            case BattleEventType.Evaded:
                OnDamageDealt?.Invoke(e.actor, 0, false, true);
                break;
            case BattleEventType.Damage:
                if (e.actor != null && e.actor != e.target && (e.kind == DamageKind.Normal || e.kind == DamageKind.Critical))
                    OnDamageDealt?.Invoke(e.actor, e.amount, e.crit, false);
                break;
            case BattleEventType.RoundStart:
                if (e.round > 1) OnRoundEnd?.Invoke(e.round - 1);
                break;
        }
    }
}
