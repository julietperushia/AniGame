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

    /// <summary>Sheet card (as the player, with a random Common build of its talent) vs sheet card (as a floor enemy).</summary>
    public void StartBattle(Card playerCard, Card enemyCard)
    {
        var rarity = playerCard != null ? playerCard.rarity : CardRarity.Common;
        var instance = new CardInstance { uid = 0, key = playerCard != null ? playerCard.Key : "", rarity = rarity };
        instance.build = PlayerProfile.RandomBuild(rarity, playerCard != null && playerCard.talent != null ? playerCard.talent.DisplayName : null);
        StartBattle(CardRuntime.ForBuild(instance, instance.build), new CardRuntime(enemyCard));
    }

    /// <summary>Runs a battle between two prepared fighters.</summary>
    public void StartBattle(CardRuntime playerFighter, CardRuntime enemyFighter)
    {
        player = playerFighter;
        enemy = enemyFighter;
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
