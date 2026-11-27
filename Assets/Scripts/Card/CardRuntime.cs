using UnityEngine;
using System.Collections.Generic;

/// <summary>What kind of damage was dealt (drives popup colours and log text).</summary>
public enum DamageKind
{
    Normal,
    Critical,
    Skill,
    True,
    Burn,
    Poison,
    Reflect,
    Recoil
}

/// <summary>
/// In-battle state of one card. Created from a <see cref="Card"/> asset plus the owner's level/evolution.
/// All HP changes go through TakeDamage / TakeTrueDamage / Heal so the battle log (and the UI) see them.
/// </summary>
public class CardRuntime
{
    public Card baseCard;          // character art / name (may be null for a missing key)
    /// <summary>Main talent (the first of <see cref="talents"/>).</summary>
    public Talent talent;
    /// <summary>Every talent this card has in battle: God cards have an active + a passive; Gamble and Mimic add more.</summary>
    public readonly List<Talent> talents = new List<Talent>();
    readonly Dictionary<Talent, float> talentScale = new Dictionary<Talent, float>();
    int nextActive;
    /// <summary>The other fighter (set by the battle engine).</summary>
    public CardRuntime opponent;

    // ---- 3v3 teams: this runtime is the team's shared pool (HP, ATK, DEF, mana, evasion, statuses)
    /// <summary>The cards of this team (null in 1v1).</summary>
    public List<TeamMember> members;
    /// <summary>The card acting right now (3v3).</summary>
    public TeamMember currentMember;
    public string nameOverride;
    public int baseCrit = 5, baseCritDamage = 135;
    readonly Dictionary<Talent, TeamMember> talentOwner = new Dictionary<Talent, TeamMember>();
    public bool IsTeam => members != null && members.Count > 0;
    /// <summary>3v3: the team's starting mana pool (100 per card, more for 200-mana actives).</summary>
    public int basePool = 100;

    public CardRarity rarity;
    public CardElement element;
    /// <summary>Talent strength multiplier from rarity (1.0 at Common ... 1.2 at God).</summary>
    public float talentPower = 1f;
    public CardInstance instance;  // set for the player's card
    public BuildData build;        // set for the player's card
    public bool isPlayer;

    // Strength of status effects this card currently suffers / owns (set by the talent that applied them)
    public float burnPercent = 0.05f;
    public float poisonPercent = 0.05f;
    public float regenPercent = 0.075f;
    public float enduranceReflect = 0.35f;

    public int baseMaxHealth;
    public int maxHealth;
    public int health;

    public int baseAttack;
    public int baseDefense;
    public int baseSpeed;

    public int attack;
    public int defense;
    public int speed;

    public int mana;
    public int maxMana = 100;
    /// <summary>Mana regeneration in percent. 100 = normal gain, 140 = +40% (Celestial Influence).</summary>
    public int manaRegen = 100;

    // Status Effects
    public int burnStacks;
    public int poisonStacks;
    public int poisonTurns; // Tracks how many turns poison has been active (for miss chance)
    public bool isStunned;
    public int silenceTurns;
    public int regenerationStacks;
    public bool hasEndurance;
    public int enduranceTurns;

    // Combat modifiers
    public int lifesteal;   // percent of normal-attack damage healed back
    public int critRate;
    public int critDamage;
    public int evasion;     // evasion RATING (no limit); real chance = GameRules.EvadeChance(evasion), max 75%
    public bool critIgnoresEvasion; // Precision: critical hits can't be evaded

