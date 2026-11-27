# Talents

**Source of truth:** the talent ScriptableObjects in `Assets/Scriptable Objects/Talent/Active|Passive/` (every
number is a field there) and their classes in `Assets/Scripts/Talents/Active|Passive/*.cs` (base class
`Assets/Scripts/Card/Talent.cs`). A spreadsheet of the same list, with every field value, is in
[`Docs/talents.csv`](../talents.csv).

There are **74 talents: 40 active and 34 passive** (snapshot of the assets on 2026-11-26).
Every talent can appear at every rarity. A card has one talent; **God** cards have two (an active and a passive).
Gamble and Mimic can add more during a battle.

## How talents work

| Kind | Fires when | Hooks |
|---|---|---|
| **Active** | At the start of the card's turn, when it has enough Mana: in 1v1 the cost is `max(manaCost, max Mana)`, a full bar (Celestial Blessing only needs its own cost). The card still makes its normal attack afterwards. Silenced cards (Restricted Instinct) can't cast. A card with several actives takes turns between them and skips one that can't be cast any more (Soul Swap after its use). In 3v3 the team pays exactly `manaCost` from its shared pool | `Activate`, `CanActivate` |
| **Passive** | Automatically on its hook | `OnBattleStart`, `OnTurnStart`, `OnBeforeAttack`, `OnAfterAttack`, `OnTakeDamage`, `OnHitTaken`, `OnAnyAttack`, `OnEnemySkill`, `OnEvade`, `OnAttackMissed`, `OnLearnedInBattle` |

- **Descriptions are generated from the numbers** (`DefaultDescription`, `autoDescription` on the asset). Change a
  number in the Inspector and the text follows; *Tools > AniGame > Sync Talent Descriptions* refreshes all of them.
  Only Chaotic has custom text.
- **Stat rules** run after every talent call (`BattleEngine.Guarded`): Purify immunity and Gigantify resist block
  stat drops from the enemy, Chaotic reverses every change, Blackhole swallows the enemy's gains (30% of them go
  to the Blackhole card) and halves its owner's, Doppelganger copies 30% of the enemy's gains.
- **Per-battle state never lives in talent fields** (the assets are shared): use `CardRuntime.Get/SetTalentState`.

### Rarity scaling

Talent power is `1 + 0.20 × tier / 8`: **×1.00 at Common up to ×1.20 at God**. Numbers that scale are written
"5% (Max 6%)": the first value is the Common value, Max is the God value. Numbers without a Max are fixed.
The Library shows this "(Max …)" text; a card in the Collection or in battle shows its own value for its rarity.

## Rules that affect every battle

- **Healing fatigue:** after round 10, all healing received (heals, lifesteal, Regeneration…) drops by 5% more
  every round: −5% in round 11, −50% in round 20, no healing from round 30. Set in `GameBalance.asset`
  (`healingFatigueAfterRound`, `healingFatiguePerRound`).
- **Enrage:** from round 20 (× HP multiplier / 10) both fighters gain +10% of base ATK every round.
- **Barriers** (Burst Barrier) absorb damage first; whatever they can't hold goes straight on to the HP, then the
  broken barrier explodes.

## Active talents (40)

