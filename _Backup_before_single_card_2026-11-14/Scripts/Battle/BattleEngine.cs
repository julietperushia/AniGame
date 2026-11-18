using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleState
{
    Ongoing,
    PlayerWon,
    EnemyWon,
    Draw
}

/// <summary>
/// Pure C# 1v1 Anigame battle simulation (no MonoBehaviour, no timing).
/// Call Begin() once, then Step() repeatedly: each Step executes exactly one card's turn and appends
/// BattleEvents to <see cref="Events"/>. The UI replays those events with animations; editor tools and
/// BattleSystem can call RunToEnd() for an instant result.
/// </summary>
public class BattleEngine
{
    public CardRuntime Player { get; }
    public CardRuntime Enemy { get; }
    public int Round { get; private set; }
    public BattleState State { get; private set; } = BattleState.Ongoing;
    public bool IsOver => State != BattleState.Ongoing;

    /// <summary>After this many rounds the battle is a draw (prevents endless stalls).</summary>
    public int MaxRounds = 50;

    /// <summary>From this round on both fighters enrage (like Anigame raid bosses): +EnragePercent% base ATK every round.</summary>
    public int EnrageStartRound = 20;
    public int EnragePercent = 10;

    public readonly List<BattleEvent> Events = new List<BattleEvent>();

    /// <summary>Card whose turn is being executed (used to attribute damage events).</summary>
    public CardRuntime CurrentActor { get; private set; }

    CardRuntime first;
    CardRuntime second;
    int turnInRound; // 0 = first card acts next, 1 = second card acts next
    bool begun;