    // ---- New statuses (talent pack 2)
    public int shield;               // Burst Barrier: absorbs damage first
    public int shieldTurns;
    public int shieldBurst;          // true damage dealt to the opponent when the shield breaks
    public int debuffImmuneTurns;    // Purify
    public float debuffResistChance; // Gigantify: chance to shrug off each debuff
    public bool frozen;              // Frostbite: skips the next turn
    public int sleepTurns;           // Sleep: skips turns until hit
    public int blindTurns;           // Blind: chance to miss each normal attack
    public int blindMissChance = 50;
    public int hexTurns;             // Hex: ATK and DEF swapped
    public int hypnoStacks;          // Hypnotize: chance per stack to hit itself
    public int hypnoImmuneTurns;
    public int hypnoChancePerStack = 20;
    public float hypnoSelfDamage = 0.5f;
    public int hypnoImmuneAfter = 2;
    public float sleepDamageCut = 0.3f;   // Sleep: normal attacks on this sleeping card deal this much less (0.3 = -30%)
    public float sleepCasterRegen = 0.5f; // Sleep: the opponent's Mana Regeneration multiplier while this card sleeps
    public int poisonFlat = 5;           // Poison: flat damage per tick
    public int poisonMissBase = 15;      // Poison: chance (%) a tick misses with one stack; a miss removes every stack
    public int poisonMissPerStack = 5;   // Poison: miss chance added per stack after the first
    public int burnMissBase = 15;        // Burn: same rule as Poison
    public int burnMissPerStack = 5;
    public float shieldManaRegen = 1f;   // Burst Barrier: Mana Regeneration multiplier while the barrier is up (0.5 = half)
    public float enduranceReduction = 0.65f; // Endurance: normal attack damage blocked
    public float silenceManaMultiplier = 1f; // Silence talent: mana gain multiplier while silenced
    public float healTakenMultiplier = 1f; // Rot: healing and lifesteal received
    public CardRuntime rotSource;          // Rot: the card whose Rot weakens this card's healing (gains ATK from it)
    public Talent rotTalent;
    public float rotAtkShare;              // Rot: ATK the Rot card gains per point of healing it stopped (0.05 = 5%)
    public int manaBlockTurns;             // Silence (passive): no Mana Regeneration while > 0 (counts down at the end of this card's turns)
    public float greedyPercent;      // Greedy: share of the opponent's healing that heals this card instead
    public float doppelgangerPercent;// Doppelganger: share of the opponent's stat gains and heals copied
    public bool gutsReady;           // Guts: survive one lethal hit
    public int gutsHp = 1;
    public int gutsAtkBonus = 30;
    public int gutsTurns = 3;
    public float damageStoreRate;    // Death's Dance: part of the damage taken is stored instead
    public int storedDamage;
    public float defIgnore;          // Piercing Edge: part of the enemy DEF ignored
    public float reductionPierce;    // Piercing Edge: share of the enemy's damage reduction ignored (0.5 = half)
    public float slayerPercent;      // Slayer: + enemy CURRENT HP per attack
    public float infernusPercent;    // Infernus: part of attack damage dealt as true damage
    public float infernusHeal;
    public int momentumStacks;       // Momentum
    public float momentumPerStack;
    public int momentumMax = 5;
    public float hasteChance;        // Haste: extra attack when faster
    public bool counterStance;       // Counter Stance: hit back after evading
    public float counterDamage = 0.4f;
    public bool chargeStrikeReady;   // Charge Strike: next attack xN, can't be evaded
    public float chargeStrikeMultiplier = 3f;
    public bool chargeStrikeUnevadable = true;
    public bool skipAttackThisTurn;
    public string skipAttackText;    // why the attack is skipped (null = Charge Strike's "charges up")
    public int regenMissBase = 10;   // Regeneration: chance (%) a tick misses with one stack; a miss removes 1 stack
    public int regenMissPerStack = 10;
    public bool chargeAttackPassive; // Charge Attack: store attacks while above a HP threshold
    public float chargeAttackThreshold = 0.3f;
    public int chargeAttackMax = 4;
    public float chargeAttackRelease = 1.3f;
    public int chargeStored;
    public int chargeCount;
    public string skipReason;

    // Player buffs (set by PlayerProfile.ApplyCombatBuffs)
    public float secondWindChance;   // revive with 1 HP once
    public bool secondWindUsed;
    public float strongestHeroChance; // enemy starts at 50% HP

    public float healingBonus = 0f;
    public float healingReduction = 0f; // From Burn effect
    public int damageReduction = 0;          // flat damage removed from every non-true hit
    public float damageTakenReduction = 0f;  // percent (0.05 = 5%) removed from every non-true hit

