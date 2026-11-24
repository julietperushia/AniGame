# Gacha packs

**Source of truth:** `Assets/Scripts/Rules/Packs.cs` (design doc section 7), `PlayerProfile.OpenPack`,
`GameRules.GodOrbPackChance`

## How a pack works

- Every pack gives **5 items**.
- **Slot 1** is always a card of the pack's rarity.
- **Slots 2–5** each roll the pack's table on their own (the chances add up to 100%).
- Diamond packs can also add a **bonus God Orb** on top of the 5 items.
- **No pity:** there is no guaranteed reward after a run of bad luck.
- Every card is **your character** with a random build (rolls, element, talent). **Rare Encounter** can raise a
  card one rarity (up to Ultra Rare).
- Gold items count as earned gold, so Golden Touch applies. Gold spent on gold packs counts for Gold Cashback.

## Gold packs (doc 7.1): the only source of diamonds

### Common Pack: 1,000 gold, slot 1 = Common

| Item | Chance per slot |
|---|---|
| Common card | 50% |
| Uncommon card | 10% |
| 300 gold | 20% |
| 1 Fusion Orb | 10% |
| 1 Element Orb | 3% |
| 1 Talent Orb | 3% |
| 5 diamonds | 4% |

### Rare Pack: 3,000 gold, slot 1 = Rare

| Item | Chance per slot |
|---|---|
| Uncommon card | 35% |
| Rare card | 12% |
| Super Rare card | 1% |
| 1,000 gold | 18% |
| 2 Fusion Orbs | 15% |
| 1 Element Orb | 6% |
| 1 Talent Orb | 5% |
| 15 diamonds | 8% |

### Super Rare Pack: 10,000 gold, slot 1 = Super Rare

| Item | Chance per slot |
|---|---|
| Rare card | 30% |
| Super Rare card | 10% |
| Ultra Rare card | 1% |
| 3,000 gold | 13% |
| 3 Fusion Orbs | 18% |
| 1 Element Orb | 8% |
| 1 Talent Orb | 8% |
| 50 diamonds | 12% |

## Diamond packs (doc 7.2): never contain diamonds

### Ultra Rare Pack: 100 diamonds, slot 1 = Ultra Rare. Bonus God Orb: 1%

| Item | Chance per slot |
|---|---|
| Super Rare card | 30% |
| Ultra Rare card | 8% |
| Epic card | 1% |
| 5 Fusion Orbs | 20% |
| 2 Element Orbs | 15% |
| 2 Talent Orbs | 16% |
| 10,000 gold | 10% |

### Epic Pack: 500 diamonds, slot 1 = Epic. Bonus God Orb: 3%

| Item | Chance per slot |
|---|---|
| Ultra Rare card | 30% |
| Epic card | 6% |
| Legendary card | 0.5% |
| 10 Fusion Orbs | 22% |
| 3 Element Orbs | 15% |
| 3 Talent Orbs | 16.5% |
| 30,000 gold | 10% |

### Legendary Pack: 1,000 diamonds, slot 1 = Legendary. Bonus God Orb: 6%

| Item | Chance per slot |
|---|---|
| Epic card | 30% |
| Legendary card | 4% |
| Mythic card | 0.3% |
| 20 Fusion Orbs | 25% |
| 5 Element Orbs | 15% |
| 5 Talent Orbs | 15.7% |
| 80,000 gold | 10% |

## Expected value per pack

Derived: 4 rolled slots × chance × amount.

| Pack | Diamonds | Fusion Orbs | Element Orbs | Talent Orbs | Gold back |
|---|---|---|---|---|---|
| Common | 0.8 | 0.4 | 0.12 | 0.12 | 240 |
| Rare | 4.8 | 1.2 | 0.24 | 0.2 | 720 |
| Super Rare | 24 | 2.16 | 0.32 | 0.32 | 1,560 |
| Ultra Rare | — | 4.0 | 1.2 | 1.28 | 4,000 |
| Epic | — | 8.8 | 1.8 | 1.98 | 12,000 |
| Legendary | — | 20.0 | 3.0 | 3.14 | 32,000 |

Mythic cards appear only in Legendary packs (0.3% per slot, so about 1.2% per pack). **God cards never come
from packs:** fusing a Mythic is the only way to get one.
