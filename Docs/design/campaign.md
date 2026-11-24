# Campaign

**Source of truth:** `Assets/Data/campaign.csv`, `Assets/Editor/CampaignGenerator.cs`,
`Assets/Scripts/Data/CardDatabase.cs`, `Assets/Scripts/Data/PlayerProfile.cs` (campaign progress)

## Structure

| | |
|---|---|
| Locations (LOC) | **100**, each themed on one anime (82 different anime in total) |
| Floors | **2,358** in total, 12–36 per location (average 23.6) |
| Enemy per floor | One anime character, as a `Card` asset in `Assets/Campaign/LOC n/Floor k.asset` |

### Floor codes

The sheet's `FLOOR` column is **letter + tier**: `A1, B1, C1 … A2, B2 … A3 …`. The letter is the character and
the number is its tier. Floors are ordered by **tier first, then letter**, so a location has you fight each
character at tier 1, then all of them again at tier 2, then at tier 3. A higher tier means a higher rarity.
LOC 1, for example:

| Floors 1–6 | Floors 7–12 | Floors 13–18 |
|---|---|---|
| A1–F1: Common | A2–F2: Uncommon | A3–F3: Rare |

A character keeps the same talent at every tier.

## Unlock rules

From `PlayerProfile.cs:1071-1114`:

- LOC 1 is open from the start. **LOC n+1 unlocks when every floor of LOC n is cleared.**
- Inside a location, floors unlock one at a time: you can fight any cleared floor or the next uncleared one.
- Clearing a location for the first time **refills stamina to max**.
- Cleared floors can be fought again for gold and drops. This is how you farm.

## Difficulty curve

### Enemy rarity by location band

Counted from `campaign.csv`:

| LOC | Rarities (tier 1 / 2 / 3) |
|---|---|
| 1–10 | Common / Uncommon / Rare |
| 11–20 | Uncommon / Rare / Super Rare |
| 21–30 | Rare / Super Rare / Ultra Rare |
| 31–40 | Super Rare / Ultra Rare / Epic |
| 41–50 | Ultra Rare / Epic / Legendary |
| 51–60 | Epic / Legendary / Mythic |
| 61–70 | Legendary / Mythic / God |
| 71–80 | Mythic / God / God |
| 81–100 | God / God / God |

An enemy's rarity sets its **talent power** (+0% … +20%), its **card drop chance**, and whether it can drop a
God Orb (God enemies only). It does **not** set its stats.

### Enemy power

The sheet's `POWER` column (HP + ATK + DEF + SPD) is **90 + 10 × LOC**: 100 at LOC 1, 590 at LOC 50, 1,090 at
LOC 100. Each tier inside a location adds about +1 to +3 per stat. Enemy stats are used unchanged in battle
(`GameRules.EnemyStatScale = 1`, user decision).

### Player power against enemy power

Comparing stat totals only ([cards-and-rarity.md](cards-and-rarity.md#rarity-table)):

| Player card | Power | Matches enemy power at |
|---|---|---|
| Common, roll 0 | 400 | LOC 31 |
| Ultra Rare, roll 0 | 480 | LOC 39 |
| Legendary, roll 0 | 520 | LOC 43 |
| God, roll 0 | 560 | LOC 47 |
| God, perfect rolls | 676 | LOC 58–59 |
| God, perfect rolls, +20% God Orbs | 812 | LOC 72 |

**Balance observation (not checked by simulation):** a fresh Common card has four times LOC 1's enemy power,
so the opening is very easy. Past about **LOC 72**, every enemy has a higher stat total than the best possible
player card. Winning there depends on element advantage, talents, and the damage formula's flat term. This
needs a check with **Simulate Balance**, or a late-LOC simulation pass. It is listed in the
[roadmap](../project/roadmap.md#open-design-questions).

## The sheet

Columns, generator behaviour and validation: [tech/data-pipeline.md](../tech/data-pipeline.md#campaign-sheet).

- The sheet has **no ELEMENT column**, so enemy elements come from a hash of the character's name
  ([elements.md](elements.md#campaign-enemies)).
- 37 of the 38 talents appear on enemies. **Overload DEF** is missing.
- Changing the sheet changes the database's data version, and that **resets player saves** (with a backup)
  ([save-format.md](../tech/save-format.md#versioning-and-resets)).