    public int lastDamageDealt;
    public bool lastAttackWasCrit;

    // Buff system
    public int bonusAttack;
    public int bonusDefense;
    public int buffTurnsLeft;

    // Talent state storage (per-instance, not shared)
    private readonly Dictionary<string, object> talentState = new Dictionary<string, object>();

    // Computed properties
    public float healthPercent => maxHealth > 0 ? (float)health / maxHealth : 0f;
    public int TotalAttack => Mathf.Max(1, attack + bonusAttack);
    public int TotalDefense => Mathf.Max(1, defense + bonusDefense);
    public int TotalSpeed => Mathf.Max(1, speed);
    public bool IsAlive => health > 0;
    public string DisplayName => !string.IsNullOrEmpty(nameOverride) ? nameOverride : baseCard != null ? baseCard.DisplayName : "???";
    /// <summary>Attacking element: the acting card's element in 3v3.</summary>
    public CardElement Element => currentMember != null ? currentMember.element : element;
    /// <summary>Crit of the attack being made: in 3v3 the acting card's own crit plus the team's crit buffs.</summary>
    public int ActingCritRate => currentMember != null ? Mathf.Clamp(currentMember.critRate + (critRate - baseCrit), 0, 100) : critRate;
    public int ActingCritDamage => currentMember != null ? currentMember.critDamage + (critDamage - baseCritDamage) : critDamage;
    /// <summary>SPD of the acting card (3v3) or of this fighter.</summary>
    public int ActingSpeed => currentMember != null ? currentMember.Speed(this) : TotalSpeed;
    /// <summary>Name for the log: "Card (Team)" in 3v3.</summary>
    public string ActorName => currentMember != null ? currentMember.name : DisplayName;
    public int Tier => GameRules.Tier(rarity);

    CardRuntime() { }

    /// <summary>Floor enemy: the sheet's stats as-is (already scaled by POWER), its own element and talent.</summary>
    public CardRuntime(Card card)
    {
        baseCard = card;
        talent = card.talent;
        AddTalent(card.talent);
        AddTalent(card.talent2);
        rarity = card.rarity;
        element = card.element;
        talentPower = GameRules.TalentPower(Tier);
        float s = GameRules.EnemyStatScale;
        // card.health is stored x10 (sheet HP x 10): rescale it to the current HP multiplier
        SetBaseStats(Mathf.RoundToInt(card.health / 10f * GameRules.HpMultiplier * s), Mathf.RoundToInt(card.attack * s),
                     Mathf.RoundToInt(card.defense * s), Mathf.RoundToInt(card.speed * s));
        mana = Mathf.Max(0, card.mana);
        maxMana = card.maxMana > 0 ? card.maxMana : 100;
        critRate = card.critRate;
        critDamage = card.critDamage > 0 ? card.critDamage : 135;
        evasion = card.evasion;
    }

    /// <summary>Player card from a build: (Base 100 + Roll) x rarity multiplier, build element and talent.</summary>
    public static CardRuntime ForBuild(CardInstance card, BuildData build)
    {
        var db = CardDatabase.Instance;
        var rt = new CardRuntime
        {
            instance = card,
            build = build,
            baseCard = PlayerProfile.Art(card, build), // the player's own character (or the picture given to this copy)
            rarity = card.rarity,
            element = build.element,
            talent = db != null ? db.FindTalent(build.talent) : null,
            nameOverride = string.IsNullOrWhiteSpace(build.name) ? null : build.name, // the name the player gave this copy
        };
        rt.talentPower = GameRules.TalentPower(rt.Tier);
        rt.AddTalent(rt.talent);
        if (rt.Tier >= GameRules.MaxTier && db != null) rt.AddTalent(db.FindTalent(build.talent2));
        rt.SetBaseStats(CardStats.HpOf(card.rarity, build, card.godBonus), CardStats.AtkOf(card.rarity, build, card.godBonus),
                        CardStats.DefOf(card.rarity, build, card.godBonus), CardStats.SpdOf(card.rarity, build, card.godBonus));
        rt.mana = 0;
        rt.maxMana = 100;
        rt.critRate = 5;
        rt.critDamage = 135;
        rt.evasion = 5;
        return rt;
    }

