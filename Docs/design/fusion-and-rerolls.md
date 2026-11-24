# Fusion, rerolls, God Orbs and dismantling

**Source of truth:** `Assets/Scripts/Rules/GameRules.cs`, `Assets/Scripts/Data/PlayerProfile.cs`
(design doc sections 3, 4 and 10). All of this happens on the **My Cards** screen.

## Fusion

From `GameRules.cs:36-44` and `PlayerProfile.Fuse`:

**Recipe:** 1 main card + **2 fodder cards of the same rarity** + Fusion Orbs.

| From → To | Success rate | Fusion Orbs | Expected tries |
|---|---|---|---|
| Common → Uncommon | 100% | 1 | 1.0 |
| Uncommon → Rare | 90% | 1 | 1.1 |
| Rare → Super Rare | 75% | 2 | 1.3 |
| Super Rare → Ultra Rare | 60% | 3 | 1.7 |
| Ultra Rare → Epic | 55% | 5 | 1.8 |
| Epic → Legendary | 40% | 8 | 2.5 |
| Legendary → Mythic | 25% | 12 | 4.0 |
| Mythic → God | 15% | 20 | 6.7 |

- **Success:** the main card goes up one rarity and **keeps its rolls, element and talent**. Both fodder cards
  are used up. Stats rise because the rarity multiplier rises.
- **Failure:** the orbs **and both fodder cards** are lost. The main card is never lost.
- Using extra orbs does **not** raise the rate (user change, `GameRules.cs:43`).
- **Mythic → God** needs the **New Generation** buff (3,000 diamonds).
- Taking one main card from Common to God with no failures costs 1+1+2+3+5+8+12+20 = **52 Fusion Orbs**, not
  counting the fodder cards, which have to be found or fused at each rarity too.
- Locked cards and cards in a build slot are never used as fodder. The fodder list is sorted weakest first
  (lowest total roll).

### Fusion buffs

| Buff | Effect on fusion |
|---|---|
| Fusion Fortune | +3 points success rate (capped at 100%) |
| Fusion Protection | On a fail, lose 1 fodder instead of 2 (the weaker one goes) |
| Fusion Blessing | 3% chance a success jumps **two** rarities, but only if the result is Legendary or lower |
| Lucky Fusion | After any fusion: 5% + 5% per orb used chance to get 1 orb back |

### Auto merge

`PlayerProfile.AutoMerge`: fuses automatically, lowest rarity first, up to a **target rarity** and within an
**orb budget**. It stops when the budget is spent, orbs run out, or nothing more can be fused.

- The main card for each rarity is chosen in this order: cards in a build slot, then locked cards, then the
  best total roll. So your real card goes up first.
- Locked and slotted cards are only ever the main card, never fodder.
- Before running it, the screen shows a best-case estimate (assuming every fusion succeeds)
  (`AutoMergeEstimate`).

## Rerolls

Rerolls never change a card directly. They produce a **preview build** that you can keep rerolling, and it
becomes real only when you press **SAVE TO SLOT** (`CollectionScreen.cs` header). Every reroll is paid when you
press it, even if you never save the result.

| Reroll | What changes | Cost |
|---|---|---|
| **Stats** | All 4 rolls, re-rolled inside the card's ± range | Gold, by rarity (table below) |
| **Element** | Element, using the [Element Orb odds](elements.md#element-orb-odds) | 1 Element Orb, or its shop price in gold (3,500) |
| **Talent** | Talent, chosen uniformly from all 38 | 1 Talent Orb, or its shop price in gold (7,000) |

An orb you own is always used first. **Efficient Upgrader** takes 10% off every gold reroll cost.

### Stat reroll and dismantle values

From `GameRules.cs:23` and `GameRules.cs:78`:

| Rarity | Stat reroll (gold) | Dismantle (gold) |
|---|---|---|
| Common | 500 | 250 |
| Uncommon | 800 | 400 |
| Rare | 1,200 | 600 |
| Super Rare | 2,000 | 1,000 |
| Ultra Rare | 3,500 | 1,750 |
| Epic | 6,000 | 3,000 |
| Legendary | 10,000 | 5,000 |
| Mythic | 16,000 | 8,000 |
| God | 25,000 | 12,500 |

## God Orb

From `GameRules.cs:80-86` and `PlayerProfile.UseGodOrb`. This was the user's own addition to the design.

- Works **only on God cards**.
- Each use: **50%** chance of +1% to every stat (`godBonus +1`), up to **+20%** total. A failed use still spends
  the orb.
- **Never sold.** Sources: God-rarity floor enemies (10% per win), and diamond packs as a bonus item (Ultra
  Rare 1%, Epic 3%, Legendary 6% per pack).
- Expected orbs to max one card: 20 ÷ 0.5 = **40**.

## Dismantling

- Unlocked by the **Salvage Expert** buff (5,000 gold).
- Destroys a card for **half its rarity's stat reroll cost** in gold (table above). Golden Touch adds its +10%.
- Locked and slotted cards can't be dismantled.

## Locking

Any card can be locked from My Cards. Locked cards can't be fodder or dismantled. Cards in a build slot are
protected automatically.
