# Elements

**Source of truth:** `Assets/Scripts/Battle/ElementSystem.cs`, `Assets/Scripts/Card/CardElement.cs`
(design doc section 5)

## The 9 elements

| Element | Short | UI colour | Type |
|---|---|---|---|
| Fire | FIRE | orange-red | Cycle |
| Tree | TREE | green | Cycle |
| Rock | ROCK | brown | Cycle |
| Lightning | BOLT | yellow | Cycle |
| Water | WATER | blue | Cycle |
| Dark | DARK | purple | Pair |
| Light | LIGHT | pale yellow | Pair |
| Non-Neutral | NON-N | pink | Special |
| Neutral | NEUT | grey | Special |

## Rules

From `ElementSystem.cs:5-11`, `36-58`:

- **Cycle:** Fire > Tree > Rock > Lightning > Water > Fire. The winner deals **+25%** and the loser deals
  **−25%**.
- **Dark ⇄ Light:** each deals **+25%** to the other.
- **Same element:** **−12.5%**.
- **Non-Neutral attacking:** **+12.5%** against every element except Neutral and Non-Neutral (**−12.5%**).
- **Neutral attacking:** no change, except **+25%** against Non-Neutral.
- A cycle or pair element attacking Neutral or Non-Neutral: no change.
- Any other pairing (for example Fire vs Rock, or Water vs Dark): no change.

## Damage chart

Rows are the **attacker**, columns the **defender**. The multiplier applies to normal attacks and to
"elemental" talent damage.

| ATK ↓ / DEF → | Fire | Tree | Rock | Lightning | Water | Dark | Light | Non-N | Neutral |
|---|---|---|---|---|---|---|---|---|---|
| **Fire** | −12.5% | **+25%** | 0 | 0 | −25% | 0 | 0 | 0 | 0 |
| **Tree** | −25% | −12.5% | **+25%** | 0 | 0 | 0 | 0 | 0 | 0 |
| **Rock** | 0 | −25% | −12.5% | **+25%** | 0 | 0 | 0 | 0 | 0 |
| **Lightning** | 0 | 0 | −25% | −12.5% | **+25%** | 0 | 0 | 0 | 0 |
| **Water** | **+25%** | 0 | 0 | −25% | −12.5% | 0 | 0 | 0 | 0 |
| **Dark** | 0 | 0 | 0 | 0 | 0 | −12.5% | **+25%** | 0 | 0 |
| **Light** | 0 | 0 | 0 | 0 | 0 | **+25%** | −12.5% | 0 | 0 |
| **Non-Neutral** | +12.5% | +12.5% | +12.5% | +12.5% | +12.5% | +12.5% | +12.5% | −12.5% | −12.5% |
| **Neutral** | 0 | 0 | 0 | 0 | 0 | 0 | 0 | **+25%** | 0 |

### In the battle log

Normal attacks (and the skills that use the element multiplier) say how well they hit
(`ElementSystem.Effectiveness`):

| Multiplier | Battle log |
|---|---|
| ×1.25 (and team averages ≥ ×1.2) | **SUPER EFFECTIVE!** |
| ×1.125 (above ×1.01) | slightly effective |
| ×0.875 (×0.85 – ×0.99) | not very effective |
| ×0.75 (below ×0.85) | barely effective... |
| ×1 | nothing |

In 3v3 the multiplier is the average over the defending team, so it can land between these values.

### Reading the chart as a player

- **Non-Neutral** is the "safe" pick: +12.5% against almost everything, never −25%, but weak to Neutral.
- **Neutral** never loses damage, but only gains against Non-Neutral.
- Cycle elements are high-risk, high-reward: +25% against one element, −25% against another.

## Element Orb odds

When an element is rolled (new card, Element Orb reroll), it is drawn with these weights
(`ElementSystem.cs:32`):

| Fire | Tree | Rock | Lightning | Water | Dark | Light | Non-Neutral | Neutral |
|---|---|---|---|---|---|---|---|---|
| 13% | 13% | 13% | 13% | 13% | 11% | 11% | 5% | 8% |

## Campaign enemies

The campaign sheet has no `ELEMENT` column, so each enemy gets a **fixed element worked out from a hash of its
name**, spread with the same odds as above (`ElementSystem.FromName`). The same character always gets the same
element, even after the sheet is regenerated. Adding an `ELEMENT` column to the sheet overrides this.

A card dropped by a floor enemy keeps **that enemy's element** (and talent).

## Rule for code changes

`CardElement` integer values are stored in assets and save files. **Only add new values at the end.** Never
reorder or renumber them (`CardElement.cs:5`).