    /// <summary>
    /// A 3v3 team: HP, ATK and DEF are the totals of the 3 cards; evasion is shared (average rating);
    /// SPD stays per card (the team's SPD is the average, used as the reference for SPD changes).
    /// The mana pool holds 100 per card (more for cards whose active costs more). All talents are global.
    /// </summary>
    public static CardRuntime ForTeam(string name, List<TeamMember> cards)
    {
        var rt = new CardRuntime { nameOverride = name, members = cards };
        int hp = 0, atk = 0, def = 0, spd = 0, eva = 0, pool = 0;
        var best = CardRarity.Common;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            c.index = i;
            hp += c.hp; atk += c.atk; def += c.def; spd += c.baseSpeed; eva += c.evasion;
            pool += c.PoolShare;
            if (c.rarity > best) best = c.rarity;
        }
        int n = Mathf.Max(1, cards.Count);
        rt.rarity = best;
        rt.element = cards.Count > 0 ? cards[0].element : CardElement.Neutral;
        rt.talentPower = GameRules.TalentPower(rt.Tier);
        rt.SetBaseStats(hp, atk, def, Mathf.RoundToInt(spd / (float)n));
        rt.mana = 0;
        rt.maxMana = Mathf.Max(100, pool);
        rt.basePool = rt.maxMana;
        rt.critRate = rt.baseCrit;
        rt.critDamage = rt.baseCritDamage;
        rt.evasion = Mathf.RoundToInt(eva / (float)n);
        foreach (var c in cards)
            for (int i = 0; i < c.talents.Count; i++)
            {
                var t = c.talents[i];
                if (t == null) continue;
                if (rt.talentOwner.ContainsKey(t))
                {
                    // A passive another card already has is global anyway: it applies once.
                    // Two cards with the same active each keep their own copy.
                    if (!(t is ActiveTalent)) continue;
                    t = DuplicateOf(t, rt);
                    c.talents[i] = t;
                }
                rt.talentOwner[t] = c;
                rt.AddTalent(t);
            }
        return rt;
    }

    static readonly Dictionary<Talent, List<Talent>> duplicates = new Dictionary<Talent, List<Talent>>();

    /// <summary>A reusable copy of a shared talent asset (talent state is kept per CardRuntime, so copies are safe to reuse).</summary>
    static Talent DuplicateOf(Talent t, CardRuntime team)
    {
        if (!duplicates.TryGetValue(t, out var list)) duplicates[t] = list = new List<Talent>();
        foreach (var d in list) if (d != null && !team.talentOwner.ContainsKey(d)) return d;
        var copy = UnityEngine.Object.Instantiate(t);
        copy.name = t.name;
        copy.hideFlags = HideFlags.DontSave;
        list.Add(copy);
        return copy;
    }

    /// <summary>The card that owns a talent (3v3), or null.</summary>
    public TeamMember OwnerOf(Talent t) => t != null && talentOwner.TryGetValue(t, out var m) ? m : null;

    /// <summary>Strength of one talent: its owner's rarity (3v3) or this card's, times its scale (Gamble 60%).</summary>
    public float PowerOf(Talent t)
    {
        var owner = OwnerOf(t);
        return GameRules.TalentPower(owner != null ? owner.Tier : Tier) * ScaleOf(t);
    }

    /// <summary>The next active talent of a card (3v3: only that card's actives; cards with several take turns).</summary>
    public ActiveTalent NextActiveOf(TeamMember m, CardRuntime enemy = null)
    {
        if (m == null) return NextActive(enemy);
        var list = new List<ActiveTalent>();
        foreach (var t in talents) if (t is ActiveTalent a && OwnerOf(t) == m) list.Add(a);
        if (list.Count == 0) return null;
        if (enemy != null && list.Count > 1) m.nextActive += PickUsable(list, m.nextActive, enemy);
        return list[m.nextActive % list.Count];
    }

    void SetBaseStats(int hp, int atk, int def, int spd)
    {
        baseMaxHealth = Mathf.Max(1, hp);
        maxHealth = baseMaxHealth;
        health = maxHealth;
        baseAttack = Mathf.Max(1, atk);
        baseDefense = Mathf.Max(1, def);
        baseSpeed = Mathf.Max(1, spd);
        attack = baseAttack;
        defense = baseDefense;
        speed = baseSpeed;
        manaRegen = 100;
    }

    /// <summary>
    /// Heals the card by the specified amount (affected by healing bonus / Burn reduction), capped at max health.
    /// </summary>
    public void Heal(int amount) => Heal(amount, false);

    /// <summary>redirected = this heal was sent here by Greedy / Doppelganger (no further redirects).</summary>
    public void Heal(int amount, bool redirected)
    {
        if (!IsAlive || amount <= 0) return;
        // Greedy: the opponent takes (part of) this healing instead
        if (!redirected && opponent != null && opponent.greedyPercent > 0f && opponent.IsAlive)
        {
            int stolen = Mathf.Clamp(Mathf.RoundToInt(amount * opponent.greedyPercent), 0, amount);
            if (stolen > 0)
            {
                BattleLog.Status(opponent, $"<color=#FFD54F>[Greedy] {opponent.DisplayName} steals the healing!</color>");
                opponent.Heal(stolen, true);
                amount -= stolen;
                if (amount <= 0) return;
            }
        }
        var engine = BattleLog.Current;
        float fatigue = GameRules.HealingMultiplier(engine != null ? engine.Round : 0); // healing fatigue after round 10
        float withoutRot = (1f + healingBonus) * (1f - healingReduction) * fatigue;
        float multiplier = withoutRot * Mathf.Max(0f, healTakenMultiplier);
        int actualHeal = Mathf.Max(0, Mathf.RoundToInt(amount * multiplier)); // Can't heal if reduction >= 100%
        int before = health;
        health = Mathf.Min(maxHealth, health + actualHeal);
        BattleLog.Healed(this, health - before);
        // Rot: part of the healing it stopped becomes ATK for the Rot card (only healing that could have landed counts)
        if (rotSource != null && rotAtkShare > 0f && healTakenMultiplier < 1f && engine != null && rotSource.IsAlive)
        {
            int would = Mathf.Min(Mathf.Max(0, maxHealth - before), Mathf.Max(0, Mathf.RoundToInt(amount * withoutRot)));
            int gain = Mathf.RoundToInt(Mathf.Max(0, would - (health - before)) * rotAtkShare);
            if (gain > 0)
            {
                var src = rotSource;
                engine.ApplyStatChange(src, rotTalent, () => src.attack += gain);
                BattleLog.Passive(src, $"<color=#9CCC65>[Rot] {src.DisplayName} feeds on the rotten healing: +{gain} ATK</color>");
            }
        }
        // Doppelganger: the opponent copies part of every heal
        if (!redirected && health > before && opponent != null && opponent.doppelgangerPercent > 0f && opponent.IsAlive)
            opponent.Heal(Mathf.Max(1, Mathf.RoundToInt((health - before) * opponent.doppelgangerPercent)), true);
    }

    /// <summary>Deals damage to the card, applying flat damage reduction (min 1).</summary>
    public void TakeDamage(int amount)
    {
        TakeDamage(amount, DamageKind.Skill);
    }

    /// <summary>Deals damage of a given kind. Returns the damage actually taken.</summary>
    public int TakeDamage(int amount, DamageKind kind) => TakeDamage(amount, kind, 1f);

    /// <summary>reductionScale: 0.5 = the attacker halves this card's damage reduction (Piercing Edge).</summary>
    public int TakeDamage(int amount, DamageKind kind, float reductionScale)
    {
        if (!IsAlive) return 0;
        // Counter Stance: skill damage can be evaded too (and countered)
        if (kind == DamageKind.Skill && counterStance && BattleLog.Current != null && BattleLog.Current.TryEvadeSkill(this)) return 0;
        float scaled = amount * (1f - Mathf.Clamp01(damageTakenReduction * reductionScale));
        int actualDamage = Mathf.Max(1, Mathf.RoundToInt(scaled) - Mathf.RoundToInt(damageReduction * reductionScale));
        return ApplyDamage(actualDamage, kind);
    }

    /// <summary>Shield (penetration: what the barrier can't hold goes on to the HP), Death's Dance and Guts, then the HP loss.</summary>
    int ApplyDamage(int amount, DamageKind kind)
    {
        int burst = 0;
        if (shield > 0 && amount > 0)
        {
            int absorbed = Mathf.Min(shield, amount);
            shield -= absorbed;
            amount -= absorbed;
            if (shield > 0)
                BattleLog.Status(this, $"<color=#6FD3FF>{DisplayName}'s barrier absorbs {absorbed} ({shield} left)</color>");
            else
            {
                shieldTurns = 0;
                burst = shieldBurst;
                shieldBurst = 0;
                BattleLog.Status(this, amount > 0
                    ? $"<color=#6FD3FF>{DisplayName}'s barrier absorbs {absorbed} and breaks! {amount} damage goes through</color>"
                    : $"<color=#6FD3FF>{DisplayName}'s barrier absorbs {absorbed} and breaks!</color>");
            }
            if (amount <= 0) { Explode(burst); return 0; }
        }
        int taken = ApplyHpDamage(amount, kind);
        Explode(burst); // the excess hits first, then the broken barrier explodes
        return taken;
    }

    /// <summary>Burst Barrier: a broken barrier deals its stored true damage to the opponent.</summary>
    void Explode(int burst)
    {
        if (burst <= 0 || opponent == null || !opponent.IsAlive) return;
        BattleLog.Status(this, $"<color=#6FD3FF>[Burst Barrier] The barrier explodes!</color>");
        opponent.TakeTrueDamage(burst, DamageKind.Skill);
    }

    int ApplyHpDamage(int amount, DamageKind kind)
    {
        if (damageStoreRate > 0f)
        {
            int stored = Mathf.RoundToInt(amount * damageStoreRate);
            amount -= stored;
            storedDamage += stored;
        }
        int actualDamage = Mathf.Min(Mathf.Max(0, amount), health);
        if (gutsReady && actualDamage >= health)
        {
            gutsReady = false;
            actualDamage = Mathf.Max(0, health - Mathf.Max(1, gutsHp));
            AddTimedBuff(gutsAtkBonus, 0, gutsTurns);
            BattleLog.Status(this, $"<color=#FF8A3D>[Guts] {DisplayName} refuses to fall! +{gutsAtkBonus}% ATK for {gutsTurns} turns</color>");
        }
        health -= actualDamage;
        BattleLog.Damaged(this, actualDamage, kind);
        if (damageStoreRate > 0f && health > 0 && storedDamage >= health)
        {
            BattleLog.Status(this, $"<color=#B39DDB>[Death's Dance] The stored damage ({storedDamage}) catches up with {DisplayName}!</color>");
            int rest = health;
            health = 0;
            BattleLog.Damaged(this, rest, DamageKind.True);
        }
        return actualDamage;
    }

    /// <summary>Deals true damage that bypasses all defenses and damage reduction.</summary>
    public void TakeTrueDamage(int amount)
    {
        TakeTrueDamage(amount, DamageKind.True);
    }

    public int TakeTrueDamage(int amount, DamageKind kind)
    {
        if (!IsAlive || amount <= 0) return 0;
        return ApplyDamage(amount, kind);
    }

    /// <summary>Resets stats (and max HP) to base values, clears timed buffs and restores full health.</summary>
    public void ResetToBaseStats()
    {
        attack = baseAttack;
        defense = baseDefense;
        speed = baseSpeed;
        bonusAttack = 0;
        bonusDefense = 0;
        buffTurnsLeft = 0;
        maxHealth = baseMaxHealth;
        int before = health;
        health = maxHealth;
        if (health > before) BattleLog.Healed(this, health - before);
    }

    /// <summary>Adds a timed buff with percentage-based stat bonuses.</summary>
    public void AddTimedBuff(int atkBonusPercent, int defBonusPercent, int turns)
    {
        bonusAttack += baseAttack * atkBonusPercent / 100;
        bonusDefense += baseDefense * defBonusPercent / 100;
        buffTurnsLeft = Mathf.Max(buffTurnsLeft, turns);
    }

    /// <summary>Decrements buff duration and removes buffs when expired (called at the end of this card's turn).</summary>
    public void TickBuffs()
    {
        if (buffTurnsLeft > 0)
        {
            buffTurnsLeft--;
            if (buffTurnsLeft == 0)
            {
                bonusAttack = 0;
                bonusDefense = 0;
                BattleLog.Status(this, $"{DisplayName}'s timed buff expired");
            }
        }
    }

    /// <summary>Adds raw mana, capped at maxMana (no regen modifier).</summary>
    public void GainMana(int amount)
    {
        mana = Mathf.Clamp(mana + amount, 0, maxMana);
    }

    /// <summary>Adds mana scaled by <see cref="manaRegen"/> (doubled while silenced by the Silence talent).</summary>
    public void GainManaWithRegen(int amount)
    {
        float boost = silenceTurns > 0 ? Mathf.Max(0f, silenceManaMultiplier) : 1f;
        if (shield > 0) boost *= Mathf.Max(0f, shieldManaRegen);                                     // Burst Barrier
        if (opponent != null && opponent.sleepTurns > 0) boost *= Mathf.Max(0f, opponent.sleepCasterRegen); // Sleep caster
        if (manaBlockTurns > 0) return;                                                                    // Silence: no regen at all
        GainMana(Mathf.RoundToInt(amount * Mathf.Max(0, manaRegen) / 100f * boost));
    }

    // ========== Talents ==========

    /// <summary>Adds a talent (ignored if null or already there). scale = strength (Gamble talents: 0.6).</summary>
    public void AddTalent(Talent t, float scale = 1f)
    {
        if (t == null || talents.Contains(t)) return;
        talents.Add(t);
        if (IsTeam && currentMember != null && !talentOwner.ContainsKey(t)) talentOwner[t] = currentMember; // learned in battle
        if (talent == null) talent = t;
        if (!Mathf.Approximately(scale, 1f)) talentScale[t] = scale;
    }

    public bool HasTalent<T>() where T : Talent
    {
        foreach (var t in talents) if (t is T) return true;
        return false;
    }

    public bool HasTalent(Talent t) => t != null && talents.Contains(t);

    /// <summary>Strength multiplier of one of this card's talents (1, or 0.6 for Gamble's).</summary>
    public float ScaleOf(Talent t) => t != null && talentScale.TryGetValue(t, out float s) ? s : 1f;

    public IEnumerable<ActiveTalent> Actives()
    {
        foreach (var t in talents) if (t is ActiveTalent a) yield return a;
    }

    public IEnumerable<PassiveTalent> Passives()
    {
        foreach (var t in talents) if (t is PassiveTalent p) yield return p;
    }

    /// <summary>The active talent to try this turn (cards with several actives take turns between them).</summary>
    public ActiveTalent NextActive() => NextActive(null);

    /// <summary>
    /// The active talent to try this turn. With the enemy given, an active that can't be cast any more (Soul Swap after
    /// its uses) is passed over, so it doesn't block the card's other actives for the rest of the battle.
    /// </summary>
    public ActiveTalent NextActive(CardRuntime enemy)
    {
        var list = new List<ActiveTalent>(Actives());
        if (list.Count == 0) return null;
        if (enemy == null || list.Count == 1) return list[nextActive % list.Count];
        int pick = PickUsable(list, nextActive, enemy);
        nextActive += pick;
        return list[nextActive % list.Count];
    }

    /// <summary>Steps from <paramref name="start"/> to the first active that can be cast (0 if none can).</summary>
    int PickUsable(List<ActiveTalent> list, int start, CardRuntime enemy)
    {
        for (int k = 0; k < list.Count; k++)
        {
            var a = list[(start + k) % list.Count];
            bool can = false;
            try { can = a.CanActivate(this, enemy); } catch (System.Exception ex) { Debug.LogException(ex); }
            if (can) return k;
        }
        return 0;
    }

    public void AdvanceActive() => nextActive++;

    /// <summary>True when any debuff on this card is blocked (Purify immunity, Gigantify resist).</summary>
    public bool ResistsDebuff()
    {
        if (debuffImmuneTurns > 0)
        {
            BattleLog.Status(this, $"<color=#E0F7FA>{DisplayName} is immune (Purify)</color>");
            return true;
        }
        if (debuffResistChance > 0f && BattleRandom.Value < debuffResistChance)
        {
            BattleLog.Status(this, $"<color=#E0F7FA>{DisplayName} shrugs it off (Gigantify)</color>");
            return true;
        }
        return false;
    }

    // ========== Talent State Management ==========

    /// <summary>Gets talent-specific state data (per-instance, not shared across the ScriptableObject).</summary>
    public T GetTalentState<T>(string key, T defaultValue = default)
    {
        if (talentState.TryGetValue(key, out object value) && value is T typed)
        {
            return typed;
        }
        return defaultValue;
    }

    /// <summary>Sets talent-specific state data.</summary>
    public void SetTalentState<T>(string key, T value)
    {
        talentState[key] = value;
    }

    /// <summary>Checks if talent state exists.</summary>
    public bool HasTalentState(string key)
    {
        return talentState.ContainsKey(key);
    }

    /// <summary>One-line rich-text summary of active status effects (for the UI).</summary>
    public string StatusSummary()
    {
        var parts = new List<string>();
        if (burnStacks > 0) parts.Add($"<color=#FF8A3D>BURN x{burnStacks}</color>");
        if (poisonStacks > 0) parts.Add($"<color=#7CFC6A>POISON x{poisonStacks}</color>");
        if (isStunned) parts.Add("<color=#FFE14D>STUN</color>");
        if (silenceTurns > 0) parts.Add($"<color=#C78BFF>SILENCE {silenceTurns}</color>");
        if (regenerationStacks > 0) parts.Add($"<color=#5DFFB0>REGEN x{regenerationStacks}</color>");
        if (hasEndurance) parts.Add($"<color=#6FD3FF>ENDURE {enduranceTurns}</color>");
        if (buffTurnsLeft > 0) parts.Add($"<color=#FFD54F>BLESS {buffTurnsLeft}</color>");
        if (lifesteal > 0) parts.Add($"<color=#FF6B8B>LIFESTEAL {lifesteal}%</color>");
        if (critIgnoresEvasion) parts.Add("<color=#FFB23D>SURE CRIT</color>");
        if (shield > 0) parts.Add($"<color=#6FD3FF>SHIELD {shield}</color>");
        if (debuffImmuneTurns > 0) parts.Add($"<color=#E0F7FA>IMMUNE {debuffImmuneTurns}</color>");
        if (frozen) parts.Add("<color=#81D4FA>FROZEN</color>");
        if (sleepTurns > 0) parts.Add($"<color=#9FA8DA>SLEEP {sleepTurns}</color>");
        if (blindTurns > 0) parts.Add($"<color=#B0BEC5>BLIND {blindTurns}</color>");
        if (hexTurns > 0) parts.Add($"<color=#CE93D8>HEX {hexTurns}</color>");
        if (hypnoStacks > 0) parts.Add($"<color=#BA68C8>HYPNO x{hypnoStacks}</color>");
        if (momentumStacks > 0) parts.Add($"<color=#FF8A65>MOMENTUM x{momentumStacks}</color>");
        if (chargeStored > 0) parts.Add($"<color=#FFB300>CHARGE {chargeStored}</color>");
        if (chargeStrikeReady) parts.Add("<color=#FFB300>CHARGED</color>");
        if (storedDamage > 0) parts.Add($"<color=#B39DDB>STORED {storedDamage}</color>");
        return string.Join("  ", parts);
    }
}
