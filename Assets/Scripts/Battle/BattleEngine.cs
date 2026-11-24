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
            start.elementMultiplier = ElementSystem.GetElementMultiplier(Player.Element, Enemy.Element);

            // Strongest Hero buff: small chance the enemy starts at half HP
            if (runPlayerStart && Player.strongestHeroChance > 0f && BattleRandom.Value < Player.strongestHeroChance)
            {
                Enemy.health = Mathf.Max(1, Enemy.health - Enemy.maxHealth / 2);
                var sh = Emit(BattleEventType.Info, Player, Enemy);
                sh.text = $"<color=#FFD54F>STRONGEST HERO! {Enemy.DisplayName} loses 50% HP</color>";
            }

            if (runPlayerStart) RunTalents(Player, Enemy, t => t.OnBattleStart(Player, Enemy));
            if (runEnemyStart) RunTalents(Enemy, Player, t => t.OnBattleStart(Enemy, Player));
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

        RunTalents(attacker, defender, t => t.OnTurnStart(attacker, defender));
        if (!attacker.IsAlive || !defender.IsAlive) return;

        bool usedSkill = false;
        var active = attacker.NextActive();
        if (active != null && attacker.silenceTurns == 0)
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
            info.text = $"<color=#FFB300>{attacker.DisplayName} charges up instead of attacking</color>";
        }
        else if (attacker.IsAlive && defender.IsAlive)
        {
            NormalAttack(attacker, defender, AttackOptions.Normal);
            // Haste: a faster card may attack twice
            if (attacker.IsAlive && defender.IsAlive && attacker.hasteChance > 0f && attacker.TotalSpeed > defender.TotalSpeed
                && BattleRandom.Value < attacker.hasteChance)
            {
                var h = Emit(BattleEventType.Info, attacker, defender);
                h.text = $"<color=#00E5FF>[Haste] {attacker.DisplayName} attacks again!</color>";
                NormalAttack(attacker, defender, new AttackOptions { extra = true });
            }
        }

        GainTurnMana(attacker, usedSkill);
        attacker.TickBuffs();
        StatusEffects.EndOfTurn(attacker);
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
    }

    void NormalAttack(CardRuntime attacker, CardRuntime defender, AttackOptions opt)
    {
        opt = opt ?? AttackOptions.Normal;
        RunTalents(attacker, defender, t => t.OnBeforeAttack(attacker, defender));
        if (!attacker.IsAlive || !defender.IsAlive) return;

        float elementMul = ElementSystem.GetElementMultiplier(attacker.Element, defender.Element);
        bool plain = !opt.skillHit && !opt.counter;

        // Hypnotize: chance to hit itself instead
        if (plain && attacker.hypnoStacks > 0 && BattleRandom.Chance(20 * attacker.hypnoStacks))
        {
            int self = Mathf.Max(1, DamageCalculator.NormalAttack(attacker, attacker, false) / 2);
            attacker.hypnoStacks = 0;
            attacker.hypnoImmuneTurns = 2;
            var hy = Emit(BattleEventType.Info, attacker, attacker);
            hy.text = $"<color=#BA68C8>[Hypnotize] {attacker.DisplayName} attacks itself!</color>";
            attacker.TakeDamage(self, DamageKind.Normal);
            return;
        }

        // Charge Attack (passive): above 30% HP the card stores its attacks instead of hitting
        int chargeBonus = 0;
        if (plain && attacker.chargeAttackPassive)
        {
            if (attacker.healthPercent > 0.3f)
            {
                int would = DamageCalculator.NormalAttack(attacker, defender, false);
                attacker.chargeStored += would;
                var ch = Emit(BattleEventType.Info, attacker, defender);
                ch.text = $"<color=#FFB300>[Charge Attack] {attacker.DisplayName} stores {would} damage (total {attacker.chargeStored})</color>";
                return;
            }
            if (attacker.chargeStored > 0)
            {
                chargeBonus = Mathf.RoundToInt(attacker.chargeStored * 1.3f);
                attacker.chargeStored = 0;
                var rel = Emit(BattleEventType.Info, attacker, defender);
                rel.text = $"<color=#FFB300>[Charge Attack] {attacker.DisplayName} releases everything: +{chargeBonus} damage!</color>";
            }
        }

        // Blind: 50% miss
        if (plain && attacker.blindTurns > 0 && BattleRandom.Chance(50))
        {
            Missed(attacker, defender, elementMul, $"<color=#B0BEC5>{attacker.DisplayName} is blinded and misses!</color>", true);
            return;
        }

        // Charge Strike: x3 and can't be evaded
        float mult = opt.multiplier;
        bool unevadable = opt.unevadable;
        if (!opt.skillHit && !opt.counter && attacker.chargeStrikeReady)
        {
            attacker.chargeStrikeReady = false;
            mult *= 3f;
            unevadable = true;
        }

        // Crit is rolled first so a Precision card's critical hits can skip the evasion check.
        bool isCrit = opt.forceCrit || BattleRandom.Chance(attacker.critRate);
        bool canEvade = !unevadable && !(isCrit && attacker.critIgnoresEvasion) && defender.sleepTurns <= 0 && !defender.frozen;

        // Evasion: the defender's rating goes through the diminishing-returns curve (max 75%)
        if (canEvade && BattleRandom.Chance(GameRules.EvadeChance(defender.evasion)))
        {
            Missed(attacker, defender, elementMul, $"<color=#C9D1DC>{defender.DisplayName} evaded the attack!</color>", false);
            defender.GainMana(5);
            RunTalents(defender, attacker, t => t.OnEvade(defender, attacker));
            // Counter Stance: hit back right away (60% weaker, no mana)
            if (!opt.counter && defender.counterStance && defender.IsAlive && attacker.IsAlive)
            {
                var c = Emit(BattleEventType.Info, defender, attacker);
                c.text = $"<color=#FFB300>[Counter Stance] {defender.DisplayName} strikes back!</color>";
                NormalAttack(defender, attacker, new AttackOptions { counter = true, multiplier = defender.counterDamage });
            }
            return;
        }

        int damage = Mathf.RoundToInt(DamageCalculator.NormalAttack(attacker, defender, isCrit) * mult) + chargeBonus;
        if (attacker.slayerPercent > 0f) damage += Mathf.RoundToInt(defender.maxHealth * attacker.slayerPercent);

        // Sleep: the hit wakes the target up and deals +40%
        if (defender.sleepTurns > 0)
        {
            damage = Mathf.RoundToInt(damage * 1.4f);
            defender.sleepTurns = 0;
            BattleLog.Status(defender, $"<color=#9FA8DA>{defender.DisplayName} wakes up! (+40% damage)</color>");
        }

        // Endurance: 65% less damage from normal attacks, 35% of the blocked damage is reflected as true damage
        // (Piercing Edge halves the reduction)
        int reflect = 0;
        if (defender.hasEndurance)
        {
            int original = damage;
            float keep = attacker.reductionPierce ? 0.675f : 0.35f;
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
        atk.text = $"{attacker.DisplayName} attacks{(isCrit ? " - <color=#FFB23D>CRITICAL!</color>" : "")}";

        // Infernus: part of the damage is dealt as true damage, and half of it heals the attacker
        int trueDamage = attacker.infernusPercent > 0f ? Mathf.RoundToInt(damage * attacker.infernusPercent) : 0;
        float reductionScale = attacker.reductionPierce ? 0.5f : 1f;
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
        RunTalents(attacker, defender, t => t.OnAfterAttack(attacker, defender));
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
        RunTalents(attacker, defender, t => t.OnAttackMissed(attacker, defender));
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
    void RunTalents(CardRuntime owner, CardRuntime enemy, Action<Talent> call)
    {
        if (owner == null || owner.talents.Count == 0) return;
        foreach (var t in owner.talents.ToArray())
        {
            if (!owner.IsAlive && owner.health <= 0 && t == null) continue;
            Guarded(owner, t, () => call(t));
        }
    }

    struct StatSnap { public int atk, bonusAtk, def, bonusDef, spd, maxHp; }

    static StatSnap Snap(CardRuntime c) => new StatSnap
    { atk = c.attack, bonusAtk = c.bonusAttack, def = c.defense, bonusDef = c.bonusDefense, spd = c.speed, maxHp = c.maxHealth };

    /// <summary>
    /// Runs one talent call with that talent's strength (Gamble talents 60%), then applies the stat rules to
    /// whatever ATK/DEF/SPD/max HP changes the call made: Purify immunity and Gigantify resist block drops caused
    /// by the enemy, Chaotic flips every change, Blackhole cancels every gain, Doppelganger copies 30% of gains.
    /// </summary>
    void Guarded(CardRuntime owner, Talent t, Action call)
    {
        if (owner == null || t == null) return;
        var a = Snap(Player);
        var b = Snap(Enemy);
        float prevPower = owner.talentPower;
        owner.talentPower = GameRules.TalentPower(owner.Tier) * owner.ScaleOf(t);
        var previous = CurrentActor;
        CurrentActor = owner;
        try { call(); }
        catch (Exception ex) { Debug.LogException(ex); }
        CurrentActor = previous;
        owner.talentPower = prevPower;
        ApplyStatRules(Player, a, owner);
        ApplyStatRules(Enemy, b, owner);
    }

    void ApplyStatRules(CardRuntime card, StatSnap before, CardRuntime source)
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
        bool blackhole = Player.HasTalent<Blackhole>() || Enemy.HasTalent<Blackhole>();
        if (chaotic) { dAtk = -dAtk; dBonusAtk = -dBonusAtk; dDef = -dDef; dBonusDef = -dBonusDef; dSpd = -dSpd; dHp = -dHp; }
        if (blackhole)
        {
            if (dAtk > 0) dAtk = 0; if (dBonusAtk > 0) dBonusAtk = 0; if (dDef > 0) dDef = 0;
            if (dBonusDef > 0) dBonusDef = 0; if (dSpd > 0) dSpd = 0; if (dHp > 0) dHp = 0;
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
            note.text = blackhole && !chaotic ? "<color=#9E9E9E>[Blackhole] Stat gains are swallowed</color>" : "<color=#FF4081>[Chaotic] The stat change is reversed!</color>";
        }

        // Doppelganger: the opponent copies 30% of every gain
        var other = card.opponent;
        if (other != null && other.doppelganger && other.IsAlive)
        {
            int gAtk = Mathf.Max(0, dAtk) + Mathf.Max(0, dBonusAtk), gDef = Mathf.Max(0, dDef) + Mathf.Max(0, dBonusDef);
            int gSpd = Mathf.Max(0, dSpd), gHp = Mathf.Max(0, dHp);
            if (gAtk + gDef + gSpd + gHp > 0)
            {
                other.attack += Mathf.RoundToInt(gAtk * 0.3f);
                other.defense += Mathf.RoundToInt(gDef * 0.3f);
                other.speed += Mathf.RoundToInt(gSpd * 0.3f);
                int hp = Mathf.RoundToInt(gHp * 0.3f);
                other.maxHealth += hp;
                other.health += hp;
                BattleLog.Status(other, $"<color=#7986CB>[Doppelganger] {other.DisplayName} copies 30% of the gain</color>");
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

    /// <summary>Normal attack damage that rolls its own crit.</summary>
    public static int NormalAttack(CardRuntime attacker, CardRuntime defender, out bool isCrit)
    {
        isCrit = BattleRandom.Chance(attacker.critRate);
        return NormalAttack(attacker, defender, isCrit);
    }

    /// <summary>Normal attack damage with the crit already decided.</summary>
    public static int NormalAttack(CardRuntime attacker, CardRuntime defender, bool isCrit)
    {
        float baseAtk = Mathf.Max(1, attacker.baseAttack);
        float currentAtk = attacker.TotalAttack * (1f + attacker.momentumPerStack * attacker.momentumStacks); // Momentum
        float def = Mathf.Max(1f, defender.TotalDefense * (1f - Mathf.Clamp01(attacker.defIgnore)));        // Piercing Edge

        float damage = (AtkCoefficient * baseAtk * currentAtk + FlatConstant) / (DefCoefficient * def);
        damage *= ElementSystem.GetElementMultiplier(attacker.Element, defender.Element);

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
