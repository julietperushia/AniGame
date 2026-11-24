# Talents

**Source of truth:** `Assets/Scripts/Card/Talent.cs`, `Assets/Scripts/Talents/Active/*.cs`,
`Assets/Scripts/Talents/Passive/*.cs` (design doc section 6)

Every card has exactly one talent. There are **38**: 25 active and 13 passive. **Every talent can appear at
every rarity** (user decision, `GameRules.cs:32`). New cards and Talent Orb rerolls pick one uniformly from all
talent assets (`CardDatabase.RandomTalent`).

## How talents work

| Kind | Fires when | Hooks |
|---|---|---|
| **Active** | The mana bar is full, at the start of the card's turn, before its normal attack. Cost = `max(manaCost, maxMana)`, normally 100. Silence blocks it | `Activate` |
| **Passive** | Automatically on its hook | `OnBattleStart`, `OnTurnStart`, `OnBeforeAttack`, `OnAfterAttack`, `OnTakeDamage` |

The card still makes its normal attack after an active talent fires, so buffs and debuffs never cost a turn.
Timing details: [combat.md](combat.md#one-turn).

### Rarity scaling

Talent power is `1 + 0.20 × tier / 8`: **×1.00 at Common up to ×1.20 at God**. Each talent passes most of its
numbers through `Scaled()`. In the tables below, **bold** numbers scale and plain numbers are fixed. Example:
Amplifier ATK gives +14% at Common and +17% at God (14 × 1.2 = 16.8, rounded).

## Active talents (25)

| Talent | Effect (base numbers) | Damage type |
|---|---|---|
| **Amplifier ATK** | Permanently +**14%** of base ATK. Stacks every cast | — |
| **Amplifier DEF** | Permanently +**14%** of base DEF. Stacks every cast | — |
| **Balancing Strike** | True damage = **15%** of your max HP if your HP% is below the enemy's, otherwise **5%** | True |
| **Berserk Strike** | **12%** of your ATK × (1 + % of your HP missing), so up to ×2 | Skill, elemental |
| **Blaze** | Applies 1 Burn stack: **5%** max HP per turn per stack (max 4), −75% healing received | True (over time) |
| **Breaker ATK** | Enemy ATK −**20%** of its base | — |
| **Breaker DEF** | Enemy DEF −**20%** of its base | — |
| **Celestial Blessing** | Max mana +50 (up to 600; not while silenced), Mana Regen +**18%**, heal **2×** max mana | Heal |
| **Dexterity Drive** | **5%** of your SPD + **5** per point of SPD above the enemy's | Skill, elemental |
| **Double Edged Strike** | **18%** of your ATK; you take 1/4 of the damage dealt as recoil | Skill, elemental |
| **Elemental Strike** | **12%** of your ATK × element multiplier | Skill, elemental |
| **Endurance** | For 3 turns: normal attacks against you −65%, reflect **35%** of the blocked damage | True (reflect) |
| **Evasion** | +**17** evasion points, stacks, capped at 60% | — |
| **Lucky Coin** | Roll d20 × **3** → add it to a random stat: HP (×10, also heals that much), ATK, DEF or SPD | — |
| **Mana Reaver** | Steal **20%** of the enemy's mana; true damage = stolen mana × 10 | True |
| **Paralysis** | **40%** chance to Stun: the enemy skips its next turn and loses 10% DEF permanently | — |
| **Poison** | Applies 1 Poison stack: 5 + **5%** max HP per turn per stack; the tick can miss (10% +5% per turn) | True (over time) |
| **Precision** | +**20** crit rate points (cap 100%) and +**20** crit damage points. Stacks | — |
| **Regeneration** | Heal **7.5%** max HP now and gain a Regen stack (heals 7.5% each turn per stack, max 3) | Heal |
| **Restricted Instinct** | Silence the enemy for **3** turns: no talent, mana capped at 100 | — |
| **Trick Room ATK** | If the enemy has more ATK, steal **60%** of the difference | — |
| **Trick Room DEF** | If the enemy has more DEF, steal **60%** of the difference | — |
| **Unlucky Coin** | Roll d20 × **2** → subtract it from a random enemy stat: max HP (×10), ATK, DEF or SPD | — |
| **Vengeance ATK** | True damage = **11%** of the enemy's ATK (×10 scale) | True |
| **Vengeance DEF** | True damage = **11%** of the enemy's DEF (×10 scale) | True |

## Passive talents (13)

| Talent | Hook | Effect (base numbers) |
|---|---|---|
| **Berserker** | Turn start | While HP < 30%, ATK +**30%** of base (removed again if you heal above 30%) |
| **Blood Surge** | Turn start | The first time HP ≤ 30%: +**45%** lifesteal for the rest of the battle |
| **Bloodthirster** | Turn start | First turn: +**18%** lifesteal and +**23%** healing (cap 50%). Each turn after: +**5%** lifesteal (cap 100%) |
| **Celestial Influence** | Battle start | Your Mana Regen +**40%**; the enemy's Mana Regen −**14%** |
| **Divine Blessing** | Battle start | +**30%** ATK and +**30%** DEF for 5 turns |
| **Dominance** | Turn start | While your HP% ≥ the enemy's, ATK +**17%** of base |
| **Grievous Limiter** | Battle start, turn start | The enemy's ATK −**40%** of base at the start; it recovers 5% of base each turn |
| **Lethal Clarity** | Battle start, after attack | +**12** crit rate points and +**12%** ATK. Critical hits heal **45%** of the damage dealt |
| **Life Sap** | Turn start | Deal **2%** of the enemy's max HP (skill damage) and heal 175% of what was dealt |
| **Overload ATK** | Battle start, turn start | ATK +**60%** of base at the start, then −8% of base per turn, down to base |
| **Overload DEF** | Battle start, turn start | DEF +**60%** of base at the start, then −8% of base per turn, down to base |
| **Protector** | Turn start | Once, when HP < 30%: heal **20%** max HP, DEF +**35%**, take **5%** less damage |
| **Reversion** | Turn start | Once, when HP < 20%: **both** fighters reset to base stats and full HP, then you gain +**12%** DEF and SPD |

## Balance notes

- Burn was halved (10% → 5% per stack) and capped at 4 stacks after Blaze won 76% of even fights in
  simulation (`StatusEffects.cs:7`).
- Regeneration is capped at 3 stacks because uncapped stacks caused most timed-out draws
  (`Regeneration.cs:7`).
- **Overload DEF** never appears on a campaign enemy (it isn't in `campaign.csv`), but players can still roll it.
- Talent text comes from each class's `DefaultDescription`, which builds the string from the talent's
  unscaled fields. So descriptions always show the base (Common) numbers, even on a God card.

## Adding a talent

See [data-pipeline.md](../tech/data-pipeline.md#add-a-talent). Talents are ScriptableObjects shared by every
card that uses them, so **never keep per-battle state in fields**: use `CardRuntime.Get/SetTalentState`
(`Talent.cs:5`).