| Talent | Category | Effect (base value, **Max** = God) | Mana |
|---|---|---|---|
| **Assassinate** | Attack | Strike once for 130% (Max 156%) of a normal attack. The hit is always a critical hit (your crit damage applies) and can't be evaded. It is your normal attack for this turn (you don't attack again), so lifesteal and on-hit effects work. | 100 |
| **Balancing Strike** | Attack | Deal true damage equal to 15% (Max 18%) of your max HP if your HP% is lower than the enemy's; otherwise 5% (Max 6%) of your max HP. | 100 |
| **Berserk Strike** | Attack | Deal damage of your element equal to 12% (Max 14.4%) of your ATK. It grows with the HP you are missing: +50% at half HP, up to +100% near 0 HP. Element advantage applies. | 100 |
| **Charge Strike** | Attack | Skip your attack this turn to charge up. Your next normal attack deals 3x damage and can't be evaded. | 100 |
| **Dexterity Drive** | Attack | Raise your SPD by 10% (Max 12%) of your base SPD for the rest of the battle, then deal damage of your element equal to 10% (Max 12%) of your SPD, plus 3 (Max 4) damage for every point of SPD you have above the enemy. Element advantage applies. Every cast makes you faster, so the hits keep growing. | 100 |
| **Double Edged Strike** | Attack | Deal damage of your element equal to 24% (Max 28.8%) of your ATK. You take 1/4 of the damage dealt as recoil (true damage). Element advantage applies. | 100 |
| **Elemental Strike** | Attack | Deal damage of your element equal to 20% (Max 24%) of your ATK. Element advantage applies (more damage against a weak element, less against a strong one). | 100 |
| **Final Gambit** | Attack | Sacrifice 25% of your current HP (it never drops you below 1 HP), then deal true damage equal to 1.6x (Max 1.92x) the HP you lost. | 100 |
| **Flurry** | Attack | Strike 3 times for 45% (Max 54%) of a normal attack each (135% (Max 162%) in total). Every hit can crit and be evaded on its own, and counts as a normal attack (lifesteal and on-hit effects apply). | 100 |
| **Vengeance ATK** | Attack | Deal true damage equal to 18% (Max 21.6%) of the enemy's ATK: the stronger its ATK, the harder this hits. | 100 |
| **Vengeance DEF** | Attack | Deal true damage equal to 18% (Max 21.6%) of the enemy's DEF: the tankier it is, the harder this hits. | 100 |
| **Celestial Blessing** | Control | Raise your max Mana by 50 (up to 300; not while silenced) and your Mana Regeneration by 18% (Max 22%), then heal HP equal to 1.5x (Max 1.8x) your max Mana. It fires again every time you have 100 Mana: the bigger bar stores extra Mana and makes each heal bigger. | 100 |
| **Dispel** | Control | Strip every buff from the enemy: raised ATK / DEF / SPD, timed buffs, crit rate above 5%, crit damage above 135%, Precision's evasion pierce, evasion rating above 5, Regeneration stacks, Endurance, barriers, lifesteal, debuff immunity and Momentum. Then deal true damage equal to 12% (Max 14.4%) of your ATK for each buff removed (every Regeneration stack counts). | 100 |
| **Mana Reaver** | Control | Absorb 20% (Max 24%) of the enemy's current Mana (you gain it) and deal true damage equal to the absorbed Mana x 10. | 100 |
| **Purify** | Control | Remove every Burn, Poison, Stun, Freeze, Sleep, Blind, Hex, Hypnotize, Silence, Mana Regeneration stop and stat drop on you. You can't get new debuffs for 2 turns, and you heal 10% (Max 12%) of your max HP for each effect removed. | 100 |
| **Restricted Instinct** | Control | Silence the enemy for 2 turns: it can't use its active talent, and its max Mana is cut to 100 (any Mana above that is lost). | 100 |
| **Blaze** | Debuff | Inflict a stack of Burn. At the start of each of its turns a burning enemy takes true damage equal to 5% (Max 6%) of its max HP per stack (max 5 stacks = 25% (Max 30%)), and all healing it receives is reduced by 80%. Each tick can miss: 10% with one stack, +7% for every extra stack (max 38%). A miss puts the fire out and removes every stack. | 100 |
| **Blind** | Debuff | Blind the enemy for 2 turns: each of its normal attacks has a 50% chance to miss. | 100 |
| **Breaker ATK** | Debuff | Lower the enemy's ATK by 18% (Max 22%) of its base ATK. Every cast stacks. | 100 |
| **Breaker DEF** | Debuff | Lower the enemy's DEF by 18% (Max 22%) of its base DEF. Every cast stacks. | 100 |
| **Frostbite** | Debuff | Lower the enemy's SPD by 15% (Max 18%) of its base SPD (stacks up to 3 times), then try to Freeze it: 25% (Max 30%) chance at full SPD, growing as its SPD drops, up to 60% once its SPD is 35% of its base SPD or lower. A frozen card skips its next turn and can't evade until then. | 100 |
| **Hex** | Debuff | Swap the enemy's ATK and DEF for 2 turns. They swap back when the Hex ends. Then, if its ATK is higher than its DEF, you permanently gain ATK equal to the difference; if its DEF is higher, you gain DEF instead. | 100 |
| **Hypnotize** | Debuff | Add a Hypnotize stack to the enemy. Each stack gives it a 20% chance to attack itself instead of you (that hit deals 50% of its normal damage). When it hits itself, the stacks reset and it can't be hypnotized for 2 turns. | 100 |
| **Paralysis** | Debuff | 40% (Max 48%) chance to Stun the enemy: it skips its next turn and permanently loses 10% of its base DEF. | 100 |
| **Poison** | Debuff | Inflict a stack of Poison. At the start of each of its turns a poisoned enemy takes 5 true damage plus 4% (Max 4.8%) of its max HP per stack. Each tick can miss: 15% with one stack, +7% for every extra stack. A miss cures the poison and removes every stack. | 100 |
| **Sleep** | Debuff | Put the enemy to sleep for 2 turns: it skips them and can't evade, and attacks don't wake it up. While it sleeps, your normal attacks on it deal 60% less damage and your Mana Regeneration is cut to 80%. | 100 |
| **Unlucky Coin** | Debuff | Roll a 20-sided dice: the enemy loses the roll x 2 (Max 2.4) in one random stat: ATK, DEF, SPD or max HP (1 to 40 (Max 48) points). HP points count like the HP stat. | 100 |
| **Burst Barrier** | Defense | Raise a barrier worth 10% (Max 12%) of your max HP for 3 turns. It absorbs all damage before your HP. If it breaks, it explodes and deals true damage equal to 25% (Max 30%) of your ATK to the enemy; if it lasts, it fades without exploding. While the barrier is up, your Mana Regeneration is reduced by 50%. Casting again refreshes the time and keeps the bigger barrier. | 100 |
| **Endurance** | Defense | For 3 turns, take 45% less damage from normal attacks, and 35% (Max 42%) of the damage blocked goes back to the attacker as true damage. Piercing Edge weakens the reduction. | 100 |
| **Evasion** | Defense | Gain +17 (Max 20) evasion rating every cast (stacks, no limit). The real chance to evade a normal attack has diminishing returns and never passes 75% (rating 25 = 21.3%, 50 = 36.5%, 100 = 55.2%). | 100 |
| **Regeneration** | Healing | Heal 5% (Max 6%) of your max HP now and gain a Regeneration stack: at the start of each of your turns you heal 5% (Max 6%) of your max HP per stack (max 3 stacks = 15% (Max 18%) per turn). Each turn's heal can fail: 10% with one stack, +10% for every extra stack (max 30%). A failed turn heals nothing and loses one stack. | 100 |
| **Mimic** | Special | Copy one enemy talent per cast. First choice: Nothingness, if any enemy has it (a trap that costs you HP). Next: an active talent of the strongest enemy card, which you cast yourself at once. Otherwise: a passive talent of the strongest enemy card, which you keep for the rest of the battle. Once Mimic has learned a passive, every later cast heals you 10% (Max 12%) of your max HP instead. | 100 |
| **Soul Swap** | Special | If your HP% is lower than the enemy's, swap HP% with it (you get its HP%, it gets yours). Usable once per battle. Your Mana bar grows to 200 at the start of the battle so you can pay for it. | 200 |
| **Amplifier ATK** | Stat Boost | Permanently raise your ATK by 20% (Max 24%) of your base ATK. Every cast stacks. | 100 |
| **Amplifier DEF** | Stat Boost | Permanently raise your DEF by 20% (Max 24%) of your base DEF. Every cast stacks. | 100 |
| **Lucky Coin** | Stat Boost | Roll a 20-sided dice and gain the roll x 3 (Max 3.6) in one random stat: ATK, DEF, SPD or HP (1 to 60 (Max 72) points). HP points count like the HP stat and are healed at once. | 100 |
| **Precision** | Stat Boost | Gain +16% (Max 19%) crit rate (max 100%) and +25% (Max 30%) crit damage. Every cast stacks. From the first cast on, your critical hits can't be evaded. | 100 |
| **Trick Room ATK** | Stat Boost | If the enemy's ATK is higher than yours, take 80% (Max 96%) of the difference: you gain that much ATK and the enemy loses it. No effect if your ATK is already higher. | 100 |
| **Trick Room DEF** | Stat Boost | If the enemy's DEF is higher than yours, take 80% (Max 96%) of the difference: you gain that much DEF and the enemy loses it. No effect if your DEF is already higher. | 100 |
| **True Sight** | Stat Boost | The enemy loses 30% (Max 36%) of its evasion rating, and you gain 70% of the rating it lost. | 100 |