    public BattleEngine(CardRuntime player, CardRuntime enemy, int seed = 0)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
        Player.isPlayer = true;
        Enemy.isPlayer = false;
        BattleRandom.Seed(seed);
    }

    /// <summary>Triggers battle-start talents. Set runEnemyStart=false when a new player card swaps in
    /// against an enemy that is already in the fight (relay battles).</summary>
    public void Begin(bool runPlayerStart = true, bool runEnemyStart = true)
    {
        if (begun) return;
        begun = true;
        BattleLog.Attach(this);
        try
        {
            var start = Emit(BattleEventType.BattleStart, Player, Enemy);
            start.text = $"<b>{Player.DisplayName}</b> vs <b>{Enemy.DisplayName}</b>";
            start.elementMultiplier = ElementSystem.GetElementMultiplier(Player.Element, Enemy.Element);

            if (runPlayerStart) RunTalent(Player, () => Player.talent.OnBattleStart(Player, Enemy));
            if (runEnemyStart) RunTalent(Enemy, () => Enemy.talent.OnBattleStart(Enemy, Player));
            CurrentActor = null;
            CheckBattleEnd();
        }
        finally
        {
            BattleLog.Detach(this);
        }
    }

    /// <summary>Executes the next card's turn. Returns false if the battle is already over.</summary>
    public bool Step()
    {
        if (!begun) Begin();
        if (IsOver) return false;

        BattleLog.Attach(this);
        try
        {
            if (turnInRound == 0)
            {
                Round++;
                if (Round > MaxRounds)
                {
                    Round = MaxRounds;
                    Finish(BattleState.Draw, "Time limit reached");
                    return false;
                }
                DetermineTurnOrder();
                var rs = Emit(BattleEventType.RoundStart, first, second);
                rs.text = $"<color=#9AA4B5>- Round {Round} -</color>";
                if (Round >= EnrageStartRound) ApplyEnrage();
            }

            CardRuntime actor = turnInRound == 0 ? first : second;
            CardRuntime target = actor == Player ? Enemy : Player;
            turnInRound = (turnInRound + 1) % 2;

            ExecuteTurn(actor, target);
            CurrentActor = null;
            CheckBattleEnd();
        }
        finally
        {
            BattleLog.Detach(this);
        }
        return true;
    }

    /// <summary>Runs the whole battle instantly.</summary>
    public BattleState RunToEnd()
    {
        Begin();
        int guard = 0;
        while (!IsOver && guard++ < 10000) Step();
        return State;
    }

    void ApplyEnrage()
    {
        foreach (var card in new[] { Player, Enemy })
        {
            if (!card.IsAlive) continue;
            card.attack += Mathf.Max(1, card.baseAttack * EnragePercent / 100);
        }
        var e = Emit(BattleEventType.Info, null, null);
        e.text = $"<color=#FF6B6B>ENRAGE! Both fighters gain +{EnragePercent}% ATK</color>";
    }

    void DetermineTurnOrder()
    {
        // Faster card acts first; ties go to the player. Re-evaluated every round because speed can change.
        if (Player.TotalSpeed >= Enemy.TotalSpeed) { first = Player; second = Enemy; }
        else { first = Enemy; second = Player; }
    }

    void ExecuteTurn(CardRuntime attacker, CardRuntime defender)
    {
        if (!attacker.IsAlive || !defender.IsAlive) return;
        CurrentActor = attacker;

        var ts = Emit(BattleEventType.TurnStart, attacker, defender);
        ts.text = $"{attacker.DisplayName}'s turn";

        // Burn, Poison, Regeneration, countdowns, Stun
        bool canAct = StatusEffects.ProcessStatusEffects(attacker);
        if (!attacker.IsAlive) return;
        if (!canAct)
        {
            var skip = Emit(BattleEventType.TurnSkipped, attacker, defender);
            skip.text = $"<color=#FFE14D>{attacker.DisplayName} is stunned and skips the turn!</color>";
            attacker.TickBuffs();
            return;
        }

        RunTalent(attacker, () => attacker.talent.OnTurnStart(attacker, defender));
        if (!attacker.IsAlive || !defender.IsAlive) return;

        bool usedSkill = false;
        if (attacker.talent is ActiveTalent active && attacker.silenceTurns == 0)
        {
            int cost = active.GetManaCost(attacker);
            if (attacker.mana >= cost)
            {
                attacker.mana -= cost;
                var sk = Emit(BattleEventType.SkillActivated, attacker, defender);
                sk.text = $"<color=#6FD3FF>{attacker.DisplayName} uses <b>{active.DisplayName}</b>!</color>";
                RunTalent(attacker, () => active.Activate(attacker, defender));
                usedSkill = true;
            }
        }

        // The talent fires first, then the card still attacks (so buffs/debuffs are not a wasted turn).
        if (attacker.IsAlive && defender.IsAlive)
        {
            NormalAttack(attacker, defender);
        }

        GainTurnMana(attacker, usedSkill);
        attacker.TickBuffs();
    }

    void NormalAttack(CardRuntime attacker, CardRuntime defender)
    {
        RunTalent(attacker, () => attacker.talent.OnBeforeAttack(attacker, defender));
        if (!attacker.IsAlive || !defender.IsAlive) return;

        float elementMul = ElementSystem.GetElementMultiplier(attacker.Element, defender.Element);

        // Evasion check first
        if (BattleRandom.Chance(defender.evasion))
        {
            attacker.lastDamageDealt = 0;
            attacker.lastAttackWasCrit = false;
            var ev = Emit(BattleEventType.Evaded, attacker, defender);
            ev.elementMultiplier = elementMul;
            ev.text = $"<color=#C9D1DC>{defender.DisplayName} evaded the attack!</color>";
            defender.GainMana(5);
            return;
        }

        int damage = DamageCalculator.NormalAttack(attacker, defender, out bool isCrit);

        // Endurance: 65% less damage from normal attacks, 35% of the blocked damage is reflected as true damage
        int reflect = 0;
        if (defender.hasEndurance)
        {
            int original = damage;
            damage = Mathf.Max(1, Mathf.RoundToInt(original * 0.35f));
            reflect = Mathf.RoundToInt((original - damage) * 0.35f);
        }

        if (defender.talent != null)
        {
            try { defender.talent.OnTakeDamage(defender, ref damage); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        var atk = Emit(BattleEventType.NormalAttack, attacker, defender);
        atk.crit = isCrit;
        atk.elementMultiplier = elementMul;
        atk.text = $"{attacker.DisplayName} attacks{(isCrit ? " - <color=#FFB23D>CRITICAL!</color>" : "")}";

        int dealt = defender.TakeDamage(damage, isCrit ? DamageKind.Critical : DamageKind.Normal);
        attacker.lastDamageDealt = dealt;
        attacker.lastAttackWasCrit = isCrit;

        if (reflect > 0 && attacker.IsAlive)
        {
            BattleLog.Status(attacker, $"<color=#6FD3FF>[Endurance] {defender.DisplayName} reflects {reflect} damage!</color>");
            attacker.TakeTrueDamage(reflect, DamageKind.Reflect);
        }

        // Lifesteal (Blood Surge / Bloodthirster)
        if (attacker.lifesteal > 0 && dealt > 0 && attacker.IsAlive)
        {
            attacker.Heal(dealt * attacker.lifesteal / 100);
        }

        // Defender gains mana when hit
        defender.GainMana(5);

        RunTalent(attacker, () => attacker.talent.OnAfterAttack(attacker, defender));
    }

    void GainTurnMana(CardRuntime attacker, bool usedSkill)
    {
        // Mana per action: 10 after a skill, otherwise ceil(80 / round) + 20 (fills fast early, slower later).
        int manaGain = usedSkill ? 10 : Mathf.CeilToInt(80f / Mathf.Max(1, Round)) + 20;
        attacker.GainManaWithRegen(manaGain);
    }

    void CheckBattleEnd()
    {
        if (IsOver) return;
        bool playerAlive = Player.IsAlive;
        bool enemyAlive = Enemy.IsAlive;

        if (!playerAlive && !enemyAlive) Finish(BattleState.Draw, "Both fighters fell!");
        else if (!enemyAlive) Finish(BattleState.PlayerWon, $"{Enemy.DisplayName} was defeated!");
        else if (!playerAlive) Finish(BattleState.EnemyWon, $"{Player.DisplayName} was defeated!");
    }

    void Finish(BattleState state, string message)
    {
        State = state;
        var e = Emit(BattleEventType.BattleEnd, null, null);
        e.text = $"<b>{message}</b>";
    }

    void RunTalent(CardRuntime owner, Action call)
    {
        if (owner == null || owner.talent == null) return;
        var previous = CurrentActor;
        CurrentActor = owner;
        try { call(); }
        catch (Exception ex) { Debug.LogException(ex); }
        CurrentActor = previous;
    }

    /// <summary>Creates, snapshots and records an event. Used by BattleLog too.</summary>
    public BattleEvent Emit(BattleEventType type, CardRuntime actor, CardRuntime target)
    {
        var e = new BattleEvent
        {
            type = type,
            actor = actor,
            target = target,
            round = Round,
            playerHp = Player.health,
            playerMaxHp = Player.maxHealth,
            playerMana = Player.mana,
            playerMaxMana = Player.maxMana,
            enemyHp = Enemy.health,
            enemyMaxHp = Enemy.maxHealth,
            enemyMana = Enemy.mana,
            enemyMaxMana = Enemy.maxMana
        };
        Events.Add(e);
        return e;
    }
}

/// <summary>
/// Damage math. Formula from the community Anigame calculator
/// (github.com/julietperushia/calculator, anigame_simulator_update.c):
///   damage = (100 * baseATK * currentATK + 638000) / (8.7 * DEF * 10)  x element multiplier
/// HP is the HP stat x 10. Talent damage that is "X% of a stat" is also x10 so it lives on the HP scale.
/// </summary>
public static class DamageCalculator
{
    public const float AtkCoefficient = 100f;
    public const float FlatConstant = 638000f;
    public const float DefCoefficient = 87f; // 8.7 * 10

    public static int NormalAttack(CardRuntime attacker, CardRuntime defender, out bool isCrit)
    {
        float baseAtk = Mathf.Max(1, attacker.baseAttack);
        float currentAtk = attacker.TotalAttack;
        float def = defender.TotalDefense;

        float damage = (AtkCoefficient * baseAtk * currentAtk + FlatConstant) / (DefCoefficient * def);
        damage *= ElementSystem.GetElementMultiplier(attacker.Element, defender.Element);

        isCrit = BattleRandom.Chance(attacker.critRate);
        if (isCrit) damage *= Mathf.Max(100, attacker.critDamage) / 100f;

        return Mathf.Max(1, Mathf.RoundToInt(damage));
    }

    /// <summary>Talent damage equal to <paramref name="percent"/> of a stat, on the HP (x10) scale.</summary>
    public static int FromStat(int stat, float percent)
    {
        return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0, stat) * percent * 10f));
    }

    /// <summary>Applies the attacker-vs-defender element multiplier to talent damage.</summary>
    public static int WithElement(int damage, CardRuntime attacker, CardRuntime defender)
    {
        return Mathf.Max(1, Mathf.RoundToInt(damage * ElementSystem.GetElementMultiplier(attacker.Element, defender.Element)));
    }
}
