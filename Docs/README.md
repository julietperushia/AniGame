# AniGame documentation

Written on **2026-11-24** against commit `53e815d`, including the uncommitted changes in the working tree on
that date (the new `UI/Fx/` module, `TooltipTrigger`, and edits to every screen).

## The one rule: the code is the source of truth

Every number in these docs comes from the code, and each table names the file it came from. If a doc and the
code disagree, **the code wins**. Fix the doc, or file the difference as a bug in
[project/roadmap.md](project/roadmap.md#known-issues). The in-game **Library** screen builds its pages from the
same rules at runtime, so it is always current. These docs are a snapshot.

The original design document, *"Thiet ke Game The Bai - He thong 1 The Nang Cap"* ("Card Game Design: the
Single Upgradable Card System"), is referenced by section number in `GameRules.cs` but is **not in the repo**.
Where these docs mention a doc section (for example "doc 7.1"), they mean that document.

## Reading order

| # | Document | Read it for |
|---|---|---|
| 1 | [GDD.md](GDD.md) | What the game is, the core loop, progression, every system in brief |
| 2 | [design/cards-and-rarity.md](design/cards-and-rarity.md) | Stats, rolls, the 9 rarities, build slots |
| 3 | [design/combat.md](design/combat.md) | Turn order, mana, the damage formula, status effects |
| 4 | [design/elements.md](design/elements.md) | The 9×9 element chart |
| 5 | [design/talents.md](design/talents.md) | All 38 talents |
| 6 | [design/fusion-and-rerolls.md](design/fusion-and-rerolls.md) | Raising rarity, rerolling builds, God Orbs, dismantling |
| 7 | [design/economy-and-stamina.md](design/economy-and-stamina.md) | Currencies, shop, rewards, stamina |
| 8 | [design/gacha-packs.md](design/gacha-packs.md) | The 6 packs and their odds |
| 9 | [design/buffs.md](design/buffs.md) | The 22 permanent buffs |
| 10 | [design/campaign.md](design/campaign.md) | Locations, floors, difficulty curve |
| 11 | [ux/screens-and-flow.md](ux/screens-and-flow.md) | The 9 screens and how they connect |
| 12 | [art/art-and-audio-guide.md](art/art-and-audio-guide.md) | Visual direction, replaceable art, sound |
| 13 | [tech/architecture.md](tech/architecture.md) | How the code is organised and the rules it must keep |
| 14 | [tech/data-pipeline.md](tech/data-pipeline.md) | CSV → assets; how to add cards, talents, packs, buffs |
| 15 | [tech/save-format.md](tech/save-format.md) | What is saved, where, and how it is protected |
| 16 | [tech/editor-tools.md](tech/editor-tools.md) | Tools menu, balance simulator, Dev Bridge |
| 17 | [project/roadmap.md](project/roadmap.md) | Feature status, known issues, open questions |
| 18 | [project/changelog.md](project/changelog.md) | History so far |

## Conventions

- File names are lowercase kebab-case.
- HP is always shown on the ×10 scale used in battle (a stat of 100 HP is 1,000 HP).
- "+3%" on a chance means **percentage points** (12% → 15%), not a relative increase, unless a doc says otherwise.
- Code references use repo-relative paths with a line number where it helps: `Assets/Scripts/Rules/GameRules.cs:48`.
- Sections that need a decision nobody has made yet are marked **TBD**.
