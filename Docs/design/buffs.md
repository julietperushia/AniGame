# Permanent buffs

**Source of truth:** `Assets/Scripts/Rules/Buffs.cs` (user list, 15 Nov 2026). Effects are applied in
`PlayerProfile.cs` and `BattleEngine.cs`.

Each buff is **bought once and kept forever**, on the **Buffs** screen. Prices are mixed: economy and stamina
buffs cost gold, while fusion, collection and combat buffs cost diamonds. A "+X%" chance adds percentage points.

## Fusion & RNG (diamonds)

| Buff | Price | Effect |
|---|---|---|
| Fusion Protection | 300 diamonds | A failed fusion loses 1 fodder card instead of 2 |
| Fusion Blessing | 800 diamonds | 3% chance a successful fusion jumps one extra rarity (never into Mythic or God) |
| Fusion Fortune | 300 diamonds | +3% fusion success rate |
| Lucky Fusion | 100 diamonds | After a fusion: 5% + 5% per Fusion Orb used chance to get 1 orb back |

## Cards & Collection (diamonds)

| Buff | Price | Effect |
|---|---|---|
| Hunter's Instinct | 300 diamonds | +10% chance to get a card from a defeated enemy |
| Rare Encounter | 300 diamonds | 3% chance a card you get is one rarity higher (only below Ultra Rare, so it tops out at Ultra Rare) |
| Treasure Hunter | 100 diamonds | 5% chance per win of rare loot: Fusion / Element / Talent Orb or 10 diamonds |
| Berserker | 50 diamonds | −10% build slot unlock cost |

## Economy & Resources (gold)

| Buff | Price | Effect |
|---|---|---|
| Gold Cashback | 40,000 | 15% of the gold you spend comes back |
| Golden Touch | 15,000 | +10% gold earned |
| Merchant's Favor | 5,000 | −5% in the orb shop |
| Efficient Upgrader | 15,000 | −10% gold on card rerolls |
| Salvage Expert | 5,000 | Unlocks dismantling: a card is worth half its rarity's stat reroll cost |

## Combat & Exploration (combat buffs: diamonds; stamina buffs: gold)

| Buff | Price | Effect |
|---|---|---|
| The Flash | 300 diamonds | +2% evasion |
| Head Hunter | 300 diamonds | +2% crit chance |
| Strongest Hero | 100 diamonds | 1% chance the enemy loses 50% HP when the battle starts |
| Second Wind | 100 diamonds | 1% chance to revive with 1 HP instead of dying (once per battle) |
| Stamina Master | 40,000 | Stamina regen 2 per 5 minutes instead of 1 |
| Elixir | 15,000 | +2 stamina after any win |
| Potion of Stamina | 15,000 | Max stamina 80 → 100 (also adds 20 stamina immediately) |
| Returning Heroes | 5,000 | +1 stamina when you are defeated |
| **New Generation** | **3,000 diamonds** | **Unlocks the God rarity: Mythic cards can be fused into God** |

## Totals

| Currency | Buffs | Total cost |
|---|---|---|
| Gold | 9 | 155,000 gold |
| Diamonds | 13 | 6,050 diamonds |

## Design notes

- **New Generation** is the late-game gate. It costs almost as many diamonds as all 12 other diamond buffs combined
  (3,050), and without it God cards can't be made at all.
- Buff ids (`fusion_protection`, `new_generation`, …) are stored in save files. **Never rename an id**
  (`Buffs.cs:24`). Change the display name instead.
- Combat buffs only apply to the player (`PlayerProfile.ApplyCombatBuffs`).

## Adding a buff

See [data-pipeline.md](../tech/data-pipeline.md#add-a-permanent-buff).
