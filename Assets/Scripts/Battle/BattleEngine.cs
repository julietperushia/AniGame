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
        Player.opponent = Enemy;
        Enemy.opponent = Player;
        BattleRandom.Seed(seed);
        // Round limit and enrage follow the HP multiplier (longer fights when HP is higher)
        MaxRounds = GameRules.MaxRounds;
        EnrageStartRound = GameRules.EnrageStartRound;
        EnragePercent = GameRules.EnragePercent;
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
            start.elementMultiplier = DamageCalculator.ElementMul(Player, Enemy);

            // Strongest Hero buff: small chance the enemy starts at half HP
            if (runPlayerStart && Player.strongestHeroChance > 0f && BattleRandom.Value < Player.strongestHeroChance)
            {
                Enemy.health = Mathf.Max(1, Enemy.health - Enemy.maxHealth / 2);
                var sh = Emit(BattleEventType.Info, Player, Enemy);
                sh.text = $"<color=#FFD54F>STRONGEST HERO! {Enemy.DisplayName} loses 50% HP</color>";
            }

            if (runPlayerStart) BattleStartTalents(Player, Enemy);
            if (runEnemyStart) BattleStartTalents(Enemy, Player);
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

        if (Player.IsTeam || Enemy.IsTeam) return StepTeam();

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
                AnnounceHealingFatigue();
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

        // Burn, Poison, Regeneration, countdowns, Stun / Freeze / Sleep
        bool canAct = StatusEffects.ProcessStatusEffects(attacker);
        if (!attacker.IsAlive) return;
        if (!canAct)
        {
            var skip = Emit(BattleEventType.TurnSkipped, attacker, defender);
            skip.text = $"<color=#FFE14D>{attacker.DisplayName} is {attacker.skipReason ?? "stunned"} and skips the turn!</color>";
            attacker.TickBuffs();
            StatusEffects.EndOfTurn(attacker);
            return;
        }

        RunTalents(attacker, defender, t => t.OnTurnStart(attacker, defender), true);
        if (!attacker.IsAlive || !defender.IsAlive) return;

        bool usedSkill = false;
        var active = attacker.silenceTurns == 0 ? attacker.NextActive(defender) : null;
        if (active != null)
        {
            int cost = active.GetManaCost(attacker);
            bool can = false;
            try { can = active.CanActivate(attacker, defender); } catch (Exception ex) { Debug.LogException(ex); }
            if (can && attacker.mana >= cost)
            {
                attacker.mana -= cost;
                attacker.AdvanceActive();
                var sk = Emit(BattleEventType.SkillActivated, attacker, defender);
                sk.text = $"<color=#6FD3FF>{attacker.DisplayName} uses <b>{active.DisplayName}</b>!</color>";
                sk.skill = active;
                Guarded(attacker, active, () => active.Activate(attacker, defender));
                usedSkill = true;
                if (defender.IsAlive && attacker.IsAlive)
                    RunTalents(defender, attacker, t => t.OnEnemySkill(defender, attacker));
            }
        }

        // The talent fires first, then the card still attacks (so buffs/debuffs are not a wasted turn).
        if (attacker.skipAttackThisTurn)
        {
            attacker.skipAttackThisTurn = false;
            var info = Emit(BattleEventType.Info, attacker, defender);
            info.text = $"<color=#FFB300>{attacker.DisplayName} {attacker.skipAttackText ?? "charges up instead of attacking"}</color>";
            attacker.skipAttackText = null;
        }
        else if (attacker.IsAlive && defender.IsAlive)
        {
            NormalAttack(attacker, defender, AttackOptions.Normal);
            // Haste: a faster card may attack twice
            if (attacker.IsAlive && defender.IsAlive && attacker.hasteChance > 0f && attacker.ActingSpeed > defender.TotalSpeed
                && BattleRandom.Value < attacker.hasteChance)
            {
                var h = Emit(BattleEventType.Info, attacker, defender);
                h.tag = "haste";
                h.text = $"<color=#00E5FF>[Haste] {attacker.ActorName} attacks again!</color>";
                NormalAttack(attacker, defender, new AttackOptions { extra = true });
            }
        }

        GainTurnMana(attacker, usedSkill);
        attacker.TickBuffs();
        StatusEffects.EndOfTurn(attacker);
    }

    // =====================================================================================
    // 3v3 team battle
    // Every card has its own SPD: each round all 6 cards act once, fastest first (re-checked before every turn,
    // so SPD changes apply at once; ties go to the player team, then card order). HP / ATK / DEF / mana / evasion and
    // statuses belong to the team. Team statuses (DoTs, regen, countdowns, buffs) tick once per round per team.
    // Stun / Freeze / Sleep skip only the team's next card. Actives can't be used in round 1; each card uses only
    // its own actives and pays their mana cost from the shared pool.
    // =====================================================================================
    readonly Dictionary<TeamMember, int> actedThisRound = new Dictionary<TeamMember, int>();
    int Acted(TeamMember m) => actedThisRound.TryGetValue(m, out int n) ? n : 0;
    static int Actions(TeamMember m) => Mathf.Max(1, m.actionsPerRound);
    /// <summary>SPD of a card's k-th turn this round: a card with several turns (a boss) takes them at full, 2/3, 1/3... SPD.</summary>
    static int ActionSpeed(TeamMember m, CardRuntime side, int k) => Mathf.Max(1, Mathf.RoundToInt(m.Speed(side) * (Actions(m) - k) / (float)Actions(m)));
    readonly HashSet<CardRuntime> tickedThisRound = new HashSet<CardRuntime>();

    bool StepTeam()
    {
        BattleLog.Attach(this);
        try
        {
            var next = NextTeamActor(out var team);
            if (next == null || Round == 0)
            {
                Round++;
                if (Round > MaxRounds)
                {
                    Round = MaxRounds;
                    Finish(BattleState.Draw, "Time limit reached");
                    return false;
                }
                actedThisRound.Clear();
                tickedThisRound.Clear();
                var rs = Emit(BattleEventType.RoundStart, Player, Enemy);
                rs.text = $"<color=#9AA4B5>- Round {Round} -</color>";
                if (Round >= EnrageStartRound) ApplyEnrage();
                AnnounceHealingFatigue();
                next = NextTeamActor(out team);
                if (next == null) { Finish(BattleState.Draw, "No cards"); return false; }
            }
            actedThisRound[next] = Acted(next) + 1;
            var foe = team == Player ? Enemy : Player;
            ExecuteTeamTurn(team, next, foe);
            team.currentMember = null;
            foe.currentMember = null;
            CurrentActor = null;
            CheckBattleEnd();
        }
        finally
        {
            BattleLog.Detach(this);
        }
        return true;
    }

    /// <summary>The fastest card that hasn't acted this round (null when the round is over).</summary>
    TeamMember NextTeamActor(out CardRuntime team)
    {
        TeamMember best = null;
        team = null;
        int bestSpd = int.MinValue;
        foreach (var side in new[] { Player, Enemy })
        {
            if (!side.IsTeam) continue;
            foreach (var m in side.members)
            {
                if (Acted(m) >= Actions(m)) continue;
                int spd = ActionSpeed(m, side, Acted(m));
                if (spd > bestSpd) { best = m; team = side; bestSpd = spd; } // strict: ties keep the player team / lower index
            }
        }
        return best;
    }

    /// <summary>Order of the cards that still act this round (for the UI / logs).</summary>
    public List<TeamMember> TeamTurnOrder()
    {
        var all = new List<(TeamMember m, CardRuntime t, int spd)>();
        foreach (var side in new[] { Player, Enemy })
            if (side.IsTeam)
                foreach (var m in side.members)
                    for (int k = Acted(m); k < Actions(m); k++) all.Add((m, side, ActionSpeed(m, side, k)));
        all.Sort((a, b) =>
        {
            int c = b.spd.CompareTo(a.spd);
            if (c != 0) return c;
            c = (a.t == Player ? 0 : 1).CompareTo(b.t == Player ? 0 : 1);
            return c != 0 ? c : a.m.index.CompareTo(b.m.index);
        });
        var list = new List<TeamMember>();
        foreach (var x in all) list.Add(x.m);
        return list;
    }

    bool IsLastOfTeam(CardRuntime team)
    {
        foreach (var m in team.members) if (Acted(m) < Actions(m)) return false;
        return true;
    }

    void ExecuteTeamTurn(CardRuntime team, TeamMember m, CardRuntime foe)
    {
        if (!team.IsAlive || !foe.IsAlive) return;
        team.currentMember = m;
        CurrentActor = team;

        var ts = Emit(BattleEventType.TurnStart, team, foe);
        ts.text = $"{m.name} ({team.DisplayName}) - SPD {m.Speed(team)}";

        // Team statuses tick once per round, on the team's first card
        if (tickedThisRound.Add(team))
        {
            StatusEffects.TickStatuses(team);
            if (!team.IsAlive) return;
        }

        bool last = IsLastOfTeam(team);
        if (!StatusEffects.ConsumeSkip(team))
        {
            var skip = Emit(BattleEventType.TurnSkipped, team, foe);
            skip.text = $"<color=#FFE14D>{m.name} is {team.skipReason ?? "stunned"} and skips the turn!</color>";
            if (last) EndTeamRound(team);
            return;
        }

        RunTalents(team, foe, t => t.OnTurnStart(team, foe), true);
        if (!team.IsAlive || !foe.IsAlive) return;

        bool usedSkill = false;
        var active = Round >= 2 && team.silenceTurns == 0 ? team.NextActiveOf(m, foe) : null;
        if (active != null)
        {
            int cost = Mathf.Max(1, active.BaseCost);
            bool can = false;
            try { can = active.CanActivate(team, foe); } catch (Exception ex) { Debug.LogException(ex); }
            if (can && team.mana >= cost)
            {
                team.mana -= cost;
                m.nextActive++;
                var sk = Emit(BattleEventType.SkillActivated, team, foe);
                sk.text = $"<color=#6FD3FF>{m.name} uses <b>{active.DisplayName}</b>! ({cost} mana)</color>";
                sk.skill = active;
                Guarded(team, active, () => active.Activate(team, foe));
                team.currentMember = m; // a talent may have switched the acting card (Counter Stance...)
                usedSkill = true;
                if (foe.IsAlive && team.IsAlive)
                    RunTalents(foe, team, t => t.OnEnemySkill(foe, team));
            }
        }

        if (team.skipAttackThisTurn)
        {
            team.skipAttackThisTurn = false;
            var info = Emit(BattleEventType.Info, team, foe);
            info.text = $"<color=#FFB300>{m.name} {team.skipAttackText ?? "charges up instead of attacking"}</color>";
            team.skipAttackText = null;
        }
        else if (team.IsAlive && foe.IsAlive)
        {
            NormalAttack(team, foe, AttackOptions.Normal);
            team.currentMember = m;
            if (team.IsAlive && foe.IsAlive && team.hasteChance > 0f && m.Speed(team) > foe.TotalSpeed
                && BattleRandom.Value < team.hasteChance)
            {
                var h = Emit(BattleEventType.Info, team, foe);
                h.tag = "haste";
                h.text = $"<color=#00E5FF>[Haste] {m.name} attacks again!</color>";
                NormalAttack(team, foe, new AttackOptions { extra = true });
            }
        }

        GainTurnMana(team, usedSkill);
        if (last) EndTeamRound(team);
    }

    /// <summary>After the team's last card of the round: timed buffs and Blind / Hex counters tick once.</summary>
    void EndTeamRound(CardRuntime team)
    {
        team.TickBuffs();
        StatusEffects.EndOfTurn(team);
    }

    /// <summary>Options for one attack hit (normal attack, Haste's extra attack, Counter Stance, Flurry...).</summary>
    public class AttackOptions
    {
        public float multiplier = 1f;
        public bool counter;     // Counter Stance riposte: can't be countered, no mana for the attacker
        public bool extra;       // Haste's second attack
        public bool skillHit;    // hit from an active talent (Flurry, Assassinate): no blind/hypnotize/charge checks
        public bool forceCrit;
        public bool unevadable;
        public static AttackOptions Normal => new AttackOptions();
    }

    /// <summary>Counter Stance: hit back right away after an evade (weaker, no mana, can't be countered).</summary>
    void CounterStrike(CardRuntime defender, CardRuntime attacker)
    {
        if (defender == null || attacker == null || !defender.counterStance || !defender.IsAlive || !attacker.IsAlive) return;
        var prevMember = defender.currentMember;
        var prevAttacker = attacker.currentMember;
        if (defender.IsTeam) defender.currentMember = defender.members[BattleRandom.Range(0, defender.members.Count)];
        var c = Emit(BattleEventType.Info, defender, attacker);
        c.tag = "counter";
        c.text = $"<color=#FFB300>[Counter Stance] {defender.ActorName} strikes back!</color>";
        NormalAttack(defender, attacker, new AttackOptions { counter = true, multiplier = defender.counterDamage });
        defender.currentMember = prevMember;
        attacker.currentMember = prevAttacker;
    }

    /// <summary>
    /// Counter Stance: skill damage (Elemental Strike, Dexterity Drive, Life Sap...) can be evaded with the card's evasion
    /// chance. An evade gives +5 Mana, runs the evade talents and the counter. Returns true if the damage was evaded.
    /// </summary>
    public bool TryEvadeSkill(CardRuntime defender)
    {
        if (defender == null || !defender.IsAlive || !defender.counterStance || defender.sleepTurns > 0 || defender.frozen) return false;
        if (!BattleRandom.Chance(GameRules.EvadeChance(defender.evasion))) return false;
        var attacker = defender.opponent;
        var ev = Emit(BattleEventType.Evaded, attacker, defender);
        ev.elementMultiplier = attacker != null ? DamageCalculator.ElementMul(attacker, defender) : 1f;
        ev.text = $"<color=#C9D1DC>[Counter Stance] {defender.DisplayName} evades the skill!</color>";
        defender.GainMana(5);
        RunTalents(defender, attacker, t => t.OnEvade(defender, attacker));
        CounterStrike(defender, attacker);
        return true;
    }

    /// <summary>Every enemy card of a fighter (one today; team battles can return several).</summary>
    public List<CardRuntime> EnemiesOf(CardRuntime c) => new List<CardRuntime> { c == Player ? Enemy : Player };

    /// <summary>Lets active talents make attack hits that count as normal-attack damage.</summary>
    public void PerformAttack(CardRuntime attacker, CardRuntime defender, AttackOptions options)
    {
        if (attacker == null || defender == null || !attacker.IsAlive || !defender.IsAlive) return;
        NormalAttack(attacker, defender, options ?? new AttackOptions { skillHit = true });
    }

    /// <summary>Runs a talent that was just added in battle (Mimic, Gamble) as if the battle had started.</summary>
    public void InitTalent(CardRuntime owner, CardRuntime enemy, Talent t)
    {
        if (owner == null || t == null) return;
        Guarded(owner, t, () => t.OnBattleStart(owner, enemy));
        Guarded(owner, t, () => t.OnLearnedInBattle(owner, enemy)); // Nothingness punishes every copy
    }

    /// <summary>Runs a stat change with the stat rules (Blackhole, Chaotic...). Inside a talent call the rules
    /// already run at the end of that call, so the change is made directly.</summary>
    public void ApplyStatChange(CardRuntime owner, Talent t, Action change)
    {
        if (change == null) return;
        if (guardDepth > 0 || owner == null || t == null) { change(); return; }
        Guarded(owner, t, change);
    }

    /// <summary>The round's healing fatigue, announced at the start of every round after round 10.</summary>
    void AnnounceHealingFatigue()
    {
        if (!GameRules.HealingFatigueOn || Round <= GameRules.HealingFatigueAfterRound) return;
        int cut = Mathf.RoundToInt((1f - GameRules.HealingMultiplier(Round)) * 100f);
        var e = Emit(BattleEventType.Info, null, null);
        e.text = cut >= 100 ? "<color=#9CCC65>HEALING FATIGUE! Healing no longer works</color>" : $"<color=#9CCC65>HEALING FATIGUE! All healing received -{cut}%</color>";
    }

    void NormalAttack(CardRuntime attacker, CardRuntime defender, AttackOptions opt)
    {
        opt = opt ?? AttackOptions.Normal;
        RunTalents(attacker, defender, t => t.OnBeforeAttack(attacker, defender), true);
        if (!attacker.IsAlive || !defender.IsAlive) return;

        float elementMul = DamageCalculator.ElementMul(attacker, defender);
        bool plain = !opt.skillHit && !opt.counter;

        // Hypnotize: chance to hit itself instead
        if (plain && attacker.hypnoStacks > 0 && BattleRandom.Chance(attacker.hypnoChancePerStack * attacker.hypnoStacks))
        {
            int self = Mathf.Max(1, Mathf.RoundToInt(DamageCalculator.NormalAttack(attacker, attacker, false) * attacker.hypnoSelfDamage));
            attacker.hypnoStacks = 0;
            attacker.hypnoImmuneTurns = attacker.hypnoImmuneAfter;
            var hy = Emit(BattleEventType.Info, attacker, attacker);
            hy.tag = "hypno";
            hy.text = $"<color=#BA68C8>[Hypnotize] {attacker.DisplayName} attacks itself!</color>";
            attacker.TakeDamage(self, DamageKind.Normal);
            return;
        }

        // Charge Attack (passive): above the HP threshold the card stores its attacks instead of hitting
        int chargeBonus = 0;
        if (plain && attacker.chargeAttackPassive)
        {
            // Stores a limited number of attacks, then releases (so two charging cards can't stall the battle forever)
            if (attacker.healthPercent > attacker.chargeAttackThreshold && attacker.chargeCount < attacker.chargeAttackMax)
            {
                int would = DamageCalculator.NormalAttack(attacker, defender, false);
                attacker.chargeStored += would;
                attacker.chargeCount++;
                var ch = Emit(BattleEventType.Info, attacker, defender);
                ch.tag = "charge";
                ch.text = $"<color=#FFB300>[Charge Attack] {attacker.DisplayName} stores {would} damage ({attacker.chargeCount}/{attacker.chargeAttackMax}, total {attacker.chargeStored})</color>";
                return;
            }
            if (attacker.chargeStored > 0)
            {
                chargeBonus = Mathf.RoundToInt(attacker.chargeStored * attacker.chargeAttackRelease);
                attacker.chargeStored = 0;
                attacker.chargeCount = 0;
                var rel = Emit(BattleEventType.Info, attacker, defender);
                rel.tag = "release";
                rel.text = $"<color=#FFB300>[Charge Attack] {attacker.DisplayName} releases everything: +{chargeBonus} damage!</color>";
            }
        }

        // Blind: chance to miss
        if (plain && attacker.blindTurns > 0 && BattleRandom.Chance(attacker.blindMissChance))
        {
            Missed(attacker, defender, elementMul, $"<color=#B0BEC5>{attacker.DisplayName} is blinded and misses!</color>", true);
            return;
        }

        // Charge Strike: xN and (by default) can't be evaded
        float mult = opt.multiplier;
        bool unevadable = opt.unevadable;
        if (!opt.skillHit && !opt.counter && attacker.chargeStrikeReady)
        {
            attacker.chargeStrikeReady = false;
            mult *= attacker.chargeStrikeMultiplier;
            unevadable |= attacker.chargeStrikeUnevadable;
        }

        // Crit is rolled first so a Precision card's critical hits can skip the evasion check.
        bool isCrit = opt.forceCrit || BattleRandom.Chance(attacker.ActingCritRate);
        bool canEvade = !unevadable && !(isCrit && attacker.critIgnoresEvasion) && defender.sleepTurns <= 0 && !defender.frozen;

        // Evasion: the defender's rating goes through the diminishing-returns curve (max 75%)
        if (canEvade && BattleRandom.Chance(GameRules.EvadeChance(defender.evasion)))
        {
            Missed(attacker, defender, elementMul, $"<color=#C9D1DC>{defender.DisplayName} evaded the attack!</color>", false);
            defender.GainMana(5);
            RunTalents(defender, attacker, t => t.OnEvade(defender, attacker));
            if (!opt.counter) CounterStrike(defender, attacker);
            return;
        }

        int damage = Mathf.RoundToInt(DamageCalculator.NormalAttack(attacker, defender, isCrit) * mult) + chargeBonus;
        if (attacker.slayerPercent > 0f) // part of the attack, so 3v3 scales it like the rest of the hit
            damage += Mathf.RoundToInt(Mathf.Max(0, defender.health) * attacker.slayerPercent * DamageCalculator.TeamScale(attacker)); // current HP

        // Sleep: attacks don't wake the target, but hitting a sleeping card deals less damage
        bool hitSleeper = defender.sleepTurns > 0 && defender.sleepDamageCut > 0f;
        if (hitSleeper) damage = Mathf.Max(1, Mathf.RoundToInt(damage * (1f - Mathf.Clamp01(defender.sleepDamageCut))));

        // Endurance: less damage from normal attacks, part of the blocked damage is reflected as true damage
        // (Piercing Edge weakens the reduction)
        int reflect = 0;
        if (defender.hasEndurance)
        {
            int original = damage;
            float keep = 1f - Mathf.Clamp01(defender.enduranceReduction) * (1f - Mathf.Clamp01(attacker.reductionPierce));
            damage = Mathf.Max(1, Mathf.RoundToInt(original * keep));
            reflect = Mathf.RoundToInt((original - damage) * defender.enduranceReflect);
        }

        foreach (var dt in defender.talents.ToArray())
        {
            try { dt.OnTakeDamage(defender, ref damage); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        var atk = Emit(BattleEventType.NormalAttack, attacker, defender);
        atk.crit = isCrit;
        atk.elementMultiplier = elementMul;
        string effect = ElementSystem.EffectivenessTag(elementMul); // element advantage: super effective, not very effective...
        atk.text = $"{attacker.ActorName} attacks{(isCrit ? " - <color=#FFB23D>CRITICAL!</color>" : "")}" + (effect.Length > 0 ? " - " + effect : "") +
                   (hitSleeper ? $" <color=#9FA8DA>(asleep: -{defender.sleepDamageCut * 100f:0}%)</color>" : "");

        // Infernus: part of the damage is dealt as true damage, and half of it heals the attacker
        int trueDamage = attacker.infernusPercent > 0f ? Mathf.RoundToInt(damage * attacker.infernusPercent) : 0;
        float reductionScale = 1f - Mathf.Clamp01(attacker.reductionPierce);
        int dealt = defender.TakeDamage(Mathf.Max(1, damage - trueDamage), isCrit ? DamageKind.Critical : DamageKind.Normal, reductionScale);
        if (trueDamage > 0 && defender.IsAlive)
        {
            int td = defender.TakeTrueDamage(trueDamage, DamageKind.True);
            dealt += td;
            if (attacker.infernusHeal > 0f) attacker.Heal(Mathf.RoundToInt(td * attacker.infernusHeal));
        }
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
        if (opt.counter) { } // the riposte gives the attacker no mana (mana is only gained per turn anyway)

        if (defender.IsAlive)
            RunTalents(defender, attacker, t => t.OnHitTaken(defender, attacker, dealt, isCrit));
        RunTalents(attacker, defender, t => t.OnAfterAttack(attacker, defender), true);
        AfterAnyAttack(attacker, defender);
    }

    void Missed(CardRuntime attacker, CardRuntime defender, float elementMul, string text, bool miss)
    {
        attacker.lastDamageDealt = 0;
        attacker.lastAttackWasCrit = false;
        var ev = Emit(BattleEventType.Evaded, attacker, defender);
        ev.elementMultiplier = elementMul;
        ev.miss = miss;
        ev.text = text;
        RunTalents(attacker, defender, t => t.OnAttackMissed(attacker, defender), true);
        AfterAnyAttack(attacker, defender);
    }

    void AfterAnyAttack(CardRuntime attacker, CardRuntime defender)
    {
        foreach (var card in new[] { Player, Enemy })
        {
            if (!card.IsAlive || card.opponent == null) continue;
            var other = card.opponent;
            RunTalents(card, other, t => t.OnAnyAttack(card, other, attacker));
        }
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
        // Second Wind buff: small chance to survive a lethal blow with 1 HP (once per battle)
        if (!Player.IsAlive && !Player.secondWindUsed && Player.secondWindChance > 0f && BattleRandom.Value < Player.secondWindChance)
        {
            Player.secondWindUsed = true;
            Player.health = 1;
            var sw = Emit(BattleEventType.Info, Player, null);
            sw.text = $"<color=#7CFC6A>SECOND WIND! {Player.DisplayName} gets back up with 1 HP</color>";
        }
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

    /// <summary>Calls a hook on every talent of a card (a copy of the list, so talents added meanwhile wait).</summary>
    /// <summary>ownerOnly (3v3): only the talents of the card that is acting (its turn, its attack).</summary>
    void RunTalents(CardRuntime owner, CardRuntime enemy, Action<Talent> call, bool ownerOnly = false)
    {
        if (owner == null || owner.talents.Count == 0) return;
        foreach (var t in owner.talents.ToArray())
        {
            if (t == null) continue;
            if (ownerOnly && owner.IsTeam && owner.currentMember != null && owner.OwnerOf(t) != owner.currentMember) continue;
            Guarded(owner, t, () => call(t));
        }
    }

    /// <summary>Battle-start talents; in 3v3 each runs with its own card as the acting card (Gamble gives it its draws).</summary>
    void BattleStartTalents(CardRuntime team, CardRuntime enemy)
    {
        if (!team.IsTeam) { RunTalents(team, enemy, t => t.OnBattleStart(team, enemy)); return; }
        foreach (var t in team.talents.ToArray())
        {
            if (t == null) continue;
            team.currentMember = team.OwnerOf(t);
            Guarded(team, t, () => t.OnBattleStart(team, enemy));
        }
        team.currentMember = null;
    }

    struct StatSnap { public int atk, bonusAtk, def, bonusDef, spd, maxHp; }

    static StatSnap Snap(CardRuntime c) => new StatSnap
    { atk = c.attack, bonusAtk = c.bonusAttack, def = c.defense, bonusDef = c.bonusDefense, spd = c.speed, maxHp = c.maxHealth };

    /// <summary>
    /// Runs one talent call with that talent's strength (Gamble talents 60%), then applies the stat rules to
    /// whatever ATK/DEF/SPD/max HP changes the call made: Purify immunity and Gigantify resist block drops caused
    /// by the enemy, Chaotic flips every change, Blackhole swallows the enemy's gains and halves its owner's, Doppelganger copies 30% of gains.
    /// </summary>
    int guardDepth;

    /// <summary>Stats a Blackhole takes from what it swallowed (applied after both cards' rules).</summary>
    struct Swallowed { public CardRuntime to; public int atk, def, spd, hp; public float share; }

    void Guarded(CardRuntime owner, Talent t, Action call)
    {
        if (owner == null || t == null) return;
        var a = Snap(Player);
        var b = Snap(Enemy);
        float prevPower = owner.talentPower;
        owner.talentPower = owner.PowerOf(t);
        var previous = CurrentActor;
        CurrentActor = owner;
        guardDepth++;
        try { call(); }
        catch (Exception ex) { Debug.LogException(ex); }
        finally { guardDepth--; }
        CurrentActor = previous;
        owner.talentPower = prevPower;
        var swallowed = new List<Swallowed>();
        ApplyStatRules(Player, a, owner, swallowed);
        ApplyStatRules(Enemy, b, owner, swallowed);
        foreach (var s in swallowed)
        {
            if (s.to == null || !s.to.IsAlive || s.atk + s.def + s.spd + s.hp <= 0) continue;
            s.to.attack += s.atk;
            s.to.defense += s.def;
            s.to.speed += s.spd;
            s.to.maxHealth += s.hp;
            s.to.health += s.hp;
            var parts = new List<string>();
            if (s.atk > 0) parts.Add($"+{s.atk} ATK");
            if (s.def > 0) parts.Add($"+{s.def} DEF");
            if (s.spd > 0) parts.Add($"+{s.spd} SPD");
            if (s.hp > 0) parts.Add($"+{s.hp} max HP");
            BattleLog.Status(s.to, $"<color=#9E9E9E>[Blackhole] {s.to.DisplayName} absorbs {s.share * 100f:0}% of the swallowed stats: {string.Join(", ", parts)}</color>");
        }
    }

    void ApplyStatRules(CardRuntime card, StatSnap before, CardRuntime source, List<Swallowed> swallowed)
    {
        int dAtk = card.attack - before.atk, dBonusAtk = card.bonusAttack - before.bonusAtk;
        int dDef = card.defense - before.def, dBonusDef = card.bonusDefense - before.bonusDef;
        int dSpd = card.speed - before.spd, dHp = card.maxHealth - before.maxHp;
        if (dAtk == 0 && dBonusAtk == 0 && dDef == 0 && dBonusDef == 0 && dSpd == 0 && dHp == 0) return;

        // Debuff protection: stat drops caused by the enemy
        bool anyDrop = dAtk < 0 || dBonusAtk < 0 || dDef < 0 || dBonusDef < 0 || dSpd < 0 || dHp < 0;
        if (anyDrop && source != card && card.ResistsDebuff())
        {
            if (dAtk < 0) dAtk = 0; if (dBonusAtk < 0) dBonusAtk = 0; if (dDef < 0) dDef = 0;
            if (dBonusDef < 0) dBonusDef = 0; if (dSpd < 0) dSpd = 0; if (dHp < 0) dHp = 0;
        }

        bool chaotic = Player.HasTalent<Chaotic>() || Enemy.HasTalent<Chaotic>();
        if (chaotic) { dAtk = -dAtk; dBonusAtk = -dBonusAtk; dDef = -dDef; dBonusDef = -dBonusDef; dSpd = -dSpd; dHp = -dHp; }
        // Blackhole: the enemy's gains are swallowed (part of them goes to the Blackhole card), the owner keeps half of its own
        float kept = Blackhole.GainKept(card);
        bool anyGain = dAtk > 0 || dBonusAtk > 0 || dDef > 0 || dBonusDef > 0 || dSpd > 0 || dHp > 0;
        bool blackhole = kept < 1f && anyGain;
        if (blackhole)
        {
            var hole = Blackhole.EnemyHoleOf(card);
            if (hole != null && hole.swallowedToYou > 0f && card.opponent != null)
            {
                float share = (1f - Mathf.Clamp01(hole.enemyGainKept)) * Mathf.Clamp01(hole.swallowedToYou);
                int S(int d) => d > 0 ? Mathf.RoundToInt(d * share) : 0;
                swallowed.Add(new Swallowed { to = card.opponent, atk = S(dAtk) + S(dBonusAtk), def = S(dDef) + S(dBonusDef), spd = S(dSpd), hp = S(dHp), share = Mathf.Clamp01(hole.swallowedToYou) });
            }
            int K(int d) => d > 0 ? Mathf.RoundToInt(d * kept) : d;
            dAtk = K(dAtk); dBonusAtk = K(dBonusAtk); dDef = K(dDef); dBonusDef = K(dBonusDef); dSpd = K(dSpd); dHp = K(dHp);
        }

        int wasHealth = card.health;
        card.attack = before.atk + dAtk;
        card.bonusAttack = before.bonusAtk + dBonusAtk;
        card.defense = before.def + dDef;
        card.bonusDefense = before.bonusDef + dBonusDef;
        card.speed = before.spd + dSpd;
        card.maxHealth = Mathf.Max(1, before.maxHp + dHp);
        if (card.health > card.maxHealth) card.health = card.maxHealth;

        if (chaotic || blackhole)
        {
            var note = Emit(BattleEventType.Info, card, null);
            note.text = blackhole && !chaotic
                ? (kept <= 0f ? $"<color=#9E9E9E>[Blackhole] {card.DisplayName}'s stat gain is swallowed</color>" : $"<color=#9E9E9E>[Blackhole] {card.DisplayName} keeps {kept * 100f:0}% of the stat gain</color>")
                : "<color=#FF4081>[Chaotic] The stat change is reversed!</color>";
        }

        // Doppelganger: the opponent copies part of every gain
        var other = card.opponent;
        if (other != null && other.doppelgangerPercent > 0f && other.IsAlive)
        {
            int gAtk = Mathf.Max(0, dAtk) + Mathf.Max(0, dBonusAtk), gDef = Mathf.Max(0, dDef) + Mathf.Max(0, dBonusDef);
            int gSpd = Mathf.Max(0, dSpd), gHp = Mathf.Max(0, dHp);
            if (gAtk + gDef + gSpd + gHp > 0)
            {
                float cp = other.doppelgangerPercent;
                other.attack += Mathf.RoundToInt(gAtk * cp);
                other.defense += Mathf.RoundToInt(gDef * cp);
                other.speed += Mathf.RoundToInt(gSpd * cp);
                int hp = Mathf.RoundToInt(gHp * cp);
                other.maxHealth += hp;
                other.health += hp;
                BattleLog.Status(other, $"<color=#7986CB>[Doppelganger] {other.DisplayName} copies {cp * 100f:0}% of the gain</color>");
            }
        }
        _ = wasHealth;
    }

    /// <summary>Creates, snapshots and records an event. Used by BattleLog too.</summary>
    public BattleEvent Emit(BattleEventType type, CardRuntime actor, CardRuntime target)
    {
        var e = new BattleEvent
        {
            type = type,
            actor = actor,
            target = target,
            member = actor != null ? actor.currentMember : null,
            round = Round,
            playerHp = Player.health,
            playerMaxHp = Player.maxHealth,
            playerMana = Player.mana,
            playerMaxMana = Player.maxMana,
            enemyHp = Enemy.health,
            enemyMaxHp = Enemy.maxHealth,
            enemyMana = Enemy.mana,
            enemyMaxMana = Enemy.maxMana,
            playerShield = Player.shield,
            enemyShield = Enemy.shield
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

    /// <summary>
    /// 3v3 hit scale: a team adds up the ATK of its cards, so each hit is cut (x0.33 for 3 cards). A team of fewer cards
    /// (a boss fighting alone) adds up less, so its hits are cut less: x(scale x 3 / cards), at most 1.
    /// </summary>
    public static float TeamScale(CardRuntime attacker)
    {
        if (attacker == null || !attacker.IsTeam) return 1f;
        return Mathf.Min(1f, GameRules.TeamAttackScale * 3f / Mathf.Max(1, attacker.members.Count));
    }

    /// <summary>Normal attack damage that rolls its own crit.</summary>
    public static int NormalAttack(CardRuntime attacker, CardRuntime defender, out bool isCrit)
    {
        isCrit = BattleRandom.Chance(attacker.ActingCritRate);
        return NormalAttack(attacker, defender, isCrit);
    }

    /// <summary>Normal attack damage with the crit already decided.</summary>
    public static int NormalAttack(CardRuntime attacker, CardRuntime defender, bool isCrit)
    {
        float baseAtk = Mathf.Max(1, attacker.baseAttack);
        float currentAtk = attacker.TotalAttack * (1f + attacker.momentumPerStack * attacker.momentumStacks); // Momentum
        float def = Mathf.Max(1f, defender.TotalDefense * (1f - Mathf.Clamp01(attacker.defIgnore)));        // Piercing Edge

        float damage = (AtkCoefficient * baseAtk * currentAtk + FlatConstant) / (DefCoefficient * def);
        damage *= ElementMul(attacker, defender);
        damage *= TeamScale(attacker); // 3v3: totals of 3 cards on both sides

        if (isCrit) damage *= Mathf.Max(100, attacker.ActingCritDamage) / 100f;

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
        float mul = ElementMul(attacker, defender);
        string effect = ElementSystem.EffectivenessTag(mul);
        if (effect.Length > 0) BattleLog.Info($"{ElementSystem.DisplayName(attacker.Element)} vs {ElementSystem.DisplayName(defender.Element)}: {effect}");
        return Mathf.Max(1, Mathf.RoundToInt(damage * mul));
    }

    /// <summary>
    /// Element bonus of an attack. 1v1: attacker element vs defender element.
    /// 3v3: the attacking card's element vs EVERY card of the defending team, averaged
    /// (Water vs Fire, Fire, Lightning = (+25% +25% -25%) / 3 = +8.3%).
    /// </summary>
    public static float ElementMul(CardRuntime attacker, CardRuntime defender)
    {
        var atk = attacker.Element;
        if (defender.IsTeam)
        {
            float sum = 0f;
            foreach (var m in defender.members) sum += ElementSystem.GetElementMultiplier(atk, m.element);
            return sum / defender.members.Count;
        }
        return ElementSystem.GetElementMultiplier(atk, defender.element);
    }
}
