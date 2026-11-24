# Cards and rarity

**Source of truth:** `Assets/Scripts/Rules/GameRules.cs`, `Assets/Scripts/Card/CardStats.cs`,
`Assets/Scripts/Data/CardInstance.cs`, `Assets/Scripts/Data/PlayerProfile.cs`

## What a card is

Every card you own is **your character** (see [GDD §3](../GDD.md#3-player-character)). An owned card
(`CardInstance`) has:

| Field | Meaning |
|---|---|
| `rarity` | Common … God (tier 0–8) |
| `build.hp / atk / def / spd` | The **roll** on each stat: a small plus or minus around the base |
| `build.element` | One of the 9 [elements](elements.md) |
| `build.talent` | One of the 38 [talents](talents.md), stored by name |
| `godBonus` | Successful God Orbs used on it (0–20, God cards only) |
| `locked` | The player locked it, so it can't be used as fodder or dismantled |

Campaign enemies are separate `Card` assets with fixed stats taken from the campaign sheet
([campaign.md](campaign.md)).

## Stats

| Stat | Player default | Notes |
|---|---|---|
| HP | from the formula, ×10 | Shown and used on the ×10 scale (stat 100 = 1,000 HP) |
| ATK, DEF, SPD | from the formula | SPD decides who acts first ([combat](combat.md#turn-order)) |
| Crit rate | 5% | |
| Crit damage | 135% | |
| Evasion | 5% | |
| Mana | 0 / 100 | Active talents fire when the bar is full |

Player defaults are set in `Assets/Scripts/Card/CardRuntime.cs` (`ForBuild`). Enemies use the sheet's crit and
evasion values, falling back to the same defaults.

### Stat formula (`GameRules.cs:28`)

```
stat = round( (100 + roll) × (1 + 0.05 × tier) × (1 + 0.01 × godBonus) )
HP   = stat(hp roll) × 10
Power = HP/10 + ATK + DEF + SPD
```

- Base stat: **100** for every card (user decision, `GameRules.cs:13`).
- Rarity multiplier: **+5% per tier**, additive, capped at +40% for God (`GameRules.cs:18`).
- Roll range: **±(5 + 2 × tier)** per stat (`GameRules.cs:21`). New cards roll each stat uniformly inside that
  range (`PlayerProfile.RandomRoll`).

## Rarity table

All values are derived from the formulas above. "Stat range" is one stat from the worst roll to the best,
before God Orbs. "Power" is the sum of all four stats.

| Tier | Rarity | Short | Multiplier | Roll | Stat range | Power at roll 0 | Power range | Talent power | Floor drop chance |
|---|---|---|---|---|---|---|---|---|---|
| 0 | Common | C | ×1.00 | ±5 | 95–105 | 400 | 380–420 | +0% | 12% |
| 1 | Uncommon | UC | ×1.05 | ±7 | 98–112 | 420 | 392–448 | +2.5% | 10% |
| 2 | Rare | R | ×1.10 | ±9 | 100–120 | 440 | 400–480 | +5% | 8% |
| 3 | Super Rare | SR | ×1.15 | ±11 | 102–128 | 460 | 408–512 | +7.5% | 6% |
| 4 | Ultra Rare | UR | ×1.20 | ±13 | 104–136 | 480 | 416–544 | +10% | 4% |
| 5 | Epic | EP | ×1.25 | ±15 | 106–144 | 500 | 424–576 | +12.5% | 2% |
| 6 | Legendary | LG | ×1.30 | ±17 | 108–152 | 520 | 432–608 | +15% | 1% |
| 7 | Mythic | MY | ×1.35 | ±19 | 109–161 | 540 | 436–644 | +17.5% | 1% |
| 8 | God | GOD | ×1.40 | ±21 | 111–169 | 560 | 444–676 | +20% | never |

- **Talent power** is `1 + 0.20 × tier / 8` (`GameRules.cs:33`). Every number a talent uses is multiplied by it
  ([talents.md](talents.md#rarity-scaling)).
- **Floor drop chance** is the chance to win a card from a floor enemy of that rarity: 12% − 2% per tier, at
  least 1%, and never for God (`GameRules.cs:51`). See [economy](economy-and-stamina.md#battle-rewards).
- With 20 God Orbs, a perfect God card reaches 203 per stat, for a power of **812**.
- A Mythic card can be fused into God only after you buy the **New Generation** buff ([buffs.md](buffs.md)).

### Rarity colours (UI)

From `CardStats.RarityColor`: Common grey, Uncommon green, Rare blue, Super Rare purple, Ultra Rare orange,
Epic pink-red, Legendary gold, Mythic cyan, God pale gold with an animated rainbow frame.

## Build slots

From doc section 10 and `GameRules.cs:66-68`.

| Slot | Unlock cost |
|---|---|
| 1 | Free |
| 2 | 300 diamonds |
| 3 | 600 diamonds |

- A slot stores **a card plus its own copy of a build** (rolls, element, talent). Rerolls create a preview,
  and the preview becomes real only when you **SAVE TO SLOT** (`PlayerProfile.SaveToSlot`).
- You choose the slot on the Location Map before each fight. Only the active slot's card fights
  ([known issue](../project/roadmap.md#known-issues): the battle screen's comment mentions relay battles).
- A card used by a slot is **protected**: it can't be fodder or be dismantled (`PlayerProfile.IsProtected`).
- The **Berserker** buff makes slots 10% cheaper.

## Starting state

From `GameRules.cs:89-90` and `PlayerProfile.NewGame`:

- 3,000 gold, 2 Fusion Orbs, 80 stamina, 0 diamonds.
- One Common card with a random build, saved in slot 1.