## Passive talents (34)

| Talent | Category | Effect (base value, **Max** = God) |
|---|---|---|
| **Charge Attack** | Attack | While your HP is above 30%, your normal attacks are stored instead of hitting (up to 4). When 4 are stored, or once your HP is 30% or lower, your next attack releases them: its normal damage plus 160% of all the stored damage. |
| **Haste** | Attack | +15% (Max 18%) SPD. While your SPD is higher than the enemy's, each of your turns has a 20% (Max 24%) chance to attack a second time. |
| **Infernus** | Attack | 15% (Max 18%) of your normal attack damage is dealt as true damage (it ignores DEF and damage reduction), and you heal 50% of that true damage. |
| **Lethal Clarity** | Attack | +18% (Max 22%) crit rate and +12% (Max 14%) ATK. Whenever you land a critical hit, heal 50% (Max 60%) of the damage dealt. |
| **Momentum** | Attack | Every normal attack that deals damage gives a stack of +6% (Max 7.2%) attack damage, up to 5 stacks (+30% (Max 36%)). Missing or getting evaded resets all stacks. |
| **Piercing Edge** | Attack | Your normal attacks ignore 30% (Max 36%) of the enemy's DEF, and the enemy's damage reduction (Endurance, Protector) loses 50% of its effect against you. |
| **Slayer** | Attack | Each of your normal attacks deals extra damage equal to 5% (Max 6%) of the enemy's current HP (strongest against a healthy enemy). |
| **Celestial Influence** | Control | Start the battle with +40% (Max 48%) Mana Regeneration, while the enemy's Mana Regeneration is reduced by 40% (Max 48%). |
| **Silence** | Control | Whenever the enemy uses its active talent, it gets no Mana Regeneration for the rest of that turn and its next 2 turns. Mana from being hit or evading still counts. |
| **Grievous Limiter** | Debuff | When the battle starts, lower the enemy's ATK by 72% (Max 86%) of its base ATK. It gets back 12% of its base ATK at the start of each of your turns until the whole debuff is gone. |
| **Rot** | Debuff | The enemy's healing and lifesteal are 75% (Max 90%) weaker for the whole battle. Every time Rot cuts a heal, you permanently gain ATK equal to 5% (Max 6%) of the healing it stopped. |
| **Counter Stance** | Defense | +10 (Max 12) evasion rating, and you can evade skill damage too (Elemental Strike, Dexterity Drive, Life Sap...), not only normal attacks. Every time you evade, you strike back at once for 40% (Max 48%) of a normal attack. The counter can't be countered and gives no Mana. |
| **Death's Dance** | Defense | 40% (Max 48%) of all damage you take is stored instead of hitting your HP. If the stored damage ever reaches your current HP, you fall at once. Each evade removes 30% of the stored damage; each of your attacks that hits removes 10%, and a critical hit removes another 15%. |
| **Gigantify** | Defense | +18% (Max 22%) max HP, and a 15% (Max 18%) chance to shrug off each debuff (statuses and stat drops from the enemy). |
| **Guts** | Defense | Once per battle, survive a hit that would knock you out with 1 HP left, then gain +30% ATK for 5 turns. |
| **Protector** | Defense | The first time your HP is below 30% at the start of your turn: heal 20% (Max 24%) of your max HP, raise your DEF by 35% (Max 42%) of base DEF and take 5% (Max 6%) less damage for the rest of the battle. Once per battle. |
| **Reversion** | Defense | The first time your HP is below 20% at the start of your turn, both sides go back to their base stats and full base HP, then you gain +12% (Max 14%) DEF and SPD. Once per battle. |
| **Thorns** | Defense | 20% (Max 24%) of the damage you take from normal attacks goes back to the attacker as true damage (25% (Max 30%) from critical hits). |
| **Blood Surge** | Healing | The first time your HP is at or below 30% at the start of your turn, gain +45% (Max 54%) Lifesteal for the rest of the battle (you heal that % of the damage your normal attacks deal). |
| **Bloodthirster** | Healing | On your first turn gain 18% (Max 22%) Lifesteal and +23% (Max 27.6%) healing received (max +50%). Every turn after that, gain +3% (Max 4%) Lifesteal (max 75%). Lifesteal heals you that % of the damage your normal attacks deal. |
| **Cursed** | Healing | Every time the enemy casts an active talent, it takes true damage equal to 5% (Max 6%) of its max HP, and you heal 50% of it. |
| **Greedy** | Healing | 80% of every heal the enemy gets (talents, regeneration, lifesteal) heals you instead. |
| **Leech** | Healing | After every normal attack (yours and the enemy's): deal true damage equal to 1% (Max 1.2%) of the enemy's max HP, raise your max HP by 1% (Max 1.2%) of your base max HP, and heal exactly the damage dealt. |
| **Life Sap** | Healing | At the start of each of your turns, deal damage equal to 2% (Max 2.4%) of the enemy's max HP and heal 175% of the damage dealt. |
| **Blackhole** | Special | While this card is in battle, the enemy can't raise its ATK, DEF, SPD or max HP: every stat gain is swallowed. 30% of what it swallows from the enemy goes to you as permanent stats, and you keep 50% of your own stat gains. Stat drops and healing still work. |
| **Chaotic** | Special | While this card is in battle, stat changes are reversed for both sides: anything that would raise ATK, DEF, SPD or max HP lowers it instead, and anything that would lower it raises it. |
| **Gamble** | Special | At the start of the battle you draw 1 random active and 1 random passive talents that you don't have yet. They work at 60% strength for this battle. |
| **Nothingness** | Special | Does nothing by itself... but any card that gets it from another talent (copied by Mimic, drawn by Gamble, or any other way) instantly takes true damage equal to 30% of its max HP. |
| **Berserker** | Stat Boost | While your HP is below 40%, your ATK is raised by 50% (Max 60%) of your base ATK. The bonus goes away if you heal back above 40% (checked at the start of each of your turns). |
| **Divine Blessing** | Stat Boost | Start the battle blessed: +30% (Max 36%) ATK and +30% (Max 36%) DEF for your first 5 turns. |
| **Dominance** | Stat Boost | While your HP% is greater than or equal to the enemy's, your ATK is raised by 50% (Max 60%) of your base ATK (checked at the start of each of your turns). |
| **Doppelganger** | Stat Boost | Whenever the enemy raises its ATK, DEF, SPD or max HP, or heals, you gain 110% of that amount too. |
| **Overload ATK** | Stat Boost | When the battle starts, raise your ATK by 80% (Max 96%) of your base ATK. At the start of each of your turns it drops by 8% of your base ATK, and it keeps dropping below base: the longer the battle, the lower it gets. |
| **Overload DEF** | Stat Boost | When the battle starts, raise your DEF by 80% (Max 96%) of your base DEF. At the start of each of your turns it drops by 8% of your base DEF, and it keeps dropping below base: the longer the battle, the lower it gets. |

## Balance notes

Use the **1v1 Talent Matrix** (Test Mode > 1v1 TALENT MATRIX, or *Tools > AniGame > 1v1 Talent Matrix*): every
talent fights every other talent on identical God cards, sides swapping every battle. About 50% is balanced.

Latest run (2026-11-26, 30 battles per pair, HP ×20, after the changes below):

- **Strongest:** Burst Barrier 95%, Life Sap 93%, Regeneration 90%, Celestial Blessing 90%, Leech 87%, Blaze 86%.
- **Weakest:** Nothingness 13%, Silence 14%, Celestial Influence 14%, Restricted Instinct 14%, True Sight 17%,
  Trick Room DEF 18%, Rot 19%.
- The matrix cards have equal ATK and DEF, so it undersells talents that feed on stat gaps (Hex, Trick Room) or on
  the enemy healing (Rot).

Changes on 2026-11-25/26:

- **Blaze / Poison:** every tick can miss (Blaze 10%, Poison 15% with one stack, +5% per extra stack); a miss
  removes every stack.
- **Leech:** heals only the damage it dealt. **Burst Barrier:** −50% Mana Regeneration while the barrier is up.
- **Overload ATK / DEF:** keep decaying below base (optional floor). **Death's Dance:** each hit removes 10% of the
  stored damage. **Dexterity Drive:** each cast first gives +10% base SPD.
- **Sleep:** attacks don't wake the target; normal attacks on it deal −30% and the caster's Mana Regeneration is
  halved while it sleeps. **Celestial Blessing:** fires at its Mana cost even after the bar grows.
- **Rot:** gains ATK equal to 5% of the healing it stops. **Silence** is now a passive: when the enemy casts, its
  Mana Regeneration stops for that turn and 2 more. **Nothingness:** punishes every way of getting it (Mimic,
  Gamble…). **Hex:** after the swap you gain the ATK/DEF gap. **Blackhole:** 30% of what it swallows goes to you,
  and you keep half of your own gains.
- **Regeneration:** each turn's heal can fail (10% with one stack, +10% per extra stack); a failed turn heals
  nothing and loses a stack. **Slayer:** extra damage is a % of the enemy's *current* HP. **Counter Stance:** can
  evade (and counter) skill damage too. **Assassinate:** the strike is that turn's normal attack (no second
  attack). **Frostbite:** the Freeze chance grows as the enemy's SPD drops (25% at full SPD, up to 60% at 35% SPD).

## Adding a talent

See [data-pipeline.md](../tech/data-pipeline.md#add-a-talent). Put every number in a field, build
`DefaultDescription` from those fields with the `Pct/PctF/Num/N/TurnsS` helpers (they add the "(Max …)" text),
add the class to `TalentCatalog` for its Library category, and add the asset to `CardDatabase.talents`.
